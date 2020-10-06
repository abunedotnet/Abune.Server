//-----------------------------------------------------------------------
// <copyright file="ClientTwinActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor
{
    using System;
    using System.Net;
    using Abune.Server.Actor.State;
    using Abune.Server.Sharding;
    using Abune.Shared.Command;
    using Abune.Shared.Message;
    using Abune.Shared.Protocol;
    using Abune.Shared.Util;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Akka.Event;
    using Akka.IO;
    using static Akka.IO.Udp;

    /// <summary>Actor representing a connected game client.</summary>
    public class ClientTwinActor : ReceiveActor
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private readonly TimeSpan keepAliveInterval = TimeSpan.FromSeconds(10);
        private readonly TimeSpan clientTimeout = TimeSpan.FromMinutes(5);
        private ClientTwinState state = new ClientTwinState();
        private ReliableUdpMessaging reliableClientMessaging = new ReliableUdpMessaging();
        private IActorRef shardRegionArea;
        private IActorRef shardRegionObject;
        private IActorRef udpSenderActor;

        /// <summary>Initializes a new instance of the <see cref="ClientTwinActor"/> class.</summary>
        /// <param name="socketActorRef">The socket actor reference.</param>
        /// <param name="endpoint">The endpoint.</param>
        public ClientTwinActor(IActorRef socketActorRef, IPEndPoint endpoint)
        {
            this.state.Endpoint = endpoint;
            this.udpSenderActor = socketActorRef;
            this.shardRegionObject = ClusterSharding.Get(Context.System).ShardRegion(ShardRegions.OBJECTREGION);
            this.shardRegionArea = ClusterSharding.Get(Context.System).ShardRegion(ShardRegions.AREAREGION);
            this.reliableClientMessaging.OnProcessCommandMessage = this.ProcessCommandMessage;
            this.reliableClientMessaging.OnSendFrame = this.SendFrameToClient;
            this.reliableClientMessaging.OnDeadLetter = this.OnDeadLetter;
            this.Receive<UdpTransferFrame>(c =>
            {
                this.ProcessUdpTransferFrame(c);
                this.SynchronizeMessages();
            });
            this.Receive<ObjectCommandRequestEnvelope>(c =>
            {
                this.ProcessCommandMessage(c.Message);
                this.SynchronizeMessages();
            });
            this.Receive<ReceiveTimeout>(r =>
            {
                this.SynchronizeMessages();
                this.OnKeepAlive();
                this.SetReceiveTimeout(this.keepAliveInterval);
            });
            this.Receive<ObjectCommandResponseEnvelope>(c =>
            {
                this.log.Debug($"[Server => Client:{this.state.ClientId}] type: {c.Message.Command.Type}");
                var controlFlag = GetControlFlags(c);
                this.reliableClientMessaging.SendCommand(c.Message.SenderId, c.Message.Command, c.Message.ToObjectId, controlFlag);
                this.SynchronizeMessages();
            });
            this.Receive<CommandFailed>(c =>
            {
                this.log.Error($"Command failed: {c.Cmd}");
            });
        }

        private static MessageControlFlags GetControlFlags(ObjectCommandResponseEnvelope command)
        {
            switch (command.Message.Command.Type)
            {
                case CommandType.ObjectCreate:
                case CommandType.ObjectDestroy:
                case CommandType.ObjectLock:
                case CommandType.ObjectUnlock:
                case CommandType.SubscribeArea:
                case CommandType.UnsubscribeArea:
                    return MessageControlFlags.QOS0;
                default:
                    return MessageControlFlags.QOS0;
            }
        }

        private void SynchronizeMessages()
        {
            TimeSpan waitTime = this.reliableClientMessaging.SynchronizeMessages();
            if (waitTime != TimeSpan.Zero)
            {
                this.SetReceiveTimeout(waitTime);
            }
        }

        private void OnDeadLetter(UdpMessage obj)
        {
            Context.System.DeadLetters.Tell(obj, this.Self);
        }

        private void SendFrameToClient(FrameType frameType, byte[] buffer)
        {
            var frame = new UdpTransferFrame(frameType, buffer);
            this.SendFrameToClient(frame);
        }

        private void SendFrameToClient(UdpTransferFrame frame)
        {
            this.udpSenderActor.Tell(Udp.Send.Create(ByteString.FromBytes(frame.Serialize()), this.state.Endpoint), this.Self);
        }

        private void ProcessUdpTransferFrame(UdpTransferFrame frame)
        {
            this.UpdateKeepAlive();
            switch (frame.Type)
            {
                case FrameType.ClientHello:
                    var msgClientHello = new ClientHelloMessage(frame.MessageBuffer);
                    this.state.ClientId = msgClientHello.ClientId;
                    var msgServerHello = new ServerHelloMessage() { Message = $"Hello {msgClientHello.ClientId}" };
                    this.SendFrameToClient(FrameType.ServerHello, msgServerHello.Serialize());
                    break;
                case FrameType.ClientPing:
                    this.log.Debug($"[Client:{this.state.ClientId} => Server] PING");
                    var cmdClientPing = new ClientPingMessage(frame.MessageBuffer);
                    var now = TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
                    var msgServerPong = new ServerPongMessage() { ClientRequestTimestamp = cmdClientPing.ClientTimestamp, ClientResponseTimestamp = now, ServerTimestamp = now };
                    this.SendFrameToClient(FrameType.ServerPong, msgServerPong.Serialize());
                    break;
                case FrameType.ClientPong:
                    this.log.Debug($"[Client:{this.state.ClientId} => Server] PONG");
                    var msgClientPong = new ClientPongMessage(frame.MessageBuffer);
                    this.CalculateClientLatency(msgClientPong);
                    break;
                case FrameType.Message:
                    this.reliableClientMessaging.ProcessMessageFrame(frame);
                    break;
            }
        }

        private void ProcessCommandMessage(ObjectCommandEnvelope cmdMsg)
        {
            try
            {
                BaseCommand parsedCommand = null;
                switch (cmdMsg.Command.Type)
                {
                    case CommandType.SubscribeArea:
                        var cmdSubscribeArea = new SubscribeAreaCommand(cmdMsg.Command);
                        parsedCommand = cmdSubscribeArea;
                        this.shardRegionArea.Tell(new AreaCommandEnvelope(cmdSubscribeArea.AreaId, new ObjectCommandEnvelope(0, cmdSubscribeArea, 0)));
                        break;
                    case CommandType.UnsubscribeArea:
                        var cmdUnsubscribeArea = new UnsubscribeAreaCommand(cmdMsg.Command);
                        parsedCommand = cmdUnsubscribeArea;
                        this.shardRegionArea.Tell(new AreaCommandEnvelope(cmdUnsubscribeArea.AreaId, new ObjectCommandEnvelope(0, cmdUnsubscribeArea, 0)));
                        break;
                    case CommandType.ObjectCollision:
                        var cmdCollision = new ObjectCollisionCommand(cmdMsg.Command);
                        parsedCommand = cmdCollision;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdCollision, cmdMsg.ToObjectId));
                        break;
                    case CommandType.ObjectUpdatePosition:
                        var cmdUpdatePosition = new ObjectUpdatePositionCommand(cmdMsg.Command);
                        parsedCommand = cmdUpdatePosition;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdUpdatePosition, cmdMsg.ToObjectId));
                        break;
                    case CommandType.ObjectCreate:
                        var cmdObjectCreate = new ObjectCreateCommand(cmdMsg.Command);
                        parsedCommand = cmdObjectCreate;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdObjectCreate, cmdMsg.ToObjectId));
                        break;
                    case CommandType.ObjectDestroy:
                        var cmdObjectDestroy = new ObjectDestroyCommand(cmdMsg.Command);
                        parsedCommand = cmdObjectDestroy;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdObjectDestroy, cmdMsg.ToObjectId));
                        break;
                    case CommandType.ObjectLock:
                        var cmdLockObject = new ObjectLockCommand(cmdMsg.Command);
                        parsedCommand = cmdLockObject;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdLockObject, cmdMsg.ToObjectId));
                        break;
                    case CommandType.ObjectUnlock:
                        var cmdUnlockObject = new ObjectUnlockCommand(cmdMsg.Command);
                        parsedCommand = cmdUnlockObject;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdUnlockObject, cmdMsg.ToObjectId));
                        break;
                    case CommandType.ObjectValueUpdate:
                        var cmdObjectValueUpdate = new ObjectValueUpdateCommand(cmdMsg.Command);
                        parsedCommand = cmdObjectValueUpdate;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdObjectValueUpdate, cmdMsg.ToObjectId));
                        break;
                    case CommandType.ObjectValueRemove:
                        var cmdObjectValueRemove = new ObjectValueRemoveCommand(cmdMsg.Command);
                        parsedCommand = cmdObjectValueRemove;
                        this.shardRegionObject.Tell(new ObjectCommandEnvelope(cmdMsg.SenderId, cmdObjectValueRemove, cmdMsg.ToObjectId));
                        break;
                }

                string commandInfo = parsedCommand != null ? parsedCommand.ToString() : string.Empty;
                this.log.Debug($"[Client:{this.state.ClientId} => Server] type: {cmdMsg.Command.Type} ({commandInfo})");
            }
