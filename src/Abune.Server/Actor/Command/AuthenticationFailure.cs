//-----------------------------------------------------------------------
// <copyright file="AuthenticationFailure.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Command
{
    using Abune.Server.Actor.Events;

    /// <summary>
    /// Request for authentication.
    /// </summary>
    public class AuthenticationFailure
    {
        /// <summary>
        /// Gets or sets the error.
        /// </summary>
        /// <value>
        /// The error.
        /// </value>
        public string Error { get; set; }
    }
}
