//-----------------------------------------------------------------------
// <copyright file="ClientTwinState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using System;
    using System.Net;

    /// <summary>Client twin actor state.</summary>
    public class ClientTwinState
    {
        /// <summary>
        /// Gets or sets the client endpoint.
        /// </summary>
        /// <value>The endpoint.</value>
        public IPEndPoint Endpoint { get; set; }

        /// <summary>Gets or sets the client identifier.</summary>
        /// <value>The client identifier.</value>
        public uint ClientId { get; set; }

        /// <summary>Gets or sets the last keep alive UTC.</summary>
        /// <value>The last keep alive UTC.</value>
        public DateTime LastKeepAliveUtc { get; set; }

        /// <summary>Gets or sets the latency.</summary>
        /// <value>The latency.</value>
        public TimeSpan Latency { get; set; }
    }
}
