//-----------------------------------------------------------------------
// <copyright file="AkkaNodeService.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Abune.Server.Actor;
    using Abune.Server.Actor.Metrics;
    using Abune.Server.Config;
    using Abune.Shared.Message;
    using Akka.Actor;
    using Akka.Cluster;
    using Akka.Cluster.Metrics;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>Service running the akka node.</summary>
    public class AkkaNodeService : IHostedService
    {
        private readonly ILogger log;
        private readonly IOptions<AkkaNodeServiceConfig> config;

        private ActorSystem system;

        /// <summary>Initializes a new instance of the <see cref="AkkaNodeService"/> class.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="config">The configuration.</param>
        public AkkaNodeService(ILogger<AkkaNodeServiceConfig> logger, IOptions<AkkaNodeServiceConfig> config)
        {
            this.log = logger;
            this.config = config;
        }

        /// <summary>Gets the version.</summary>
        /// <returns>The actual version.</returns>
        public static string GetVersion()
        {
            return typeof(AkkaNodeService).Assembly.GetName().Version.ToString();
        }

        /// <summary>Triggered when the application host is ready to start the service.</summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        /// <returns>Task for completion.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            string version = GetVersion();
            this.log.LogInformation($"Starting system: {this.config.Value.SystemName} (v{version})");
            this.system = ActorSystem.Create(this.config.Value.SystemName, this.config.Value.AkkaConfiguration);
            this.CreateServerActor();
            this.CreateMetricsActor();
            return Task.CompletedTask;
        }

        /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        /// <returns>Task for completion.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.log.LogInformation($"Stopping system: {this.config.Value.SystemName}");
            this.system.Terminate();
            return Task.CompletedTask;
        }

        private void CreateServerActor()
        {
            IActorRef serverActor = this.system.ActorOf(Props.Create(() => new ServerActor(this.config.Value.ShardCountArea, this.config.Value.ShardCountObject, this.config.Value.ShardCountSession, this.config.Value.Auth0Issuer, this.config.Value.Auth0Audience, this.config.Value.SigningKey)), "Server");
            serverActor.Tell(new StartServerMessage(new IPEndPoint(IPAddress.Any, this.config.Value.ServerPort)));
        }

        private void CreateMetricsActor()
        {
            Cluster cluster = Cluster.Get(this.system);
            ClusterMetrics clusterMetrics = ClusterMetrics.Get(this.system);
            Props props = Props.Create(() => new MetricsActor(
                TimeSpan.FromSeconds(this.config.Value.Metrics.IntervalSeconds),
                cluster,
                clusterMetrics));
            IActorRef serverActor = this.system.ActorOf(props, "MetricsActor");
        }
    }
}
