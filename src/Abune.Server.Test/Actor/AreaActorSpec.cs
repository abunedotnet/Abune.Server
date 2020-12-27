using Abune.Shared.Command;
using Abune.Shared.DataType;
using Abune.Shared.Message;

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
        private const ulong DEFAULTAREAID = 500500500;
        private const uint DEFAULTCLIENTID = 999; 

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
            var testeeRef = Sys.ActorOf(Props.Create(() => new AreaActor(shardRegionResolver.Object)), name: DEFAULTAREAID.ToString());
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
            var testeeRef = Sys.ActorOf(Props.Create(() => new AreaActor(shardRegionResolver.Object)), name: DEFAULTAREAID.ToString());
            
            //execute
            testeeRef.Tell(new RequestStateCommand(replyToProbe));

            //verify
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.True(m.JsonState.Contains(DEFAULTAREAID.ToString(), System.StringComparison.InvariantCulture));
            });
        }

        
        [Fact]
        public void ActorMustForwardEvents()
        {
            //setup
            var shardRegionObjectProbe = CreateTestProbe();
            var shardRegionResolver = new Mock<IShardRegionResolver>();
            shardRegionResolver.Setup(m => m.GetShardRegion(ShardRegions.OBJECTREGION)).Returns(shardRegionObjectProbe);
            var subscriberProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new AreaActor(shardRegionResolver.Object)), DEFAULTAREAID.ToString());
            testeeRef.Tell(new AreaCommandEnvelope(DEFAULTAREAID, new ObjectCommandEnvelope(0, new SubscribeAreaCommand(DEFAULTCLIENTID, DEFAULTAREAID, 0), 0)), subscriberProbe);
            
            //execute
            testeeRef.Tell(new AreaCommandEnvelope(DEFAULTAREAID, new ObjectCommandEnvelope(0, new EventLineCommand(0, AVector3.Zero, AVector3.Zero, 0UL, new byte[] {}), 0)));

            //verify
            subscriberProbe.ExpectMsg<ObjectCommandResponseEnvelope>(m =>
            {
                Assert.Equal(CommandType.EventLine, m.Message.Command.Type);
            });
        }
    }
}
