//-----------------------------------------------------------------------
// <copyright file="MetricsActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

    /// <summary>
    /// Actor collecting system metrics.
    /// </summary>
    public class MetricsActor : ReceiveActor, IWithUnboundedStash
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private readonly Cluster cluster = Cluster.Get(Context.System);
        private readonly ClusterMetrics metricsExtension = ClusterMetrics.Get(Context.System);
        private TimeSpan interval = TimeSpan.MaxValue;
        private ClusterMetricsChanged lastClusterMetrics;

        /// <summary>Initializes a new instance of the <see cref="MetricsActor"/> class.</summary>
        /// <param name="interval">The interval.</param>
        public MetricsActor(TimeSpan interval)
        {
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

            this.metricsExtension.Subscribe(this.Self);
        }

        /// <summary>User overridable callback.
        /// <para></para>
        /// Is called asynchronously after 'actor.stop()' is invoked.
        /// Empty default implementation.</summary>
        protected override void PostStop()
        {
            base.PostStop();

            this.metricsExtension.Unsubscribe(this.Self);
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
                    this.LogMemory(nodeMetrics);
                    this.LogCpu(nodeMetrics);
                }
            }
        }

        private void LogMemory(NodeMetrics nodeMetrics)
        {
            Option<StandardMetrics.Memory> memory = StandardMetrics.ExtractMemory(nodeMetrics);
            if (memory.HasValue)
            {
                this.log.Info("METRICS: memory used: {0:0.00} Mb", memory.Value.Used / 1024 / 1024);
            }
        }

        private void LogCpu(NodeMetrics nodeMetrics)
        {
            Option<StandardMetrics.Cpu> cpu = StandardMetrics.ExtractCpu(nodeMetrics);
            if (cpu.HasValue)
            {
                this.log.Info("METRICS: cpu load: {0:0.00}% ({1} processors)", cpu.Value.TotalUsage / 100, cpu.Value.ProcessorsNumber);
            }
        }

        private void LogShardRegionStats(string shardRegion, ShardRegionStats stats)
        {
            int areaCount = stats.Stats.Keys.Count();
            int entitiesTotal = stats.Stats.Values.Sum();
            this.log.Info($"METRICS: shard region '{shardRegion}': {areaCount} shards, {entitiesTotal} entities");
        }

        private class StartMetricsUpdate
        {
        }
    }
}
