namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Sharding;
    using Abune.Server.Test.TestKit;
    using Akka.Actor;
    using Akka.Cluster;
    using Akka.Cluster.Metrics;
    using Akka.Cluster.Sharding;
    using System;
    using Xunit;

    public class MetricsActorSpec : AbuneSpec
    {
        private readonly TimeSpan defaultInterval = TimeSpan.FromMilliseconds(100);
        private readonly Cluster cluster;
        private readonly ClusterMetrics clusterMetrics;

        public MetricsActorSpec() :
            base(CLUSTERSHARDCONFIG)
        {
            cluster = Cluster.Get(Sys);
            clusterMetrics = ClusterMetrics.Get(Sys);
            ClusterSharding.Get(Sys).Start(typeName: ShardRegions.OBJECTREGION, entityProps: Props.Create(() => new ObjectActor(null)), settings: ClusterShardingSettings.Create(Sys), messageExtractor: new ObjectRegionMessageExtractor(10));
            ClusterSharding.Get(Sys).Start(typeName: ShardRegions.AREAREGION, entityProps: Props.Create(() => new ObjectActor(null)), settings: ClusterShardingSettings.Create(Sys), messageExtractor: new AreaRegionMessageExtractor(10));
        }

        [Fact(Skip = "Akka Cluster Sharding is not ready yet")]
        public void ActorMustStartAndStop()
        {
            var custerExtensionMock = new Moq.Mock<ClusterMetrics>();
            var testeeRef = Sys.ActorOf(Props.Create(() => new MetricsActor(defaultInterval, cluster, clusterMetrics)));
            Watch(testeeRef);
            Sys.Stop(testeeRef);
            ExpectTerminated(testeeRef);
        }

        [Fact(Skip="Akka Cluster Sharding is not ready yet")]
        public void ActorMustReplyStateRequestWithJson()
        {
            var shardRegionObjectProbe = CreateTestProbe();
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new MetricsActor(defaultInterval, cluster, clusterMetrics)));
            testeeRef.Tell(new RequestStateCommand(replyToProbe));
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.NotEmpty(m.JsonState);
            });
        }
    }
}
