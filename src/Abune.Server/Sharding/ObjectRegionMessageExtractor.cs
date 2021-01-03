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

        /// <summary>Extracts the identifier of the entity.</summary>
        /// <param name="message">Message to process.</param>
        /// <returns>Entity identifier.</returns>
        public override string EntityId(object message)
        {
            if (message is ObjectCommandEnvelope)
            {
                return (message as ObjectCommandEnvelope)?.ToObjectId.ToString(CultureInfo.InvariantCulture);
            }
            else if (message is NotifySubscribeObjectExistenceCommand)
            {
                return (message as NotifySubscribeObjectExistenceCommand)?.ObjectId.ToString(CultureInfo.InvariantCulture);
            }
            else if (message is NotifyUnsubscribeObjectExistenceCommand)
            {
                return (message as NotifyUnsubscribeObjectExistenceCommand)?.ObjectId.ToString(CultureInfo.InvariantCulture);
            }
            else if (message is ICanRouteToObject)
            {
                return (message as ICanRouteToObject)?.ToObjectId.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        /// <summary>Extracts the entity message.</summary>
        /// <param name="message">Message to process.</param>
        /// <returns>Message object.</returns>
        public override object EntityMessage(object message) => message;
    }
}
