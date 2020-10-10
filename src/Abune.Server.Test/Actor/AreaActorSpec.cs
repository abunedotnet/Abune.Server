namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Sharding;
    using Abune.Server.Test.TestKit;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Xunit;

    public class AreaActorSpec : AbuneSpec
    {
        private const string DEFAULTNAME = "500500500";

        [Fact]
        public void ActorMustStartAndStopOutsideShardRegion()
        {
            var shardRegionObjectProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new AreaActor(shardRegionObjectProbe)), DEFAULTNAME);
            Watch(testeeRef);
            Sys.Stop(testeeRef);
            ExpectTerminated(testeeRef);
        }

        [Fact]
        public void ActorMustReplyStateRequestWithJson()
        {
            var shardRegionObjectProbe = CreateTestProbe();
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new AreaActor(shardRegionObjectProbe)), DEFAULTNAME);
            testeeRef.Tell(new RequestStateCommand(replyToProbe));
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.True(m.JsonState.Contains(DEFAULTNAME, System.StringComparison.InvariantCulture));
            });
        }
    }
}
