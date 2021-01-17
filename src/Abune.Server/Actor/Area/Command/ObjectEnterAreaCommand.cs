//-----------------------------------------------------------------------
// <copyright file="ObjectEnterAreaCommand.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Command
{
    using Abune.Server.Actor.Events;
    using Abune.Shared.Message.Contract;

    /// <summary>Command for object entering area.</summary>
    public class ObjectEnterAreaCommand : IInternalCommand, ICanRouteToArea
    {
        /// <summary>Gets or sets the object identifier.</summary>
        /// <value>The object identifier.</value>
        public ulong ObjectId { get; set; }

        /// <summary>Gets or sets the area identifier.</summary>
        /// <value>The area identifier.</value>
        public ulong AreaId { get; set; }

        /// <summary>Gets the area identifier.</summary>
        /// <value>Area identifier.</value>
        public ulong ToAreaId => this.AreaId;
    }
}