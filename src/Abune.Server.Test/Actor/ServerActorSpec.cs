//-----------------------------------------------------------------------
// <copyright file="ServerActorSpec.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Test.TestKit;
    using Akka.Actor;
    using Akka.Actor.Setup;
    using Akka.Cluster;
    using Xunit;

    public class ServerActorSpec : AbuneSpec
    {
        private const string ISSUER = "self";
        private const string AUDIENCE = "abuneserver";
        private const string SIGNINGKEY = "!!!CHANGEME!!!";
        private const int DEFAULTAREACOUNT = 10;
        private const int DEFAULTOBJECTCOUNT = 10;
        private const int DEFAULTSESSIONCOUNT = 10;

        public ServerActorSpec() : 
            base(CLUSTERSHARDCONFIG)
        {
        }

        [Fact]
        public void ActorMustStartAndStop()
        {
            var testeeRef = Sys.ActorOf(Props.Create(() => new ServerActor(DEFAULTAREACOUNT, DEFAULTOBJECTCOUNT, DEFAULTSESSIONCOUNT, ISSUER, AUDIENCE, SIGNINGKEY)));
            Watch(testeeRef);
            Sys.Stop(testeeRef);
            ExpectTerminated(testeeRef);
        }

        [Fact]
        public void ActorMustReplyStateRequestWithJson()
        {
            var shardRegionObjectProbe = CreateTestProbe();
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new ServerActor(DEFAULTAREACOUNT, DEFAULTOBJECTCOUNT, DEFAULTSESSIONCOUNT, ISSUER, AUDIENCE, SIGNINGKEY)));
            testeeRef.Tell(new RequestStateCommand(replyToProbe));
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.NotEmpty(m.JsonState);
            });
        }
    }
}
