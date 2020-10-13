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

        /// <summary>Gets or sets the client version.</summary>
        /// <value>The client version.</value>
        public string ClientVersion { get; set; }

        /// <summary>Gets or sets the last keep alive UTC.</summary>
        /// <value>The last keep alive UTC.</value>
        public DateTime LastKeepAliveUtc { get; set; }

        /// <summary>Gets or sets the latency.</summary>
        /// <value>The latency.</value>
        public TimeSpan Latency { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ClientTwinState"/> is authenticated.
        /// </summary>
        /// <value>
        ///   <c>true</c> if authenticated; otherwise, <c>false</c>.
        /// </value>
        public bool Authenticated { get; set; }

        /// <summary>
        /// Gets or sets the authentication challenge.
        /// </summary>
        /// <value>
        /// The authentication challenge.
        /// </value>
        public string AuthenticationChallenge { get; set; }
    }
}
