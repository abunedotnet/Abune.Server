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
    using System.Security;
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Actor.State;
    using Abune.Server.Sharding;
    using Abune.Shared.Message;
    using Abune.Shared.Protocol;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Akka.Event;
    using Akka.IO;
    using Akka.Pattern;
    using Newtonsoft.Json;
    using static Akka.IO.Udp;

    /// <summary>Actor representing a server instance accepting incoming connections.</summary>
    public class ServerActor : ReceiveActor
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private IActorRef shardRegionObject;
        private IActorRef shardRegionArea;
        private IActorRef udpManagerActor;
        private IActorRef authenticationActor;
        private ServerState state;
        private IShardRegionResolver shardRegionResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerActor"/> class.
        /// </summary>
        /// <param name="shardCountArea">The shard count area.</param>
        /// <param name="shardCountObject">The shard count object.</param>
        /// <param name="auth0Issuer">The auth0 issuer.</param>
        /// <param name="auth0Audience">The auth0 audience.</param>
        /// <param name="signingKey">The signing key.</param>
        public ServerActor(int shardCountArea, int shardCountObject, string auth0Issuer, string auth0Audience, string signingKey)
        {
            this.state = new ServerState();
            this.state.ShardCountArea = shardCountArea;
            this.state.ShardCountObject = shardCountObject;
            this.state.Auth0Issuer = auth0Issuer;
            this.state.Auth0Audience = auth0Audience;
            this.state.SigningKey = signingKey;
            this.udpManagerActor = Udp.Instance.Apply(Context.System).Manager;
            this.authenticationActor = this.CreateAuthenticationActor();
            this.InitializeShardRegions(Context.System);
            this.Receive<StartServerMessage>(c =>
            {
                this.log.Info($"Binding udp on {c.ServerEndpoint}");
                this.udpManagerActor.Tell(new Udp.Bind(this.Self, c.ServerEndpoint), this.Self);
            });
            this.Receive<Terminated>(t =>
            {
                this.log.Warning($"Client {t.ActorRef.Path.Name} terminated");
                if (this.state.ClientTwinActors.ContainsKey(t.ActorRef.Path.Name))
                {
                    Context.Unwatch(this.state.ClientTwinActors[t.ActorRef.Path.Name]);
                    this.state.ClientTwinActors.Remove(t.ActorRef.Path.Name);
                }
            });
            this.Receive<RequestStateCommand>(c =>
            {
                this.RespondState(c.ReplyTo);
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
            if (!this.state.ClientTwinActors.ContainsKey(clientTwinActorName))
            {
                IActorRef clientTwinActor = Context.Child(clientTwinActorName);
                if (clientTwinActor != Nobody.Instance)
                {
                    this.state.ClientTwinActors.Add(clientTwinActorName, clientTwinActor);
                    return clientTwinActor;
                }

                return null;
            }

            return this.state.ClientTwinActors[clientTwinActorName];
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
            if (!this.state.ClientTwinActors.ContainsKey(clientTwinActorName))
            {
                this.state.ClientTwinActors.Remove(clientTwinActorName);
                IPEndPoint clientReceiveEndPoint = clientEndPoint as IPEndPoint;
                if (clientReceiveEndPoint == null)
                {
                    throw new InvalidOperationException($"invalid endpoint type {clientEndPoint.GetType()}");
                }

                IActorRef clientTwinActor = Context.System.ActorOf(Props.Create(() => new ClientTwinActor(socketActorRef, this.authenticationActor, clientReceiveEndPoint, ClusterSharding.Get(Context.System).ShardRegion(ShardRegions.AREAREGION), ClusterSharding.Get(Context.System).ShardRegion(ShardRegions.OBJECTREGION))), clientTwinActorName);
                Context.Watch(clientTwinActor);
                this.state.ClientTwinActors.Add(clientTwinActorName, clientTwinActor);
                return clientTwinActor;
            }

            return this.state.ClientTwinActors[clientTwinActorName];
        }

        private void LogUdpMessage(Udp.Received recv)
        {
            string dataBase64 = Convert.ToBase64String(recv.Data.ToArray(), Base64FormattingOptions.None);
            this.log.Debug($"[{recv.Sender}] sent [{recv.Data.Count}] {dataBase64}");
        }

        private void InitializeShardRegions(ActorSystem system)
        {
            this.shardRegionResolver = new DefaultShardRegionResolver(ClusterSharding.Get(system));
            this.shardRegionObject = ClusterSharding.Get(system).Start(typeName: ShardRegions.OBJECTREGION, entityProps: Props.Create(() => new ObjectActor(this.shardRegionResolver)), settings: ClusterShardingSettings.Create(system), messageExtractor: new ObjectRegionMessageExtractor(this.state.ShardCountObject));
            this.shardRegionArea = ClusterSharding.Get(system).Start(typeName: ShardRegions.AREAREGION, entityProps: Props.Create(() => new AreaActor(this.shardRegionResolver)), settings: ClusterShardingSettings.Create(system), messageExtractor: new AreaRegionMessageExtractor(this.state.ShardCountArea));
        }

        private void RespondState(IActorRef replyTo)
        {
            string json = JsonConvert.SerializeObject(this.state);
            replyTo.Tell(new RespondStateCommand(json));
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

        private IActorRef CreateAuthenticationActor()
        {
            // TODO SECURITY: throttle authentication actor with akka streams to suppress brute force attacks
            Props childActor = Props.Create(() => new AuthenticationActor(this.state.Auth0Issuer, this.state.Auth0Audience, this.state.SigningKey));
            return Context.ActorOf(Props.Create(() => new BackoffSupervisor(childActor, "Authentication", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), 1.5)));
        }
    }
}
