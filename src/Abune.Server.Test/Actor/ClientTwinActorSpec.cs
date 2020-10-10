namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Test.TestKit;
    using Akka.Actor;
    using System.Net;
    using Xunit;

    public class ClientTwinActorSpec : AbuneSpec
    {
        private readonly IActorRef defaultSocketProbe;
        private readonly IActorRef defaultShardRegionArea;
        private readonly IActorRef defaultObjectRegionArea;

        IPEndPoint defaultEndpoint = new IPEndPoint(0, 0);

        public ClientTwinActorSpec()
        {
            defaultSocketProbe = CreateTestProbe();
            defaultShardRegionArea = CreateTestProbe();
            defaultObjectRegionArea = CreateTestProbe();
        }

        [Fact]
        public void ActorMustStartAndStop()
        {
            var testeeRef = Sys.ActorOf(Props.Create(() => new ClientTwinActor(defaultSocketProbe, defaultEndpoint, defaultShardRegionArea, defaultObjectRegionArea)));
            Watch(testeeRef);
            Sys.Stop(testeeRef);
            ExpectTerminated(testeeRef);
        }

        [Fact]
        public void ActorMustReplyStateRequestWithJson()
        {
            var shardRegionObjectProbe = CreateTestProbe();
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new ClientTwinActor(defaultSocketProbe, defaultEndpoint, defaultShardRegionArea, defaultObjectRegionArea)));
            testeeRef.Tell(new RequestStateCommand(replyToProbe));
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.NotEmpty(m.JsonState);
            });
        }
    }
}
