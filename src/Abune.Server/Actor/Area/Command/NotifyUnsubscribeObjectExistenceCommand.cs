//-----------------------------------------------------------------------
// <copyright file="NotifyUnsubscribeObjectExistenceCommand.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Command
{
    using Abune.Shared.Message.Contract;
    using Akka.Actor;

    /// <summary>
    /// Notification command.
    /// </summary>
    public class NotifyUnsubscribeObjectExistenceCommand : ICanRouteToObject
    {
        /// <summary>Gets or sets the object identifier.</summary>
        /// <value>The object identifier.</value>
        public ulong ObjectId { get; set; }

        /// <summary>Gets or sets the subscriber.</summary>
        /// <value>The subscriber.</value>
        public IActorRef Subscriber { get; set; }

        /// <summary>Gets the target object identifier.</summary>
        /// <value>Object identifier.</value>
        public ulong ToObjectId => this.ObjectId;
    }
}
