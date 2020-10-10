
namespace Abune.Server.Test.TestKit
{
    using Akka.Actor.Setup;
    using Akka.Cluster.Sharding;
    using Akka.Configuration;
    using Akka.TestKit;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;

    public class AbuneSpec : Akka.TestKit.Xunit2.TestKit
    {
        /// <summary>
        /// Test config for cluster sharding
        /// </summary>
        public const string CLUSTERSHARDCONFIG = "Abune.Server.Test.Config.akkaTest.ClusterSharding.hocon";

        /// <summary>
        /// Initializes a new instance of the <see cref="AbuneSpec"/> class.
        /// </summary>
        public AbuneSpec()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbuneSpec"/> class.
        /// </summary>
        /// <param name="setup">The setup.</param>
        public AbuneSpec(string resourceName) : 
            base(ConfigurationFactory.FromResource<AbuneSpec>(resourceName))
        {
        }
    }
}
