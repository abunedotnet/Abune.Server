//-----------------------------------------------------------------------
// <copyright file="ObjectState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using System;
    using System.Collections.Generic;
    using Abune.Shared.Command.Contract;
    using Abune.Shared.DataType;
    using Akka.Actor;

    /// <summary>Object actor state.</summary>
    public class ObjectState : ICanLocate
    {
        /// <summary>Initializes a new instance of the <see cref="ObjectState"/> class.</summary>
        public ObjectState()
        {
            this.ObjectStateValues = new Dictionary<uint, ObjectStateValue>();
            this.ActiveQuorumVotesByHash = new Dictionary<ulong, IActorRef>();
        }

        /// <summary>Gets or sets the object identifier.</summary>
        /// <value>The object identifier.</value>
        public ulong ObjectId { get; set; }

        /// <summary>Gets or sets the parent object identifier.</summary>
        /// <value>The parent object identifier.</value>
        public ulong ParentObjectId { get; set; }

        /// <summary>Gets or sets the type identifier.</summary>
        /// <value>The type identifier.</value>
        public uint TypeId { get; set; }

        /// <summary>Gets or sets the owner identifier.</summary>
        /// <value>The owner identifier.</value>
        public uint OwnerId { get; set; }

        /// <summary>Gets or sets the lock owner identifier.</summary>
        /// <value>The lock owner identifier.</value>
        public uint LockOwnerId { get; set; }

        /// <summary>Gets or sets the world position.</summary>
        /// <value>The world position.</value>
        public AVector3 WorldPosition { get; set; }

        /// <summary>Gets or sets the last world position.</summary>
        /// <value>The last world position.</value>
        public AVector3 LastWorldPosition { get; set; }

        /// <summary>Gets or sets the orientation.</summary>
        /// <value>The orientation.</value>
        public AQuaternion WorldOrientation { get; set; }

        /// <summary>Gets or sets the velocity.</summary>
        /// <value>The velocity.</value>
        public AVector3 Velocity { get; set; }

        /// <summary>Gets or sets the angular velocity.</summary>
        /// <value>The angular velocity.</value>
        public AVector3 AngularVelocity { get; set; }

        /// <summary>Gets or sets the time stamp last command.</summary>
        /// <value>The time stamp last command.</value>
        public DateTime TimeStampLastCommand { get; set; }

        /// <summary>Gets or sets the lock timeout.</summary>
        /// <value>The lock timeout.</value>
        public TimeSpan LockTimeout { get; set; }

        /// <summary>Gets the object state values.</summary>
        /// <value>The object state values.</value>
        public Dictionary<uint, ObjectStateValue> ObjectStateValues { get; private set; }

        /// <summary>
        /// Gets the active quorum votes by hash.
        /// </summary>
        /// <value>
        /// The active quorum votes by hash.
        /// </value>
        public Dictionary<ulong, IActorRef> ActiveQuorumVotesByHash { get; private set; }
    }
}
