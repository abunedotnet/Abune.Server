//-----------------------------------------------------------------------
// <copyright file="AreaState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using System.Collections.Generic;
    using Akka.Actor;

    /// <summary>Area actor state.</summary>
    public class AreaState
    {
        /// <summary>Initializes a new instance of the <see cref="AreaState"/> class.</summary>
        /// <param name="parentSessionId">Identifier of the parent session.</param>
        /// <param name="areaId">Identifier of the area.</param>
        public AreaState(ulong parentSessionId, ulong areaId)
        {
            this.ParentSessionId = parentSessionId;
            this.AreaId = areaId;
            this.Subscriptions = new Dictionary<IActorRef, Subscription>();
            this.Objects = new Dictionary<ulong, object>();
        }

        /// <summary>
        /// Gets the parent session id.
        /// </summary>
        /// <value>
        /// The parent session id.
        /// </value>
        public ulong ParentSessionId { get; private set; }

        /// <summary>
        /// Gets the area identifier.
        /// </summary>
        /// <value>
        /// The area identifier.
        /// </value>
        public ulong AreaId { get; private set; }

        /// <summary>Gets the subscriptions.</summary>
        /// <value>The subscriptions.</value>
        public Dictionary<IActorRef, Subscription> Subscriptions { get; private set; }

        /// <summary>Gets the objects.</summary>
        /// <value>The objects.</value>
        public Dictionary<ulong, object> Objects { get; private set; }
    }
}
