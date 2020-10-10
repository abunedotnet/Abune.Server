//-----------------------------------------------------------------------
// <copyright file="RequestStateCommand.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Command
{
    using Akka.Actor;

    /// <summary>
    /// Command for requesting the actor state.
    /// </summary>
    public class RequestStateCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestStateCommand"/> class.
        /// </summary>
        /// <param name="replyTo">Reply to target.</param>
        public RequestStateCommand(IActorRef replyTo)
        {
            this.ReplyTo = replyTo;
        }

        /// <summary>
        /// Gets the reply to.
        /// </summary>
        /// <value>
        /// The reply to.
        /// </value>
        public IActorRef ReplyTo { get; private set; }
    }
}
