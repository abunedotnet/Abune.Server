//-----------------------------------------------------------------------
// <copyright file="RespondStateCommand.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Actor.Command
{
    /// <summary>
    /// Command for responding actor state.
    /// </summary>
    public class RespondStateCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RespondStateCommand"/> class.
        /// </summary>
        /// <param name="stateAsJson">Json representation of the state.</param>
        public RespondStateCommand(string stateAsJson)
        {
            this.JsonState = stateAsJson;
        }

        /// <summary>
        /// Gets the state of the actor.
        /// </summary>
        /// <value>
        /// Json representation of the state.
        /// </value>
        public string JsonState { get; private set; }
    }
}
