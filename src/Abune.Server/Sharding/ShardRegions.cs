//-----------------------------------------------------------------------
// <copyright file="ShardRegions.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Sharding
{
    /// <summary>List of shard region types.</summary>
    public static class ShardRegions
    {
        /// <summary>The shard region for objects.</summary>
        public const string OBJECTREGION = "object";

        /// <summary>The shard region for areas.</summary>
        public const string AREAREGION = "area";

        /// <summary>The shard region for sessions.</summary>
        public const string SESSIONREGION = "session";
    }
}
