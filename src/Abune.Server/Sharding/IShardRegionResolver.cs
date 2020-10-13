//-----------------------------------------------------------------------
// <copyright file="IShardRegionResolver.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Sharding
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Akka.Actor;

    /// <summary>Resolved ActorRefs for ShardRegions.</summary>
    public interface IShardRegionResolver
    {
        /// <summary>
        /// Gets the shard region.
        /// </summary>
        /// <param name="typeName">The name.</param>
        /// <returns>Actor reference to shard region.</returns>
        IActorRef GetShardRegion(string typeName);
    }
}
