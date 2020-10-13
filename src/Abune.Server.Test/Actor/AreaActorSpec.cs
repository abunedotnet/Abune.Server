namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Sharding;
    using Abune.Server.Test.TestKit;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Moq;
    using Xunit;

    public class AreaActorSpec : AbuneSpec
    {
        private const string DEFAULTNAME = "500500500";

        public AreaActorSpec()
        {
            
        }

        [Fact]
        public void ActorMustStartAndStopOutsideShardRegion()
        {
            //setup
            var shardRegionObjectProbe = CreateTestProbe();
            var shardRegionResolver = new Mock<IShardRegionResolver>();
            shardRegionResolver.Setup(m => m.GetShardRegion(ShardRegions.OBJECTREGION)).Returns(shardRegionObjectProbe);

            //execute
            var testeeRef = Sys.ActorOf(Props.Create(() => new AreaActor(shardRegionResolver.Object)), DEFAULTNAME);
            Watch(testeeRef);

            //verify
            Sys.Stop(testeeRef);
            ExpectTerminated(testeeRef);
        }

        [Fact]
        public void ActorMustReplyStateRequestWithJson()
        {
            //setup
            var shardRegionObjectProbe = CreateTestProbe();
            var shardRegionResolver = new Mock<IShardRegionResolver>();
            shardRegionResolver.Setup(m => m.GetShardRegion(ShardRegions.OBJECTREGION)).Returns(shardRegionObjectProbe);
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new AreaActor(shardRegionResolver.Object)), DEFAULTNAME);
            
            //execute
            testeeRef.Tell(new RequestStateCommand(replyToProbe));

            //verify
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.True(m.JsonState.Contains(DEFAULTNAME, System.StringComparison.InvariantCulture));
            });
        }
    }
}
