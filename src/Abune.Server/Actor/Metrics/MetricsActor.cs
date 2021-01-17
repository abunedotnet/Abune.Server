//-----------------------------------------------------------------------
// <copyright file="MetricsActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Abune.Server.Actor.Command;
    using Abune.Server.Actor.State;
    using Abune.Server.Sharding;
    using Akka.Actor;
    using Akka.Actor.Internal;
    using Akka.Cluster;
    using Akka.Cluster.Metrics;
    using Akka.Cluster.Metrics.Events;
    using Akka.Cluster.Metrics.Serialization;
    using Akka.Cluster.Sharding;
    using Akka.Event;
    using Akka.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// Actor collecting system metrics.
    /// </summary>
    public class MetricsActor : ReceiveActor, IWithUnboundedStash
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private readonly Cluster cluster;
        private readonly ClusterMetrics clusterMetrics;
        private readonly MetricsState state;
        private TimeSpan interval = TimeSpan.MaxValue;
        private ClusterMetricsChanged lastClusterMetrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsActor"/> class.
        /// </summary>
        /// <param name="interval">The interval.</param>
        /// <param name="cluster">The cluster.</param>
        /// <param name="clusterMetrics">The cluster metrics.</param>
        public MetricsActor(TimeSpan interval, Cluster cluster, ClusterMetrics clusterMetrics)
        {
            this.cluster = cluster;
            this.clusterMetrics = clusterMetrics;
            this.state = new MetricsState();
            this.interval = interval;
            this.Stash = new UnboundedStashImpl(Context);
            this.Become(() => this.Start());
        }

        /// <summary>Gets or sets the stash.</summary>
        /// <value>The stash.</value>
        public IStash Stash { get; set; }

        /// <summary>User overridable callback.
        /// <para></para>
        /// Is called when an Actor is started.
        /// Actors are automatically started asynchronously when created.
        /// Empty default implementation.</summary>
        protected override void PreStart()
        {
            base.PreStart();

            this.clusterMetrics.Subscribe(this.Self);
        }

        /// <summary>User overridable callback.
        /// <para></para>
        /// Is called asynchronously after 'actor.stop()' is invoked.
        /// Empty default implementation.</summary>
        protected override void PostStop()
        {
            base.PostStop();

            this.clusterMetrics.Unsubscribe(this.Self);
        }

        private static void RequestShardRegionStats(string shardRegion)
        {
            ClusterSharding.Get(Context.System).ShardRegion(shardRegion).Tell(GetShardRegionStats.Instance);
        }

        private void Start()
        {
            this.Stash.UnstashAll();
            this.Receive<ClusterMetricsChanged>(clusterMetrics => this.lastClusterMetrics = clusterMetrics);
            this.Receive<StartMetricsUpdate>(_ => this.Become(this.MetricsQueryObjectShardRegion));
            this.Receive<RequestStateCommand>(_ => this.RespondState(_.ReplyTo));
            Context.System.Scheduler.ScheduleTellOnce(this.interval, this.Self, new StartMetricsUpdate(), this.Self);
            this.LogClusterMetrics();
        }

        private void MetricsQueryObjectShardRegion()
        {
            RequestShardRegionStats(ShardRegions.OBJECTREGION);
            this.Receive<ShardRegionStats>(shardRegionStats =>
            {
                this.LogShardRegionStats(ShardRegions.OBJECTREGION, shardRegionStats);
                this.Become(this.MetricsQueryAreaShardRegion);
            });
            this.Receive<RequestStateCommand>(_ => this.RespondState(_.ReplyTo));
            this.ReceiveAny(_ => this.Stash.Stash());
        }

        private void MetricsQueryAreaShardRegion()
        {
            RequestShardRegionStats(ShardRegions.AREAREGION);
            this.Receive<ShardRegionStats>(shardRegionStats =>
            {
                this.LogShardRegionStats(ShardRegions.AREAREGION, shardRegionStats);
                this.Become(() => this.Start());
            });
            this.Receive<RequestStateCommand>(_ => this.RespondState(_.ReplyTo));
            this.ReceiveAny(_ => this.Stash.Stash());
        }

        private void LogClusterMetrics()
        {
            if (this.lastClusterMetrics == null)
            {
                return;
            }

            foreach (var nodeMetrics in this.lastClusterMetrics.NodeMetrics)
            {
                if (nodeMetrics.Address.Equals(this.cluster.SelfAddress))
                {
                    this.UpdateMemory(nodeMetrics);
                    this.UpdateCpu(nodeMetrics);
                }
            }
        }

        private void UpdateMemory(NodeMetrics nodeMetrics)
        {
            Option<StandardMetrics.Memory> memory = StandardMetrics.ExtractMemory(nodeMetrics);
            if (memory.HasValue)
            {
                this.state.Memory = memory.Value;
                this.log.Info("METRICS: memory used: {0:0.00} Mb", memory.Value.Used / 1024 / 1024);
            }
        }

        private void UpdateCpu(NodeMetrics nodeMetrics)
        {
            Option<StandardMetrics.Cpu> cpu = StandardMetrics.ExtractCpu(nodeMetrics);
            if (cpu.HasValue)
            {
                this.state.Cpu = cpu.Value;
                this.log.Info("METRICS: cpu load: {0:0.00}% ({1} processors)", this.state.Cpu.TotalUsage / 100, this.state.Cpu.ProcessorsNumber);
            }
        }

        private void LogShardRegionStats(string shardRegion, ShardRegionStats stats)
        {
            int shardCount = stats.Stats.Keys.Count();
            int entityCount = stats.Stats.Values.Sum();
            this.log.Info($"METRICS: shard region '{shardRegion}': {shardCount} shards, {entityCount} entities");
            if (shardRegion == ShardRegions.AREAREGION)
            {
                this.state.AreaEntityCount = entityCount;
                this.state.AreaShardCount = shardCount;
            }

            if (shardRegion == ShardRegions.OBJECTREGION)
            {
                this.state.ObjectEntityCount = entityCount;
                this.state.ObjectShardCount = shardCount;
            }
        }

        private void RespondState(IActorRef replyTo)
        {
            string json = JsonConvert.SerializeObject(this.state);
            replyTo.Tell(new RespondStateCommand(json));
        }

        private class StartMetricsUpdate
        {
        }
    }
}
