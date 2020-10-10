//-----------------------------------------------------------------------
// <copyright file="MetricsState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using Akka.Cluster.Metrics;

    /// <summary>
    /// State for metrics actor.
    /// </summary>
    public class MetricsState
    {
        /// <summary>
        /// Gets or sets memory metrics.
        /// </summary>
        /// <value>
        /// The memory metrics.
        /// </value>
        public StandardMetrics.Memory Memory { get; set; }

        /// <summary>
        /// Gets or sets cpu metrics.
        /// </summary>
        /// <value>
        /// The cpu metrics.
        /// </value>
        public StandardMetrics.Cpu Cpu { get; set; }

        /// <summary>
        /// Gets or sets the area entity count.
        /// </summary>
        /// <value>
        /// The area entity count.
        /// </value>
        public int AreaEntityCount { get; set; }

        /// <summary>
        /// Gets or sets the area shard count.
        /// </summary>
        /// <value>
        /// The area shard count.
        /// </value>
        public int AreaShardCount { get; set; }

        /// <summary>
        /// Gets or sets the object entity count.
        /// </summary>
        /// <value>
        /// The object entity count.
        /// </value>
        public int ObjectEntityCount { get; set; }

        /// <summary>
        /// Gets or sets the object shard count.
        /// </summary>
        /// <value>
        /// The object shard count.
        /// </value>
        public int ObjectShardCount { get; set; }
    }
}
