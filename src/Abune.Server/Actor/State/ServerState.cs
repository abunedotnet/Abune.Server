//-----------------------------------------------------------------------
// <copyright file="ServerState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using System.Collections.Generic;
    using Akka.Actor;

    /// <summary>Sevrer actor state.</summary>
    public class ServerState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerState"/> class.
        /// </summary>
        public ServerState()
        {
            this.ClientTwinActors = new Dictionary<string, IActorRef>();
        }

        /// <summary>
        /// Gets or sets the shard count for areas.
        /// </summary>
        /// <value>
        /// The shard count area.
        /// </value>
        public int ShardCountArea { get; set; }

        /// <summary>
        /// Gets or sets the shard count for objects.
        /// </summary>
        /// <value>
        /// The shard count object.
        /// </value>
        public int ShardCountObject { get; set; }

        /// <summary>
        /// Gets the client twin actors.
        /// </summary>
        /// <value>
        /// The client twin actors.
        /// </value>
        public Dictionary<string, IActorRef> ClientTwinActors { get; private set; }
    }
}
