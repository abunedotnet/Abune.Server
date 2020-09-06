//-----------------------------------------------------------------------
// <copyright file="ObjectStateValue.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    /// <summary>Value of object state.</summary>
    public class ObjectStateValue
    {
        /// <summary>Gets or sets the identifier.</summary>
        /// <value>The identifier.</value>
        public uint Id { get; set; }

#pragma warning disable CA1819 // code efficiency
        /// <summary>Gets or sets the data.</summary>
        /// <value>The data.</value>
        public byte[] Data { get; set; }
    }
}
