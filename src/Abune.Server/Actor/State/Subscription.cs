//-----------------------------------------------------------------------
// <copyright file="Subscription.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using Akka.Actor;

    /// <summary>Client subscription.</summary>
    public class Subscription
    {
        /// <summary>Gets or sets the actor reference.</summary>
        /// <value>The actor reference.</value>
        public IActorRef SubscriberActorRef { get; set; }

        /// <summary>Gets or sets the message priority.</summary>
        /// <value>The message priority.</value>
        public ushort MessagePriority { get; set; }
    }
}
