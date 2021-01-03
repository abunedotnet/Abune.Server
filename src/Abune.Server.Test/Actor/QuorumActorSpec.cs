


namespace Abune.Server.Test.Actor
{
    using System;
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Sharding;
    using Abune.Server.Test.TestKit;
    using Abune.Shared.Command;
    using Abune.Shared.DataType;
    using Abune.Shared.Message;
    using Abune.Shared.Message.Contract;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using Moq;
    using Xunit;

    public class QuorumActorSpec : AbuneSpec
    {
        private const string DefaultActorName = "QUORUM-12345";

        [Fact]
        public void ActorMustReplyMessageOnSuccessfulQuorum()
        {
            //setup
            const int VOTERCOUNT = 15;
            const ulong QUORUMHASH = 12345U;
            TimeSpan timeout = TimeSpan.FromSeconds(5);
            var replyToProbe = CreateTestProbe();

            //execute
            var testeeRef = Sys.ActorOf(Props.Create(() => new QuorumActor(QUORUMHASH, VOTERCOUNT, timeout)), name: DefaultActorName);
            Watch(testeeRef);
            for (uint senderId = 0; senderId < (VOTERCOUNT / 2) + 1; senderId++)
            {
                testeeRef.Tell(new ObjectCommandEnvelope(senderId, new ObjectValueUpdateCommand(objectId: 0, valueId: 0, new byte[] {}, CommandFlags.QuorumRequest, QUORUMHASH ), 0), replyToProbe);
            }
            
            //verify
            replyToProbe.ExpectMsg<ObjectCommandEnvelope>(m =>
            {
                Assert.Equal((uint)0, m.SenderId);
            });
            replyToProbe.ExpectNoMsg(timeout - TimeSpan.FromSeconds(1));
            ExpectTerminated(testeeRef, timeout + TimeSpan.FromSeconds(2));
        }

        [Fact]
        public void ActorMustNotReplyMessageOnInvalidQuorum()
        {
            //setup
            const int VOTERCOUNT = 15;
            const ulong QUORUMHASH = 12345U;
            TimeSpan timeout = TimeSpan.FromSeconds(5);
            var replyToProbe = CreateTestProbe();

            //execute
            var testeeRef = Sys.ActorOf(Props.Create(() => new QuorumActor(QUORUMHASH, VOTERCOUNT, timeout)), name: DefaultActorName);
            Watch(testeeRef);
            for (uint senderId = 0; senderId < (VOTERCOUNT / 2) - 1; senderId++)
            {
                testeeRef.Tell(new ObjectCommandEnvelope(senderId, new ObjectValueUpdateCommand(objectId: 0, valueId: 0, new byte[] {}, CommandFlags.QuorumRequest, QUORUMHASH ), 0), replyToProbe);
            }

            //verify
            replyToProbe.ExpectNoMsg(timeout - TimeSpan.FromSeconds(1));
            ExpectTerminated(testeeRef, timeout + TimeSpan.FromSeconds(2));
        }
    }
}
