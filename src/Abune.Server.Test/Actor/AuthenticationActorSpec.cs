namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Sharding;
    using Abune.Server.Test.TestKit;
    using Akka.Actor;
    using Akka.Cluster.Sharding;
    using System;
    using System.IO;
    using Xunit;

    public class AuthenticationActorSpec : AbuneSpec
    {
        [Fact]
        public void ActorMustReplyStateRequestWithJson()
        {
            /* https://jwt.io  
             * used this data
             * 
             *  {
             *    "sub": "user",
             *    "iss": "jwt.io",
             *    "aud": "abune.server",
             *    "name": "John Doe",
             *    "iat": 1577836800,
             *    "exp": 1893456000,
             *    "abune.chlg": "140.82.121.3:50789|12131231241231"
             *  }
             *  
             *  use default key 'your-256-bit-secret'
            */

            var shardRegionObjectProbe = CreateTestProbe();
            var replyToProbe = CreateTestProbe();
            var testeeRef = Sys.ActorOf(Props.Create(() => new AuthenticationActor("jwt.io", "abune.server", GenerateBase64Key("your-256-bit-secret"))));
            testeeRef.Tell(new RequestAuthenticationCommand()
            {
                AuthenticationChallenge = "140.82.121.3:50789|12131231241231",
                ReplyTo = replyToProbe,
                Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyIiwiaXNzIjoiand0LmlvIiwiYXVkIjoiYWJ1bmUuc2VydmVyIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTc3ODM2ODAwLCJleHAiOjE4OTM0NTYwMDAsImFibmNoIjoiMTQwLjgyLjEyMS4zOjUwNzg5fDEyMTMxMjMxMjQxMjMxIn0.aFgb03OyqVlG5ri9zqqPMgT4GT0xGx54Ah8IVYHy0M8",
            });
            replyToProbe.ExpectMsg<AuthenticationSuccess>(m =>
            {
                //TODO: Add ip:port 
            });
        }

        private static string GenerateBase64Key(string text)
        {
            using (MemoryStream strm = new MemoryStream())
            {
                for (int c = 0; c < text.Length; c++)
                {
                    strm.WriteByte((byte)text[c]);
                }
                return Convert.ToBase64String(strm.ToArray());
            }
        }
    }
}
