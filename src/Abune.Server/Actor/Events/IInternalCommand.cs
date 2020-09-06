//-----------------------------------------------------------------------
// <copyright file="IInternalCommand.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Events
{
    /// <summary>
    /// Interface for internal commands.
    /// </summary>
    public interface IInternalCommand
    {
        /// <summary>Gets the object identifier.</summary>
        /// <value>The object identifier.</value>
        ulong ObjectId { get; }
    }
}
