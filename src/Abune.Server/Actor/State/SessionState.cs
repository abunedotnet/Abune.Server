//-----------------------------------------------------------------------
// <copyright file="SessionState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using System;

    #pragma warning disable CA1724
    /// <summary>Area actor state.</summary>
    public class SessionState
    {
        /// <summary>Initializes a new instance of the <see cref="SessionState"/> class.</summary>
        /// <param name="sessionId">Identifier of the area.</param>
        public SessionState(ulong sessionId)
        {
            this.SessionId = sessionId;
        }

        /// <summary>
        /// Gets the session identifier.
        /// </summary>
        /// <value>
        /// The session identifier.
        /// </value>
        public ulong SessionId { get; private set; }

        /// <summary>
        /// Gets or sets the created timestamp.
        /// </summary>
        /// <value>
        /// The created timestamp.
        /// </value>
        public DateTime Created { get; set; }
    }
}