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
        public AreaState()
        {
            this.Subscriptions = new Dictionary<IActorRef, Subscription>();
            this.Objects = new Dictionary<ulong, object>();
        }

        /// <summary>Gets the subscriptions.</summary>
        /// <value>The subscriptions.</value>
        public Dictionary<IActorRef, Subscription> Subscriptions { get; private set; }

        /// <summary>Gets the objects.</summary>
        /// <value>The objects.</value>
        public Dictionary<ulong, object> Objects { get; private set; }
    }
}
