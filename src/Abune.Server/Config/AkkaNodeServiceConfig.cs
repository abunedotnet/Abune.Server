//-----------------------------------------------------------------------
// <copyright file="AkkaNodeServiceConfig.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Config
{
    using System.IO;
    using Akka.Configuration;

    /// <summary>Configuration schema for akka node service.</summary>
    public class AkkaNodeServiceConfig
    {
        private string configLocation = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="AkkaNodeServiceConfig"/> class.</summary>
        public AkkaNodeServiceConfig()
        {
            this.Metrics = new MetricsConfig();
        }

        /// <summary>Gets or sets the name of the system.</summary>
        /// <value>The name of the system.</value>
        public string SystemName { get; set; }

        /// <summary>Gets or sets the shard count area.</summary>
        /// <value>The shard count area.</value>
        public int ShardCountArea { get; set; } = 10;

        /// <summary>Gets or sets the shard count object.</summary>
        /// <value>The shard count object.</value>
        public int ShardCountObject { get; set; } = 100;

        /// <summary>Gets or sets the server udp port.</summary>
        /// <value>The server udp port.</value>
        public int ServerPort { get; set; } = 7777;

        /// <summary>Gets or sets the configuration location.</summary>
        /// <value>The configuration location.</value>
        public string ConfigLocation
        {
            get
            {
                return this.configLocation;
            }

            set
            {
                this.configLocation = value;
                this.AkkaConfiguration = ReadConfiguration(this.configLocation);
            }
        }

        /// <summary>Gets or sets the metrics.</summary>
        /// <value>The metrics.</value>
        internal MetricsConfig Metrics { get; set; }

        /// <summary>Gets the akka configuration.</summary>
        /// <value>The akka configuration.</value>
        internal Config AkkaConfiguration { get; private set; }

        private static Config ReadConfiguration(string fileLocation)
        {
            string configString = File.ReadAllText(fileLocation);
            return ConfigurationFactory.ParseString(configString);
        }
    }
}
