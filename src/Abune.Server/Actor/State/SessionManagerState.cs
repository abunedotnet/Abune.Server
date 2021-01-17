//-----------------------------------------------------------------------
// <copyright file="SessionManagerState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using System.Collections.Generic;

    #pragma warning disable CA1724
    /// <summary>Area actor state.</summary>
    public class SessionManagerState
    {
        /// <summary>Initializes a new instance of the <see cref="SessionManagerState"/> class.</summary>
        public SessionManagerState()
        {
            this.ActiveSessions = new Dictionary<string, ulong>();
        }

        /// <summary>
        /// Gets the active sessions.
        /// </summary>
        /// <value>
        /// The active sessions.
        /// </value>
        public Dictionary<string, ulong> ActiveSessions { get; private set; }
    }
}