//-----------------------------------------------------------------------
// <copyright file="DefaultShardRegionResolver.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Sharding
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Akka.Actor;
    using Akka.Cluster.Sharding;

    /// <summary>
    /// Default implementation for shard region resolving.
    /// </summary>
    /// <seealso cref="Abune.Server.Sharding.IShardRegionResolver" />
    public class DefaultShardRegionResolver : IShardRegionResolver
    {
        private readonly ClusterSharding clusterSharding;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultShardRegionResolver"/> class.
        /// </summary>
        /// <param name="clusterSharding">Cluster sharding.</param>
        public DefaultShardRegionResolver(ClusterSharding clusterSharding)
        {
            this.clusterSharding = clusterSharding;
        }

        /// <summary>
        /// Gets the shard region.
        /// </summary>
        /// <param name="typeName">The name.</param>
        /// <returns>Actor reference for shard region.</returns>
        public IActorRef GetShardRegion(string typeName)
        {
            return this.clusterSharding.ShardRegion(typeName);
        }
    }
}
