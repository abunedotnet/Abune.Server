//-----------------------------------------------------------------------
// <copyright file="SessionActorSpec.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor.Session;
    using Abune.Server.Test.TestKit;
    using Akka.Actor;
    using Xunit;

    public class SessionActorSpec : AbuneSpec
    {

        public SessionActorSpec()
        {
            
        }

        [Fact]
        public void ActorMustSafeName()
        {
            //setup

            //execute
            var testeeRef = Sys.ActorOf(Props.Create(() => new SessionActor()), "SESSION-1234");
            Watch(testeeRef);

            //verify
            Sys.Stop(testeeRef);
            ExpectTerminated(testeeRef);
        }
    }
}