#pragma warning disable CA1031 // Keine allgemeinen Ausnahmetypen abfangen
            catch (Exception e)
#pragma warning restore CA1031 // Keine allgemeinen Ausnahmetypen abfangen
            {
                this.Sender.Tell(new Failure() { Exception = e });
            }
        }

        /// <summary>
        /// TimeOffset = (t1 - t0) - (t2 - t3)
        /// Rountrip = (t3 - t0) - (t2 - t1)
        ///     t0 is the client's timestamp of the request packet transmission,
        ///     t1 is the server's timestamp of the request packet reception,
        ///     t2 is the server's timestamp of the response packet transmission and
        ///     t3 is the client's timestamp of the response packet reception.
        /// </summary>
        private void CalculateClientLatency(ClientPongMessage msg)
        {
            var t0 = msg.ServerRequestTimestamp;
            var t1 = msg.ClientRequestTimestamp;
            var t2 = msg.ClientResponseTimestamp;
            var t3 = TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
            this.state.Latency = TimeSpan.FromTicks(((t1 - t0) + (t3 - t2)).Ticks / 2);
        }

        private void OnKeepAlive()
        {
            var msgServerPing = new ServerPingMessage() { ServerTimestamp = TimeSpan.FromTicks(DateTime.UtcNow.Ticks) };
            this.SendFrameToClient(FrameType.ServerPing, msgServerPing.Serialize());
            if (this.state.LastKeepAliveUtc + this.clientTimeout < DateTime.UtcNow)
            {
                this.Self.Tell(PoisonPill.Instance);
                this.log.Error($"[{this.Self.Path.Name}] timed out");
                return;
            }
        }

        private void UpdateKeepAlive()
        {
            this.state.LastKeepAliveUtc = DateTime.UtcNow;
            this.SetReceiveTimeout(this.keepAliveInterval);
        }
    }
}
