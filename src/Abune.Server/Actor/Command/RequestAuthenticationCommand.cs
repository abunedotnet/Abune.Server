//-----------------------------------------------------------------------
// <copyright file="RequestAuthenticationCommand.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Command
{
    using Abune.Server.Actor.Events;
    using Akka.Actor;

    /// <summary>
    /// Request for authentication.
    /// </summary>
    public class RequestAuthenticationCommand
    {
        /// <summary>
        /// Gets or sets the reply to.
        /// </summary>
        /// <value>
        /// The reply to.
        /// </value>
        public IActorRef ReplyTo { get; set; }

        /// <summary>
        /// Gets or sets the token.
        /// </summary>
        /// <value>
        /// The token.
        /// </value>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the authentication challenge.
        /// </summary>
        /// <value>
        /// The authentication challenge.
        /// </value>
        public string AuthenticationChallenge { get; set; }
    }
}
