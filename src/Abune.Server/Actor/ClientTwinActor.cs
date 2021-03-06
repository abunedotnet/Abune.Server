﻿//-----------------------------------------------------------------------
// <copyright file="ClientTwinActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using Abune.Server.Actor.Command;
    using Abune.Server.Actor.State;
    using Abune.Server.Sharding;
    using Abune.Shared.Command;
    using Abune.Shared.Command.Contract;
    using Abune.Shared.Message;
    using Abune.Shared.Message.Contract;
    using Abune.Shared.Protocol;
    using Abune.Shared.Util;
    using Akka.Actor;
    using Akka.Actor.Internal;
    using Akka.Cluster.Sharding;
    using Akka.Event;
    using Akka.IO;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Akka.IO.Udp;

    /// <summary>Actor representing a connected game client.</summary>
    public class ClientTwinActor : ReceiveActor, IWithUnboundedStash
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private readonly TimeSpan keepAliveInterval = TimeSpan.FromSeconds(10);
        private readonly TimeSpan clientTimeout = TimeSpan.FromSeconds(20);
        private readonly ClientTwinState state = new ClientTwinState();
        private ReliableUdpMessaging reliableClientMessaging = new ReliableUdpMessaging();
        private IActorRef shardRegionArea;
        private IActorRef shardRegionObject;
        private IActorRef udpSenderActor;
        private IActorRef authenticationActor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientTwinActor"/> class.
        /// </summary>
        /// <param name="socketActorRef">The socket actor reference.</param>
        /// <param name="authenticationActorRef">The authentication actor ref.</param>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="shardRegionArea">The shard region area.</param>
        /// <param name="shardRegionObject">The shard region object.</param>
        public ClientTwinActor(IActorRef socketActorRef, IActorRef authenticationActorRef, IPEndPoint endpoint, IActorRef shardRegionArea, IActorRef shardRegionObject)
        {
            this.udpSenderActor = socketActorRef;
            this.authenticationActor = authenticationActorRef;
            this.state.Endpoint = endpoint;
            this.shardRegionObject = shardRegionObject;
            this.shardRegionArea = shardRegionArea;
            this.reliableClientMessaging.OnProcessCommandMessage = this.ProcessRequestFromClient;
            this.reliableClientMessaging.OnSendFrame = this.SendFrameToClient;
            this.reliableClientMessaging.OnDeadLetter = this.OnDeadLetter;
            this.Stash = new BoundedStashImpl(Context);
            this.Become(this.BecomeAuthenticating);
        }

        /// <summary>Gets or sets the stash.</summary>
        /// <value>The stash.</value>
        public IStash Stash { get; set; }

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

        private static string BuildAuthenticationChallenge()
        {
            var random = new Random();
            var authenticationChallenge = new StringBuilder();
            authenticationChallenge.Append(random.Next(0, int.MaxValue));
            return authenticationChallenge.ToString();
        }

        private void BecomeAuthenticating()
        {
            this.Receive<UdpTransferFrame>(c =>
            {
                this.ProcessUdpLoginFrame(c);
                this.SynchronizeMessages();
            });
            this.Receive<AuthenticationSuccess>(c =>
            {
                this.state.Authenticated = true;
                this.WelcomeClient();
                this.Become(this.BecomeActive);
            });
            this.Receive<AuthenticationFailure>(c =>
            {
                this.log.Debug($"[Client:{this.state.ClientId}] failed to authenticate: {c.Error}");
                this.Self.Tell(PoisonPill.Instance);
            });
            this.Receive<ReceiveTimeout>(r =>
            {
                this.log.Warning($"[Client:{this.state.ClientId}] timed out");
                this.Self.Tell(PoisonPill.Instance);
            });
            this.Receive<CommandFailed>(c =>
            {
                this.log.Error($"Command failed: {c.Cmd}");
            });
            this.Receive<RequestStateCommand>(c =>
            {
                this.RespondState(c.ReplyTo);
            });
            this.ReceiveAny(_ => this.Stash.Stash());
        }

        private void BecomeActive()
        {
            this.Receive<UdpTransferFrame>(c =>
            {
                this.ProcessUdpTransferFrame(c);
                this.SynchronizeMessages();
            });
            this.Receive<ObjectCommandRequestEnvelope>(c =>
            {
                this.ProcessRequestFromClient(c.Message);
                this.SynchronizeMessages();
            });
            this.Receive<ObjectCommandResponseEnvelope>(c =>
            {
                this.ProcessResponseToClient(c);
                this.SynchronizeMessages();
            });
            this.Receive<ReceiveTimeout>(r =>
            {
                this.SynchronizeMessages();
                this.OnKeepAlive();
                this.SetReceiveTimeout(this.keepAliveInterval);
            });
            this.Receive<CommandFailed>(c =>
            {
                this.log.Error($"Command failed: {c.Cmd}");
            });
            this.Receive<RequestStateCommand>(c =>
            {
                this.RespondState(c.ReplyTo);
            });
            this.Stash.UnstashAll();
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

        private void ProcessUdpLoginFrame(UdpTransferFrame frame)
        {
            switch (frame.Type)
            {
                case FrameType.ClientHello:
                    var msgClientHello = new ClientHelloMessage(frame.MessageBuffer);
                    this.state.ClientId = msgClientHello.ClientId;
                    this.state.ClientVersion = msgClientHello.Version;
                    this.RequestAuthentication();
                    break;
                case FrameType.ClientAuthenticationResponse:
                    var msgClientAuthenticationResponse = new ClientAuthenticationResponse(frame.MessageBuffer);
                    this.AuthenticateClient(msgClientAuthenticationResponse);
                    break;
                default:
                    this.log.Warning($"[Client:{this.state.ClientId} => Server] not authenticated - unexpected frame type '{frame.Type}'");
                    break;
            }
        }

        private void ProcessUdpTransferFrame(UdpTransferFrame frame)
        {
            this.UpdateKeepAlive();
            switch (frame.Type)
            {
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

        private void ProcessRequestFromClient(ObjectCommandEnvelope cmdMsg)
        {
            try
            {
                BaseCommand parsedCommand = null;
                switch (cmdMsg.Command.Type)
                {
                    case CommandType.SubscribeArea:
                        parsedCommand = this.TellArea(cmdMsg, _ => new SubscribeAreaCommand(_));
                        break;
                    case CommandType.UnsubscribeArea:
                        parsedCommand = this.TellArea(cmdMsg, _ => new UnsubscribeAreaCommand(_));
                        break;
                    case CommandType.ObjectUpdatePosition:
                        parsedCommand = this.TellObject(cmdMsg, _ => new ObjectUpdatePositionCommand(_));
                        break;
                    case CommandType.ObjectCreate:
                        parsedCommand = this.TellObject(cmdMsg, _ => new ObjectCreateCommand(_));
                        break;
                    case CommandType.ObjectDestroy:
                        parsedCommand = this.TellObject(cmdMsg, _ => new ObjectDestroyCommand(_));
                        break;
                    case CommandType.ObjectLock:
                        parsedCommand = this.TellObject(cmdMsg, _ => new ObjectLockCommand(_));
                        break;
                    case CommandType.ObjectUnlock:
                        parsedCommand = this.TellObject(cmdMsg, _ => new ObjectUnlockCommand(_));
                        break;
                    case CommandType.ObjectValueUpdate:
                        parsedCommand = this.TellObject(cmdMsg, _ => new ObjectValueUpdateCommand(_));
                        break;
                    case CommandType.ObjectValueRemove:
                        parsedCommand = this.TellObject(cmdMsg, _ => new ObjectValueRemoveCommand(_));
                        break;
                    case CommandType.EventPoint:
                        parsedCommand = this.TellEvent(cmdMsg, _ => new EventPointCommand(_), _ => new[] { Locator.GetAreaIdFromWorldPosition(_.TargetPosition) });
                        break;
                    case CommandType.EventLine:
                        parsedCommand = this.TellEvent(cmdMsg, _ => new EventLineCommand(_), _ => Locator.GetAreaIdsWithinWorldBoundaries(_.StartPosition, _.EndPosition));
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

        private void ProcessResponseToClient(ObjectCommandResponseEnvelope c)
        {
            this.log.Debug($"[Server => Client:{this.state.ClientId}] type: {c.Message.Command.Type}");
            var controlFlag = GetControlFlags(c);
            this.reliableClientMessaging.SendCommand(c.Message.SenderId, c.Message.Command, c.Message.ToObjectId, controlFlag);
        }

        private T TellObject<T>(ObjectCommandEnvelope cmdMsg, Func<BaseCommand, T> createFunc)
            where T : BaseCommand, IObjectCommand
        {
            T cmd = createFunc(cmdMsg.Command);
            var commandEnvelope = new ObjectCommandEnvelope(cmdMsg.SenderId, cmd, cmdMsg.ToObjectId);
            object message = commandEnvelope;
            if ((cmd.Flags & CommandFlags.QuorumRequest) != 0)
            {
                message = new QuorumRequestEnvelope(commandEnvelope);
            }

            this.shardRegionObject.Tell(message);
            return cmd;
        }

        private T TellEvent<T>(ObjectCommandEnvelope cmdMsg, Func<BaseCommand, T> createFunc, Func<T, IEnumerable<ulong>> extractAreas)
            where T : BaseCommand, ICanLocate
        {
            T cmd = createFunc(cmdMsg.Command);
            var areas = extractAreas(cmd);
            if (!areas.Any())
            {
                this.log.Warning($"[{this.Self.Path.Name}] no areas for event [{cmd.Type}]");
            }

            foreach (ulong areaId in areas)
            {
                this.shardRegionArea.Tell(new AreaCommandEnvelope(areaId, new ObjectCommandEnvelope(cmdMsg.SenderId, cmd, 0)));
            }

            return cmd;
        }

        private T TellArea<T>(ObjectCommandEnvelope cmdMsg, Func<BaseCommand, T> createFunc)
            where T : BaseCommand, IAreaCommand
        {
            T cmd = createFunc(cmdMsg.Command);
            this.shardRegionArea.Tell(new AreaCommandEnvelope(cmd.AreaId, new ObjectCommandEnvelope(0, cmd, 0)));
            return cmd;
        }

        private void RespondState(IActorRef replyTo)
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new IPEndpointConverter());
            string json = JsonConvert.SerializeObject(this.state, settings);
            replyTo.Tell(new RespondStateCommand(json));
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

        private void WelcomeClient()
        {
            if (this.state.ClientVersion != Shared.Constants.Version.PROTOCOL)
            {
                var msgServerHelloFail = new ServerHelloMessage() { Message = $"FAIL: INVALID CLIENT VERSION '{this.state.ClientVersion}'", Version = Shared.Constants.Version.PROTOCOL };
                this.log.Error($"[{this.state.ClientId}] invalid version: {this.state.ClientVersion}");
                this.SendFrameToClient(FrameType.ServerHello, msgServerHelloFail.Serialize());
                this.Self.Tell(PoisonPill.Instance);
                return;
            }

            var msgServerHello = new ServerHelloMessage() { Message = $"HELLO {this.state.ClientId}", Version = Shared.Constants.Version.PROTOCOL };
            this.SendFrameToClient(FrameType.ServerHello, msgServerHello.Serialize());
        }

        private void RequestAuthentication()
        {
            this.state.AuthenticationChallenge = BuildAuthenticationChallenge();
            var msgAuthenticationRequest = new ServerAuthenticationRequest() { AuthenticationChallenge = this.state.AuthenticationChallenge };
            this.SendFrameToClient(FrameType.ServerAuthenticationRequest, msgAuthenticationRequest.Serialize());
        }

        private void AuthenticateClient(ClientAuthenticationResponse msg)
        {
            this.authenticationActor.Tell(new RequestAuthenticationCommand()
            {
                ReplyTo = this.Self,
                Token = msg.AuthenticationToken,
                AuthenticationChallenge = this.state.AuthenticationChallenge,
            });
        }

        private class IPEndpointConverter : JsonConverter
        {
            /// <summary>
            /// Determines whether this instance can convert the specified object type.
            /// </summary>
            /// <param name="objectType">Type of the object.</param>
            /// <returns>
            /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
            /// </returns>
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(IPEndPoint);
            }

            /// <summary>
            /// Writes the JSON representation of the object.
            /// </summary>
            /// <param name="writer">The <see cref="Newtonsoft.Json.JsonWriter" /> to write to.</param>
            /// <param name="value">The value.</param>
            /// <param name="serializer">The calling serializer.</param>
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var ep = (IPEndPoint)value;
                var jo = new JObject();
                jo.Add("Address", ep.Address.ToString());
                jo.Add("Port", ep.Port);
                jo.WriteTo(writer);
            }

            /// <summary>
            /// Reads the JSON representation of the object.
            /// </summary>
            /// <param name="reader">The <see cref="Newtonsoft.Json.JsonReader" /> to read from.</param>
            /// <param name="objectType">Type of the object.</param>
            /// <param name="existingValue">The existing value of object being read.</param>
            /// <param name="serializer">The calling serializer.</param>
            /// <returns>
            /// The object value.
            /// </returns>
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var jo = JObject.Load(reader);
                var address = jo["Address"].ToObject<IPAddress>(serializer);
                int port = (int)jo["Port"];
                return new IPEndPoint(address, port);
            }
        }
    }
}
