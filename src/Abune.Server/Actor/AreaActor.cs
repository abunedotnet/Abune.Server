//-----------------------------------------------------------------------
// <copyright file="AreaActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor
{
    using System;
    using Abune.Server.Actor.Command;
    using Abune.Server.Actor.Events;
    using Abune.Server.Actor.State;
    using Abune.Server.Command;
    using Abune.Server.Sharding;
    using Abune.Shared.Command;
    using Abune.Shared.Message;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Akka.Event;
    using Akka.Persistence;
    using Newtonsoft.Json;

    /// <summary>Actor representing areas.</summary>
    public class AreaActor : PersistentActor
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private AreaState state;
        private IActorRef shardRegionObject;

        /// <summary>
        /// Initializes a new instance of the <see cref="AreaActor"/> class.
        /// </summary>
        /// <param name="shardRegionObject">Object shard region.</param>
        public AreaActor(IActorRef shardRegionObject)
        {
            ulong areaId = this.ExtractObjectId();
            this.state = new AreaState(areaId);
            this.shardRegionObject = shardRegionObject;
        }

        /// <summary>Gets id of the persistent entity for which messages should be replayed.</summary>
        public override string PersistenceId => $"AREA-{this.state.AreaId}";

        /// <summary>
        /// Command handler. Typically validates commands against current state - possibly by communicating with other actors.
        /// On successful validation, one or more events are derived from command and persisted.
        /// </summary>
        /// <param name="message">Message received.</param>
        /// <returns>true if message is handled.</returns>
        protected override bool ReceiveCommand(object message)
        {
            if (message is SaveSnapshotSuccess)
            {
                this.log.Debug("SaveSnapshotSuccess");
                return true;
            }

            if (message is SaveSnapshotFailure)
            {
                this.log.Error("SaveSnapshotFailure");
                return true;
            }

            if (message is RequestStateCommand)
            {
                this.RespondState(((RequestStateCommand)message).ReplyTo);
                return true;
            }

            if (message is AreaCommandEnvelope)
            {
                var areaCmd = (AreaCommandEnvelope)message;
                switch (areaCmd.ObjectCommandEnvelope.Command.Type)
                {
                    case Abune.Shared.Command.CommandType.SubscribeArea:
                        var subscribeCmd = new SubscribeAreaCommand(areaCmd.ObjectCommandEnvelope.Command);
                        this.Subscribe(this.Sender, subscribeCmd.MessagePriority);
                        break;
                    case Abune.Shared.Command.CommandType.UnsubscribeArea:
                        this.Unsubscribe(this.Sender);
                        break;
                    default:
                        this.PublishCommand(areaCmd.ObjectCommandEnvelope);
                        break;
                }
            }
            else if (message is IInternalCommand)
            {
                this.UpdateStatePersistent((IInternalCommand)message);
            }

            return true;
        }

        /// <summary>
        /// Recovery handler that receives persistent events during recovery.
        /// </summary>
        /// <param name="message">Message offered.</param>
        /// <returns>true if message is handled.</returns>
        protected override bool ReceiveRecover(object message)
        {
            if (message is SnapshotOffer offeredSnapshot)
            {
                this.state = (AreaState)offeredSnapshot.Snapshot;
            }
            else if (message is RecoveryCompleted)
            {
                this.log.Debug($"{this.Self.Path} recovery completed");
            }
            else if (message is IInternalCommand)
            {
                this.UpdateState((IInternalCommand)message);
            }

            return true;
        }

        private void RespondState(IActorRef replyTo)
        {
            string json = JsonConvert.SerializeObject(this.state);
            replyTo.Tell(new RespondStateCommand(json));
        }

        private ulong ExtractObjectId()
        {
            return ulong.Parse(this.Self.Path.Name, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void InternalPersist<TEvent>(TEvent @event, Action<TEvent> handler)
        {
            this.Persist(@event, handler);
        }

        private void UpdateStatePersistent(IInternalCommand command)
        {
            this.InternalPersist(command, this.UpdateState);
        }

        private void UpdateState(IInternalCommand command)
        {
            if (command is ObjectEnterAreaCommand)
            {
                this.AddObject((ObjectEnterAreaCommand)command);
            }
            else if (command is ObjectLeaveAreaCommand)
            {
                this.RemoveObject((ObjectLeaveAreaCommand)command);
            }
        }

        private void AddObject(ObjectEnterAreaCommand command)
        {
            ulong objectId = command.ObjectId;
            if (!this.state.Objects.ContainsKey(objectId))
            {
                this.state.Objects.Add(objectId, null);
            }
        }

        private void RemoveObject(ObjectLeaveAreaCommand command)
        {
            this.state.Objects.Remove(command.ObjectId);
        }

        private void Subscribe(IActorRef subscriber, ushort messagePriority)
        {
            if (!this.state.Subscriptions.ContainsKey(subscriber))
            {
                this.state.Subscriptions.Add(subscriber, new Subscription { SubscriberActorRef = subscriber, MessagePriority = messagePriority });
            }

            foreach (ulong objectId in this.state.Objects.Keys)
            {
                this.shardRegionObject.Tell(new NotifySubscribeObjectExistenceCommand() { ObjectId = objectId, Subscriber = subscriber });
            }
        }

        private void Unsubscribe(IActorRef subscriber)
        {
            if (this.state.Subscriptions.ContainsKey(subscriber))
            {
                this.state.Subscriptions.Remove(subscriber);
            }

            foreach (ulong objectId in this.state.Objects.Keys)
            {
                this.shardRegionObject.Tell(new NotifyUnsubscribeObjectExistenceCommand() { ObjectId = objectId, Subscriber = subscriber });
            }
        }

        private void ForwardToObjects(BaseCommand command)
        {
            foreach (ulong objectId in this.state.Objects.Keys)
            {
                this.shardRegionObject.Forward(new ObjectCommandEnvelope(0, command, objectId));
            }
        }

        private void PublishCommand(ObjectCommandEnvelope command)
        {
            if (this.state.Subscriptions.Values.Count == 0)
            {
                this.log.Warning($"No subscribers!");
            }

            foreach (Subscription sub in this.state.Subscriptions.Values)
            {
                if (sub.MessagePriority <= command.Command.Priority)
                {
                    this.log.Debug($"{this.Self.Path} => forwarding to {sub.SubscriberActorRef.Path}");
                    sub.SubscriberActorRef.Tell(new ObjectCommandResponseEnvelope(command));
                }
            }
        }
    }
}
