//-----------------------------------------------------------------------
// <copyright file="ObjectActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor
{
    using System;
    using System.Globalization;
    using Abune.Server.Actor.Command;
    using Abune.Server.Actor.State;
    using Abune.Server.Command;
    using Abune.Server.Sharding;
    using Abune.Shared.Command;
    using Abune.Shared.Command.Contract;
    using Abune.Shared.Message;
    using Abune.Shared.Util;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Akka.Event;
    using Akka.Persistence;
    using Newtonsoft.Json;

    /// <summary>
    /// Actor representing a world object.
    /// </summary>
    public class ObjectActor : PersistentActor
    {
        private const uint LOCKNOOWNER = 0;
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private IActorRef shardRegionArea;
        private bool isInitialized;
        private ObjectState state = new ObjectState();

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectActor"/> class.
        /// </summary>
        /// <param name="shardRegionArea">Area shard region.</param>
        public ObjectActor(IActorRef shardRegionArea)
        {
            this.state.ObjectId = this.ExtractObjectId();
            this.shardRegionArea = shardRegionArea;
        }

        /// <summary>Gets id of the persistent entity for which messages should be replayed.</summary>
        public override string PersistenceId => $"OBJECT-{this.state.ObjectId}";

        /// <summary>
        /// Command handler. Typically validates commands against current state - possibly by communicating with other actors.
        /// On successful validation, one or more events are derived from command and persisted.
        /// </summary>
        /// <param name="message">Message to process.</param>
        /// <returns>Message is processed.</returns>
        protected override bool ReceiveCommand(object message)
        {
            if (message is SaveSnapshotSuccess)
            {
                this.Log.Debug("SaveSnapshotSuccess");
            }

            if (message is SaveSnapshotFailure)
            {
                this.Log.Error("SaveSnapshotFailure");
            }

            if (message is RequestStateCommand)
            {
                this.RespondState(((RequestStateCommand)message).ReplyTo);
                return true;
            }

            if (message is ReceiveTimeout)
            {
                this.ValidateResetLock();
            }
            else if (message is ObjectCommandEnvelope)
            {
                var objCmd = (ObjectCommandEnvelope)message;
                uint senderId = objCmd.SenderId;
                if (this.state.LockOwnerId != 0 && this.state.LockOwnerId != senderId && IsLockProtectedCommand(objCmd.Command.Type))
                {
                    this.log.Warning($"[Object:{this.state.ObjectId}] type: {objCmd.Command.Type} invalid lock owner");
                    return false;
                }

                this.ValidateResetLock();
                this.state.TimeStampLastCommand = DateTime.UtcNow;
                switch (objCmd.Command.Type)
                {
                    case CommandType.ObjectCollision:
                        this.ForwardToArea(objCmd);
                        break;
                    case CommandType.ObjectUpdatePosition:
                        this.UpdateStatePersistent(new ObjectUpdatePositionCommand(objCmd.Command), (cmd) => this.ForwardToArea(objCmd));
                        break;
                    case CommandType.ObjectValueUpdate:
                        this.UpdateStatePersistent(new ObjectValueUpdateCommand(objCmd.Command), (cmd) => this.ForwardToArea(objCmd));
                        break;
                    case CommandType.ObjectValueRemove:
                        this.UpdateStatePersistent(new ObjectValueRemoveCommand(objCmd.Command), (cmd) => this.ForwardToArea(objCmd));
                        break;
                    case CommandType.ObjectCreate:
                        ObjectCreateCommand createCmd = new ObjectCreateCommand(objCmd.Command);
                        this.NotifyEnterArea(senderId, Locator.GetAreaIdFromWorldPosition(this.state.WorldPositionX, this.state.WorldPositionY, this.state.WorldPositionZ));
                        this.UpdateStatePersistent(createCmd, (cmd) => this.ForwardToArea(objCmd));
                        break;
                    case CommandType.ObjectDestroy:
                        this.NotifyLeaveArea(Locator.GetAreaIdFromWorldPosition(this.state.WorldPositionX, this.state.WorldPositionY, this.state.WorldPositionZ));
                        this.ForwardToArea(objCmd);
                        this.Self.Tell(PoisonPill.Instance);
                        this.DeleteMessages(this.LastSequenceNr);
                        this.DeleteSnapshots(SnapshotSelectionCriteria.Latest);
                        break;
                    case CommandType.ObjectLock:
                        ObjectLockCommand lockCmd = new ObjectLockCommand(objCmd.Command);
                        this.Lock(lockCmd);
                        break;
                    case CommandType.ObjectUnlock:
                        ObjectUnlockCommand unlockCmd = new ObjectUnlockCommand(objCmd.Command);
                        this.Unlock(unlockCmd);
                        break;
                    default:
                        throw new InvalidOperationException(objCmd.Command.Type.ToString());
                }

                return true;
            }
            else if (message is NotifySubscribeObjectExistenceCommand)
            {
                NotifySubscribeObjectExistenceCommand cmd = (NotifySubscribeObjectExistenceCommand)message;
                this.NotifyNewSubscriber(cmd.Subscriber);
            }

            this.SetReceiveTimeout(this.state.LockTimeout);
            return true;
        }

        /// <summary>
        /// Recovery handler that receives persistent events during recovery. If a state snapshot has been captured and saved,
        /// this handler will receive a <see cref="SnapshotOffer" /> message followed by events that are younger than offer itself.
        /// This handler must not have side-effects other than changing persistent actor state i.e. it
        /// should not perform actions that may fail, such as interacting with external services,
        /// for example.
        /// If there is a problem with recovering the state of the actor from the journal, the error
        /// will be logged and the actor will be stopped.
        /// </summary>
        /// <param name="message">Message offered.</param>
        /// <returns>
        /// Message was processed.
        /// </returns>
        protected override bool ReceiveRecover(object message)
        {
            if (message is SnapshotOffer offeredSnapshot)
            {
                this.state = (ObjectState)offeredSnapshot.Snapshot;
            }
            else if (message is RecoveryCompleted)
            {
                this.log.Debug($"{this.Self.Path} recovery completed");
                this.isInitialized = true;
            }
            else if (message is BaseCommand)
            {
                this.UpdateState((BaseCommand)message);
            }

            return true;
        }

        private static bool IsLockProtectedCommand(CommandType commandType)
        {
            switch (commandType)
            {
                case CommandType.ObjectUpdatePosition:
                case CommandType.ObjectDestroy:
                case CommandType.ObjectLock:
                case CommandType.ObjectUnlock:
                    return true;
            }

            return false;
        }

        private void RespondState(IActorRef replyTo)
        {
            string json = JsonConvert.SerializeObject(this.state);
            replyTo.Tell(new RespondStateCommand(json));
        }

        private ulong ExtractObjectId()
        {
            return ulong.Parse(this.Self.Path.Name, CultureInfo.InvariantCulture);
        }

        private void InternalPersist<TEvent>(TEvent @event, Action<TEvent> handler)
        {
            this.Persist(@event, handler);
        }

        private void UpdateStatePersistent(BaseCommand command, Action<BaseCommand> finishedFandler)
        {
            this.InternalPersist(command, (cmd) =>
            {
                this.UpdateState(cmd);
                finishedFandler(cmd);
            });
        }

        private void UpdateState(BaseCommand command)
        {
            if (command is ICanLocate)
            {
                var canLocate = (ICanLocate)command;
                this.state.LastWorldPositionX = this.state.WorldPositionX;
                this.state.LastWorldPositionY = this.state.WorldPositionY;
                this.state.LastWorldPositionZ = this.state.WorldPositionZ;
                this.state.WorldPositionX = canLocate.WorldPositionX;
                this.state.WorldPositionY = canLocate.WorldPositionY;
                this.state.WorldPositionZ = canLocate.WorldPositionZ;
            }

            if (command is ICanRotate)
            {
                var canRotate = (ICanRotate)command;
                this.state.QuaternionX = canRotate.RotationX;
                this.state.QuaternionY = canRotate.RotationY;
                this.state.QuaternionZ = canRotate.RotationZ;
                this.state.QuaternionW = canRotate.RotationW;
            }

            if (command is ICanAccelerate)
            {
                var canAccelerate = (ICanAccelerate)command;
                this.state.VelocityX = canAccelerate.VelocityX;
                this.state.VelocityY = canAccelerate.VelocityY;
                this.state.VelocityZ = canAccelerate.VelocityZ;
                this.state.AngularVelocityX = canAccelerate.AngularVelocityX;
                this.state.AngularVelocityY = canAccelerate.AngularVelocityY;
                this.state.AngularVelocityZ = canAccelerate.AngularVelocityZ;
            }

            switch (command.Type)
            {
                case CommandType.ObjectLock:
                    var lockCmd = (ObjectLockCommand)command;
                    this.state.LockOwnerId = lockCmd.LockOwnerId;
                    this.state.LockTimeout = lockCmd.Timeout;
                    break;
                case CommandType.ObjectUnlock:
                    this.state.LockOwnerId = LOCKNOOWNER;
                    this.state.LockTimeout = TimeSpan.Zero;
                    break;
                case CommandType.ObjectCreate:
                    var createCmd = (ObjectCreateCommand)command;
                    this.state.ParentObjectId = createCmd.ParentObjectId;
                    this.state.OwnerId = createCmd.OwnerId;
                    this.state.TypeId = createCmd.TypeId;
                    this.state.LastWorldPositionX = this.state.WorldPositionX;
                    this.state.LastWorldPositionY = this.state.WorldPositionY;
                    this.state.LastWorldPositionZ = this.state.WorldPositionZ;
                    this.isInitialized = true;
                    break;
                case CommandType.ObjectDestroy:
                    break;
                case CommandType.ObjectUpdatePosition:
                    break;
                case CommandType.ObjectValueUpdate:
                    var valueUpdateCmd = (ObjectValueUpdateCommand)command;
                    if (!this.state.ObjectStateValues.ContainsKey(valueUpdateCmd.ValueId))
                    {
                        this.log.Debug($"[Object:{this.state.ObjectId}] creating new value '{valueUpdateCmd.ValueId}'");
                        this.state.ObjectStateValues.Add(valueUpdateCmd.ValueId, new ObjectStateValue() { Id = valueUpdateCmd.ValueId, Data = valueUpdateCmd.Data });
                    }
                    else
                    {
                        this.state.ObjectStateValues[valueUpdateCmd.ValueId].Data = valueUpdateCmd.Data;
                    }

                    break;
                case CommandType.ObjectValueRemove:
                    var valueRemoveCmd = (ObjectValueRemoveCommand)command;
                    if (this.state.ObjectStateValues.ContainsKey(valueRemoveCmd.ValueId))
                    {
                        this.log.Debug($"[Object:{this.state.ObjectId}] removing value '{valueRemoveCmd.ValueId}'");
                        this.state.ObjectStateValues.Remove(valueRemoveCmd.ValueId);
                    }

                    break;
            }
        }

        private void ForwardToArea(ObjectCommandEnvelope objCmd)
        {
            if (!this.isInitialized)
            {
                this.log.Error($"[Object:{this.state.ObjectId}] Cannot forward command type '{objCmd.Command.Type}'. Actor is not initialized!");
                return;
            }

            ulong oldAreaId = Locator.GetAreaIdFromWorldPosition(this.state.LastWorldPositionX, this.state.LastWorldPositionY, this.state.LastWorldPositionZ);
            ulong newAreaId = Locator.GetAreaIdFromWorldPosition(this.state.WorldPositionX, this.state.WorldPositionY, this.state.WorldPositionZ);
            if (oldAreaId != newAreaId)
            {
                this.shardRegionArea.Tell(new AreaCommandEnvelope(oldAreaId, new ObjectCommandEnvelope(objCmd.SenderId, objCmd.Command, objCmd.ToObjectId)));
                this.NotifyEnterArea(objCmd.SenderId, newAreaId);
                this.NotifyLeaveArea(oldAreaId);
            }

            this.shardRegionArea.Tell(new AreaCommandEnvelope(newAreaId, new ObjectCommandEnvelope(objCmd.SenderId, objCmd.Command, objCmd.ToObjectId)));
        }

        private void NotifyLeaveArea(ulong oldAreaId)
        {
            this.shardRegionArea.Tell(new ObjectLeaveAreaCommand() { ObjectId = this.state.ObjectId, AreaId = oldAreaId });
        }

        private void NotifyEnterArea(uint originalSenderId, ulong newAreaId)
        {
            this.shardRegionArea.Tell(new ObjectEnterAreaCommand() { ObjectId = this.state.ObjectId, AreaId = newAreaId });
            if (this.state.LockOwnerId != LOCKNOOWNER)
            {
                var lockCommand = new ObjectLockCommand(this.state.ObjectId, this.state.LockOwnerId, this.state.LockTimeout);
                this.shardRegionArea.Tell(new AreaCommandEnvelope(newAreaId, new ObjectCommandEnvelope(originalSenderId, lockCommand, this.state.ObjectId)));
            }
        }

        private void ValidateResetLock()
        {
            if (this.state.LockOwnerId == 0)
            {
                return;
            }

            if (this.state.TimeStampLastCommand + this.state.LockTimeout < DateTime.UtcNow)
            {
                this.Unlock(new ObjectUnlockCommand(this.state.ObjectId, LOCKNOOWNER));
            }
        }

        private void Lock(ObjectLockCommand lockCommand)
        {
            if (this.state.LockOwnerId != LOCKNOOWNER && lockCommand.LockOwnerId != LOCKNOOWNER && lockCommand.LockOwnerId != this.state.LockOwnerId)
            {
                this.Log.Warning("Lock requested from invalid owner. LockOwner {0} LockRequester {1}", this.state.LockOwnerId, lockCommand.LockOwnerId);
                return;
            }

            this.UpdateStatePersistent(lockCommand, (cmd) =>
            {
                this.ForwardToArea(new ObjectCommandEnvelope(this.state.LockOwnerId, new ObjectLockCommand(this.state.ObjectId, this.state.LockOwnerId, lockCommand.Timeout), this.state.ObjectId));
            });
        }

        private void Unlock(ObjectUnlockCommand unlockCommand)
        {
            if (this.state.LockOwnerId != LOCKNOOWNER && unlockCommand.LockOwnerId != LOCKNOOWNER && unlockCommand.LockOwnerId != this.state.LockOwnerId)
            {
                this.Log.Warning("Unlock requested from invalid owner. LockOwner {0} UnlockRequester {1}", this.state.LockOwnerId, unlockCommand.LockOwnerId);
                return;
            }

            this.UpdateStatePersistent(unlockCommand, (cmd) =>
            {
                this.ForwardToArea(new ObjectCommandEnvelope(this.state.LockOwnerId, new ObjectUnlockCommand(this.state.ObjectId, this.state.LockOwnerId), this.state.ObjectId));
            });
        }

        private void NotifyNewSubscriber(IActorRef subscriber)
        {
            var createCommand = new ObjectCreateCommand(0, this.state.ObjectId, this.state.ParentObjectId, this.state.OwnerId, this.state.TypeId, this.state.WorldPositionX, this.state.WorldPositionY, this.state.WorldPositionZ, this.state.QuaternionW, this.state.QuaternionX, this.state.QuaternionY, this.state.QuaternionZ);
            subscriber.Tell(this.CreateEnvelope(createCommand));
            if (this.state.LockOwnerId != LOCKNOOWNER)
            {
                var lockCommand = new ObjectLockCommand(this.state.ObjectId, this.state.LockOwnerId, this.state.LockTimeout);
                subscriber.Tell(this.CreateEnvelope(lockCommand));
                subscriber.Tell(this.CreateEnvelope(this.CreateObjectUpdatePositionCommand()));
            }

            foreach (var stateValue in this.state.ObjectStateValues)
            {
                var stateValueUpdate = new ObjectValueUpdateCommand(this.state.ObjectId, stateValue.Key, stateValue.Value.Data);
                subscriber.Tell(this.CreateEnvelope(stateValueUpdate));
            }
        }

        private ObjectCommandResponseEnvelope CreateEnvelope(BaseCommand command)
        {
            return new ObjectCommandResponseEnvelope(new ObjectCommandEnvelope(0, command, this.state.ObjectId));
        }

        private ObjectUpdatePositionCommand CreateObjectUpdatePositionCommand()
        {
            return new ObjectUpdatePositionCommand(
                this.state.WorldPositionX,
                this.state.WorldPositionY,
                this.state.WorldPositionZ,
                this.state.QuaternionW,
                this.state.QuaternionX,
                this.state.QuaternionY,
                this.state.QuaternionZ,
                this.state.VelocityX,
                this.state.VelocityY,
                this.state.VelocityZ,
                this.state.AngularVelocityX,
                this.state.AngularVelocityY,
                this.state.AngularVelocityZ,
                (ulong)DateTime.UtcNow.Ticks,
                (ulong)DateTime.UtcNow.Ticks);
        }
    }
}
