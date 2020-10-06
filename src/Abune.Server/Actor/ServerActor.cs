//-----------------------------------------------------------------------
// <copyright file="ServerActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using Abune.Server.Actor;
    using Abune.Server.Sharding;
    using Abune.Shared.Message;
    using Abune.Shared.Protocol;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Akka.Event;
    using Akka.IO;
    using static Akka.IO.Udp;

    /// <summary>Actor representing a server instance accepting incoming connections.</summary>
    public class ServerActor : ReceiveActor
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private IActorRef shardRegionObject;
        private IActorRef shardRegionArea;
        private int shardCountArea;
        private int shardCountObject;
        private Dictionary<string, IActorRef> clientTwinActors = new Dictionary<string, IActorRef>();
        private IActorRef udpManagerActor;

        /// <summary>Initializes a new instance of the <see cref="ServerActor"/> class.</summary>
        /// <param name="shardCountArea">The shard count area.</param>
        /// <param name="shardCountObject">The shard count object.</param>
        public ServerActor(int shardCountArea, int shardCountObject)
        {
            this.shardCountArea = shardCountArea;
            this.shardCountObject = shardCountObject;
            this.udpManagerActor = Udp.Instance.Apply(Context.System).Manager;
            this.InitializeShardRegions(Context.System);
            this.Receive<StartServerMessage>(c =>
            {
                this.log.Info($"Binding udp on {c.ServerEndpoint}");
                this.udpManagerActor.Tell(new Udp.Bind(this.Self, c.ServerEndpoint), this.Self);
            });
            this.Receive<Terminated>(t =>
            {
                this.log.Warning($"Client {t.ActorRef.Path.Name} terminated");
                if (this.clientTwinActors.ContainsKey(t.ActorRef.Path.Name))
                {
                    Context.Unwatch(this.clientTwinActors[t.ActorRef.Path.Name]);
                    this.clientTwinActors.Remove(t.ActorRef.Path.Name);
                }
            });
            this.Receive<Udp.Bound>(c =>
            {
                this.log.Info($"Bound to {c.LocalAddress}");
            });
            this.Receive<Udp.Received>(c =>
            {
                IActorRef senderSocketRef = this.Sender;
                if (c.Data.Count == 0)
                {
                    this.log.Warning("Invalid message: Empty");
                    return;
                }

                var message = new UdpTransferFrame(c.Data.ToArray());
#if UDPTRACE
                // LogUdpMessage(c);
#endif
                this.ProcessClientFrame(senderSocketRef, c, message);
            });
            this.Receive<CommandFailed>(c =>
            {
                this.log.Error($"Failed command: {c.Cmd}");
            });
        }

        private static string GetClientTwinActorName(EndPoint endpoint)
        {
            IPEndPoint ipEndPoint = endpoint as IPEndPoint;
            if (endpoint == null)
            {
                throw new InvalidOperationException($"invalid endpoint '{endpoint}'");
            }

            return $"ClientTwin-{ipEndPoint.Address}-{ipEndPoint.Port}";
        }

        private IActorRef GetClientTwinActor(EndPoint endpoint)
        {
            string clientTwinActorName = GetClientTwinActorName(endpoint);
            if (!this.clientTwinActors.ContainsKey(clientTwinActorName))
            {
                IActorRef clientTwinActor = Context.Child(clientTwinActorName);
                if (clientTwinActor != Nobody.Instance)
                {
                    this.clientTwinActors.Add(clientTwinActorName, clientTwinActor);
                    return clientTwinActor;
                }

                return null;
            }

            return this.clientTwinActors[clientTwinActorName];
        }

        private IActorRef CreateClientTwinActor(IActorRef socketActorRef, uint clientId, EndPoint clientEndPoint)
        {
            IPEndPoint ipClientEndpoint = clientEndPoint as IPEndPoint;
            if (ipClientEndpoint == null)
            {
                throw new InvalidOperationException($"invalid endpoint '{clientEndPoint}'");
            }

            string clientTwinActorName = GetClientTwinActorName(clientEndPoint);
            this.log.Info($"[Client:{clientId}] creating twin '{clientTwinActorName}'");
            if (!this.clientTwinActors.ContainsKey(clientTwinActorName))
            {
                this.clientTwinActors.Remove(clientTwinActorName);
                IPEndPoint clientReceiveEndPoint = clientEndPoint as IPEndPoint;
                if (clientReceiveEndPoint == null)
                {
                    throw new InvalidOperationException($"invalid endpoint type {clientEndPoint.GetType()}");
                }

                IActorRef clientTwinActor = Context.System.ActorOf(Props.Create(() => new ClientTwinActor(socketActorRef, clientReceiveEndPoint)), clientTwinActorName);
                Context.Watch(clientTwinActor);
                this.clientTwinActors.Add(clientTwinActorName, clientTwinActor);
                return clientTwinActor;
            }

            return this.clientTwinActors[clientTwinActorName];
        }

        private void LogUdpMessage(Udp.Received recv)
        {
            string dataBase64 = Convert.ToBase64String(recv.Data.ToArray(), Base64FormattingOptions.None);
            this.log.Debug($"[{recv.Sender}] sent [{recv.Data.Count}] {dataBase64}");
        }

        private void InitializeShardRegions(ActorSystem system)
        {
            this.shardRegionObject = ClusterSharding.Get(system).Start(typeName: ShardRegions.OBJECTREGION, entityProps: Props.Create<ObjectActor>(), settings: ClusterShardingSettings.Create(system), messageExtractor: new ObjectRegionMessageExtractor(this.shardCountObject));
            this.shardRegionArea = ClusterSharding.Get(system).Start(typeName: ShardRegions.AREAREGION, entityProps: Props.Create<AreaActor>(), settings: ClusterShardingSettings.Create(system), messageExtractor: new AreaRegionMessageExtractor(this.shardCountArea));
        }

        private void ProcessClientFrame(IActorRef socketActorRef, Received received, UdpTransferFrame udpTransferFrame)
        {
            try
            {
                IActorRef clientTwinActor;
                if (udpTransferFrame.Type == FrameType.ClientHello)
                {
                    var cmdClientHello = new ClientHelloMessage(udpTransferFrame.MessageBuffer);
                    clientTwinActor = this.CreateClientTwinActor(socketActorRef, cmdClientHello.ClientId, received.Sender);
                }
                else
                {
                    clientTwinActor = this.GetClientTwinActor(received.Sender);
                }

                if (clientTwinActor == null)
                {
                    this.log.Warning("Invalid client {0}", received.Sender);
                    return;
                }

                clientTwinActor.Tell(udpTransferFrame, this.Self);
            }
#pragma warning disable CA1031 // Keine allgemeinen Ausnahmetypen abfangen
            catch (Exception e)
#pragma warning restore CA1031 // Keine allgemeinen Ausnahmetypen abfangen
            {
                this.Sender.Tell(new Failure() { Exception = e });
            }
        }
    }
}
