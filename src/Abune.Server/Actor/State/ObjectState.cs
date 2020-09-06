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

    /// <summary>Object actor state.</summary>
    public class ObjectState : ICanLocate
    {
        /// <summary>Initializes a new instance of the <see cref="ObjectState"/> class.</summary>
        public ObjectState()
        {
            this.ObjectStateValues = new Dictionary<uint, ObjectStateValue>();
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

        /// <summary>Gets or sets the world position x.</summary>
        /// <value>The world position x.</value>
        public float WorldPositionX { get; set; }

        /// <summary>Gets or sets the world position y.</summary>
        /// <value>The world position y.</value>
        public float WorldPositionY { get; set; }

        /// <summary>Gets or sets the world position z.</summary>
        /// <value>The world position z.</value>
        public float WorldPositionZ { get; set; }

        /// <summary>Gets or sets the last world position x.</summary>
        /// <value>The last world position x.</value>
        public float LastWorldPositionX { get; set; }

        /// <summary>Gets or sets the last world position y.</summary>
        /// <value>The last world position y.</value>
        public float LastWorldPositionY { get; set; }

        /// <summary>Gets or sets the last world position z.</summary>
        /// <value>The last world position z.</value>
        public float LastWorldPositionZ { get; set; }

        /// <summary>Gets or sets the quaternion w.</summary>
        /// <value>The quaternion w.</value>
        public float QuaternionW { get; set; }

        /// <summary>Gets or sets the quaternion x.</summary>
        /// <value>The quaternion x.</value>
        public float QuaternionX { get; set; }

        /// <summary>Gets or sets the quaternion y.</summary>
        /// <value>The quaternion y.</value>
        public float QuaternionY { get; set; }

        /// <summary>Gets or sets the quaternion z.</summary>
        /// <value>The quaternion z.</value>
        public float QuaternionZ { get; set; }

        /// <summary>Gets or sets the velocity x.</summary>
        /// <value>The velocity x.</value>
        public float VelocityX { get; set; }

        /// <summary>Gets or sets the velocity y.</summary>
        /// <value>The velocity y.</value>
        public float VelocityY { get; set; }

        /// <summary>Gets or sets the velocity z.</summary>
        /// <value>The velocity z.</value>
        public float VelocityZ { get; set; }

        /// <summary>Gets or sets the angular velocity x.</summary>
        /// <value>The angular velocity x.</value>
        public float AngularVelocityX { get; set; }

        /// <summary>Gets or sets the angular velocity y.</summary>
        /// <value>The angular velocity y.</value>
        public float AngularVelocityY { get; set; }

        /// <summary>Gets or sets the angular velocity z.</summary>
        /// <value>The angular velocity z.</value>
        public float AngularVelocityZ { get; set; }

        /// <summary>Gets or sets the time stamp last command.</summary>
        /// <value>The time stamp last command.</value>
        public DateTime TimeStampLastCommand { get; set; }

        /// <summary>Gets or sets the lock timeout.</summary>
        /// <value>The lock timeout.</value>
        public TimeSpan LockTimeout { get; set; }

        /// <summary>Gets the object state values.</summary>
        /// <value>The object state values.</value>
        public Dictionary<uint, ObjectStateValue> ObjectStateValues { get; private set; }
    }
}
