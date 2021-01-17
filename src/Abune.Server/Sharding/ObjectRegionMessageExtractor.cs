//-----------------------------------------------------------------------
// <copyright file="ObjectRegionMessageExtractor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Sharding
{
    using System.Globalization;
    using Abune.Server.Actor.Command;
    using Abune.Shared.Message;
    using Abune.Shared.Message.Contract;
    using Abune.Shared.Message.Object;
    using Akka.Cluster.Sharding;

    /// <summary>Message extractor for object shard regions.</summary>
    public sealed class ObjectRegionMessageExtractor : HashCodeMessageExtractor
    {
        /// <summary>Initializes a new instance of the <see cref="ObjectRegionMessageExtractor"/> class.</summary>
        /// <param name="numberOfShards">The number of shards.</param>
        public ObjectRegionMessageExtractor(int numberOfShards)
            : base(numberOfShards)
        {
        }

        /// <summary>
        /// Builds the entity identifier.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="objectId">The object identifier.</param>
        /// <returns>The entity id.</returns>
        public static string BuildEntityId(ulong sessionId, ulong objectId)
        {
            return $"{sessionId}-{objectId}";
        }

        /// <summary>Extracts the identifier of the entity.</summary>
        /// <param name="message">Message to process.</param>
        /// <returns>Entity identifier.</returns>
        public override string EntityId(object message)
        {
            if (message is ICanRouteToObject canRouteToObject)
            {
                return BuildEntityId(canRouteToObject.ToSessionId, canRouteToObject.ToObjectId);
            }

            return string.Empty;
        }

        /// <summary>Extracts the entity message.</summary>
        /// <param name="message">Message to process.</param>
        /// <returns>Message object.</returns>
        public override object EntityMessage(object message) => message;
    }
}
