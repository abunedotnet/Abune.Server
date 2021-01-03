//-----------------------------------------------------------------------
// <copyright file="QuorumState.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.State
{
    using System;
    using System.Collections.Generic;
    using Abune.Shared.Message.Contract;

    /// <summary>Object actor state.</summary>
    public class QuorumState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuorumState"/> class.
        /// </summary>
        public QuorumState()
        {
            this.Voters = new Dictionary<uint, uint>();
        }

        /// <summary>
        /// Gets or sets the quorum hash.
        /// </summary>
        /// <value>
        /// The quorum hash.
        /// </value>
        public ulong Hash { get; set; }

        /// <summary>
        /// Gets or sets the quorum voter count.
        /// </summary>
        /// <value>
        /// The voter quorum count.
        /// </value>
        public int VoterCount { get; set; }

        /// <summary>
        /// Gets or sets the voting started time stamp.
        /// </summary>
        /// <value>
        /// The quorum voting started.
        /// </value>
        public DateTime VotingStarted { get; set; }

        /// <summary>
        /// Gets or sets the time to live.
        /// </summary>
        /// <value>
        /// The time to live.
        /// </value>
        public TimeSpan VotingTimeout { get; set; }

        /// <summary>
        /// Gets or sets the first quorum message.
        /// </summary>
        /// <value>
        /// The first quorum message.
        /// </value>
        public ICanQuorumVote FirstQuorumMessage { get; set; }

        /// <summary>
        /// Gets the voters.
        /// </summary>
        /// <value>
        /// The voters.
        /// </value>
        public Dictionary<uint, uint> Voters { get; private set; }

        /// <summary>
        /// Gets or sets the quorum started time stamp.
        /// </summary>
        /// <value>
        /// The quorum started time stamp.
        /// </value>
        public DateTime QuorumStartedTimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the quorum finished time stamp.
        /// </summary>
        /// <value>
        /// The quorum finished time stamp.
        /// </value>
        public DateTime QuorumFinishedTimeStamp { get; set; }
    }
}
