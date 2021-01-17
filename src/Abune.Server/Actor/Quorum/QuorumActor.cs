//-----------------------------------------------------------------------
// <copyright file="QuorumActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Quorum
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Abune.Server.Actor.Command;
    using Abune.Server.Actor.State;
    using Abune.Shared.Message;
    using Abune.Shared.Message.Contract;
    using Akka.Actor;
    using Akka.Event;

    /// <summary>
    /// Actor to verify Quorum Operation.
    /// </summary>
    public class QuorumActor : ReceiveActor
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);

        private QuorumState state = new QuorumState();

        /// <summary>
        /// Initializes a new instance of the <see cref="QuorumActor"/> class.
        /// </summary>
        /// <param name="hash">The quorum hash.</param>
        /// <param name="voterCount">The voter count.</param>
        /// <param name="votingTimeout">The voting time to live.</param>
        public QuorumActor(ulong hash, int voterCount, TimeSpan votingTimeout)
        {
            this.state.Hash = hash;
            this.state.VoterCount = voterCount;
            this.state.VotingTimeout = votingTimeout;
            this.Receive<ICanQuorumVote>(v =>
            {
                this.ProcessQuorumMessage(v);
            });
            this.Receive<ReceiveTimeout>(c =>
            {
                this.ProcessReceiveTimeout();
            });
            this.SetReceiveTimeout(this.state.VotingTimeout);
        }

        private void ProcessQuorumMessage(ICanQuorumVote quorumMessage)
        {
            if (quorumMessage.QuorumHash != this.state.Hash)
            {
                this.log.Error($"Invalid quorum hash. Expected {this.state.Hash} Actual: {quorumMessage.QuorumHash}");
                return;
            }

            if (this.state.FirstQuorumMessage == null)
            {
                this.state.QuorumStartedTimeStamp = DateTime.Now;
                this.state.QuorumFinishedTimeStamp = DateTime.MaxValue;
                this.state.FirstQuorumMessage = quorumMessage;
            }

            bool isQuorumAlreadyReached = this.IsQuorumReached();
            if (this.state.Voters.ContainsKey(quorumMessage.QuorumVoterId))
            {
                this.log.Warning($"Ignoring vote. Voter [{quorumMessage.QuorumVoterId}] already voted.");
                return;
            }

            this.state.Voters.Add(quorumMessage.QuorumVoterId, 0);
            if (!isQuorumAlreadyReached && this.IsQuorumReached())
            {
                // only send message once
                this.state.QuorumFinishedTimeStamp = DateTime.Now;
                this.Sender.Tell(this.state.FirstQuorumMessage, this.Self);
            }
        }

        private void ProcessReceiveTimeout()
        {
            if (this.state.QuorumFinishedTimeStamp != DateTime.MaxValue)
            {
                var duration = this.state.QuorumFinishedTimeStamp - this.state.QuorumStartedTimeStamp;
                this.log.Debug($"Quorum [{this.state.Hash}] finished with {this.state.Voters.Count}/{this.state.VoterCount} voting within {duration}");
            }
            else
            {
                this.log.Warning($"Quorum [{this.state.Hash}] timed out with {this.state.Voters.Count}/{this.state.VoterCount} voting after {this.state.VotingTimeout}");
            }

            this.Self.Tell(PoisonPill.Instance);
        }

        private bool IsQuorumReached()
        {
            return this.state.Voters.Count > (int)(this.state.VoterCount / 2);
        }
    }
}
