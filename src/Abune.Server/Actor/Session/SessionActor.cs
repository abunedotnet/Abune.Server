//-----------------------------------------------------------------------
// <copyright file="SessionActor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Session
{
    using System;
    using Abune.Server.Actor.Command;
    using Abune.Server.Actor.Events;
    using Abune.Server.Actor.State;
    using Akka.Actor;
    using Akka.Event;
    using Akka.Persistence;
    using Newtonsoft.Json;

    /// <summary>Actor representing areas.</summary>
    public class SessionActor : PersistentActor
    {
        private readonly ILoggingAdapter log = Logging.GetLogger(Context);
        private SessionState state;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionActor"/> class.
        /// </summary>
        public SessionActor()
        {
            ulong sessionId = this.ExtractSessionId();
            this.state = new SessionState(sessionId);
            this.state.Created = DateTime.UtcNow;
        }

        /// <summary>Gets id of the persistent entity for which messages should be replayed.</summary>
        public override string PersistenceId => $"SESSION-{this.state.SessionId}";

        /// <summary>
        /// Command handler. Typically validates commands against current state - possibly by communicating with other actors.
        /// On successful validation, one or more events are derived from command and persisted.
        /// </summary>
        /// <param name="message">Message received.</param>
        /// <returns>true if message is handled.</returns>
        protected override bool ReceiveCommand(object message)
        {
            if (message is SaveSnapshotSuccess)
            {
                this.log.Debug("SaveSnapshotSuccess");
                return true;
            }

            if (message is SaveSnapshotFailure)
            {
                this.log.Error("SaveSnapshotFailure");
                return true;
            }

            return true;
        }

        /// <summary>
        /// Recovery handler that receives persistent events during recovery.
        /// </summary>
        /// <param name="message">Message offered.</param>
        /// <returns>true if message is handled.</returns>
        protected override bool ReceiveRecover(object message)
        {
            if (message is SnapshotOffer offeredSnapshot)
            {
                this.state = (SessionState)offeredSnapshot.Snapshot;
            }
            else if (message is RecoveryCompleted)
            {
                this.log.Debug($"{this.Self.Path} recovery completed");
            }
            else if (message is IInternalCommand)
            {
                this.UpdateState((IInternalCommand)message);
            }

            return true;
        }

        private void RespondState(IActorRef replyTo)
        {
            string json = JsonConvert.SerializeObject(this.state);
            replyTo.Tell(new RespondStateCommand(json));
        }

        private ulong ExtractSessionId()
        {
            return ulong.Parse(this.Self.Path.Name, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void InternalPersist<TEvent>(TEvent @event, Action<TEvent> handler)
        {
            this.Persist(@event, handler);
        }

        private void UpdateStatePersistent(IInternalCommand command)
        {
            this.InternalPersist(command, this.UpdateState);
        }

        private void UpdateState(IInternalCommand command)
        {
        }
    }
}
