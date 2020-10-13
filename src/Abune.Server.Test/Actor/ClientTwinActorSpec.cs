namespace Abune.Server.Test.Actor
{
    using System.Net;
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Test.TestKit;
    using Abune.Shared.Message;
    using Abune.Shared.Protocol;
    using Akka.Actor;
    using Akka.IO;
    using Akka.TestKit;
    using Xunit;

    public class ClientTwinActorSpec : AbuneSpec
    {
        private readonly IActorRef defaultSocketProbe;
        private readonly IActorRef defaultAuthenticationProbe;
        private readonly IActorRef defaultShardRegionArea;
        private readonly IActorRef defaultObjectRegionArea;

        IPEndPoint defaultEndpoint = new IPEndPoint(0, 0);

        public ClientTwinActorSpec()
        {
            defaultSocketProbe = CreateTestProbe();
            defaultAuthenticationProbe = CreateTestProbe();
            defaultShardRegionArea = CreateTestProbe();
            defaultObjectRegionArea = CreateTestProbe();
        }

        [Fact]
        public void ActorMustStartAndStop()
        {
            //setup
            var testeeRef = Sys.ActorOf(Props.Create(() => new ClientTwinActor(defaultSocketProbe, defaultAuthenticationProbe, defaultEndpoint, defaultShardRegionArea, defaultObjectRegionArea)));
            Watch(testeeRef);

            //execute
            Sys.Stop(testeeRef);

            //verify
            ExpectTerminated(testeeRef);
        }

        [Fact]
        public void ActorMustReplyStateRequestWithJson()
        {
            //setup
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new ClientTwinActor(defaultSocketProbe, defaultAuthenticationProbe, defaultEndpoint, defaultShardRegionArea, defaultObjectRegionArea)));

            //execute
            testeeRef.Tell(new RequestStateCommand(replyToProbe));

            //verify
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.NotEmpty(m.JsonState);
            });
        }

        [Fact]
        public void ActorMustNotAcceptMessagesWithoutAuthentication()
        {
            //setup
            var shardRegionObjectProbe = CreateTestProbe();
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new ClientTwinActor(defaultSocketProbe, defaultAuthenticationProbe, defaultEndpoint, shardRegionObjectProbe, defaultObjectRegionArea)));
            var clientPingMessage = new ClientPingMessage();

            //execute
            testeeRef.Tell(new UdpTransferFrame(FrameType.ClientPing, clientPingMessage.Serialize()));

            //verify
            shardRegionObjectProbe.ExpectNoMsg();
        }        

        [Fact]
        public void ActorMustAcceptMessagesAfterAuthentication()
        {
            //setup
            var authenticationProbe = CreateTestProbe();
            var clientSocketProbe = CreateTestProbe();
            var testeeRef = CreateAuthenticatedActor(authenticationProbe, clientSocketProbe);
            var clientPingMessage = new ClientPingMessage();

            //execute - authentication actor response
            testeeRef.Tell(new UdpTransferFrame(FrameType.ClientPing, clientPingMessage.Serialize()));

            //verify
            clientSocketProbe.ExpectMsg<Udp.Send>(s =>
            {
                var msg = new UdpTransferFrame(s.Payload.ToArray());
                Assert.Equal(FrameType.ServerPong, msg.Type);
            });
        }

        private IActorRef CreateAuthenticatedActor(TestProbe authenticationProbe, TestProbe clientSocketProbe)
        {
            //setup
            const uint CLIENTID = 1234567890;
            const string TOKEN = "TOKEN";
            string EXPECTEDRESPONSE = $"HELLO {CLIENTID}";
            
            var testeeRef = Sys.ActorOf(Props.Create(() => new ClientTwinActor(clientSocketProbe, authenticationProbe, defaultEndpoint, defaultShardRegionArea, defaultObjectRegionArea)));
            var clientHelloMessage = new ClientHelloMessage()
            {
                Message = string.Empty,
                ClientId = CLIENTID,
                ClientPort = 9999,
                Version = typeof(ServerAuthenticationRequest).Assembly.GetName().Version.ToString(),
            };
            var clientAuthenticationResponse = new ClientAuthenticationResponse()
            {
                AuthenticationToken = TOKEN,
            };

            //execute - client hello
            testeeRef.Tell(new UdpTransferFrame(FrameType.ClientHello, clientHelloMessage.Serialize()));

            //verify
            clientSocketProbe.ExpectMsg<Udp.Send>(s =>
            {
                var msg = new UdpTransferFrame(s.Payload.ToArray());
                Assert.Equal(FrameType.ServerAuthenticationRequest, msg.Type);
            });

            //execute - client authentication response
            testeeRef.Tell(new UdpTransferFrame(FrameType.ClientAuthenticationResponse, clientAuthenticationResponse.Serialize()));

            //verify
            authenticationProbe.ExpectMsg<RequestAuthenticationCommand>(c =>
            {
                Assert.Equal(TOKEN, c.Token);
            });

            //execute - authentication actor response
            testeeRef.Tell(new AuthenticationSuccess());

            //verify
            clientSocketProbe.ExpectMsg<Udp.Send>(s =>
            {
                var msg = new UdpTransferFrame(s.Payload.ToArray());
                Assert.Equal(FrameType.ServerHello, msg.Type);
                var serverHello = new ServerHelloMessage(msg.MessageBuffer);
                Assert.Equal(EXPECTEDRESPONSE, serverHello.Message);
                Assert.Equal(clientHelloMessage.Version, serverHello.Version);
            });

            return testeeRef;
        }
    }
}
