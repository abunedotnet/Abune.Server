//-----------------------------------------------------------------------
// <copyright file="SessionRegionMessageExtractor.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Sharding
{
    using System.Globalization;
    using Abune.Server.Command;
    using Abune.Shared.Command.Contract;
    using Abune.Shared.Message.Area;
    using Abune.Shared.Message.Contract;
    using Akka.Cluster.Sharding;

    /// <summary>Message extractor for area shard regions.</summary>
    public sealed class SessionRegionMessageExtractor : HashCodeMessageExtractor
    {
        /// <summary>Initializes a new instance of the <see cref="SessionRegionMessageExtractor"/> class.</summary>
        /// <param name="numberOfShards">The number of shards.</param>
        public SessionRegionMessageExtractor(int numberOfShards)
            : base(numberOfShards)
        {
        }

        /// <summary>Extracts the identifier of the entity.</summary>
        /// <param name="message">Message to process.</param>
        /// <returns>Entity identifier.</returns>
        public override string EntityId(object message)
        {
            if (message is AreaCommandEnvelope)
            {
                return (message as AreaCommandEnvelope)?.ToAreaId.ToString(CultureInfo.InvariantCulture);
            }
            else if (message is ObjectEnterAreaCommand)
            {
                return (message as ObjectEnterAreaCommand)?.AreaId.ToString(CultureInfo.InvariantCulture);
            }
            else if (message is ObjectLeaveAreaCommand)
            {
                return (message as ObjectLeaveAreaCommand)?.AreaId.ToString(CultureInfo.InvariantCulture);
            }
            else if (message is IAreaCommand)
            {
                return (message as IAreaCommand)?.AreaId.ToString(CultureInfo.InvariantCulture);
            }
            else if (message is ICanRouteToArea)
            {
                return (message as ICanRouteToArea)?.ToAreaId.ToString(CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        /// <summary>Extracts the entity message.</summary>
        /// <param name="message">Message to process.</param>
        /// <returns>Message object.</returns>
        public override object EntityMessage(object message) => message;
    }
}
