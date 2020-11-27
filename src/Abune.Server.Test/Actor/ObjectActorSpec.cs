namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Command;
    using Abune.Server.Sharding;
    using Abune.Server.Test.TestKit;
    using Abune.Shared.Command;
    using Abune.Shared.DataType;
    using Abune.Shared.Message;
    using Abune.Shared.Util;
    using Akka.Actor;
    using Akka.TestKit;
    using Moq;
    using System;
    using System.Globalization;
    using Xunit;

    public class ObjectActorSpec : AbuneSpec
    {
        private Mock<IShardRegionResolver> shardRegionResolver;
        private IActorRef defaultShardRegionArea;
        private const ulong DEFAULTOBJECTID = 1234567890L;
        private readonly AVector3 DEFAULTVECTOR = new AVector3
        {
            X = 10.0f,
            Y = 20.0f,
            Z = 30.0f
        };

        public ObjectActorSpec()
        {
            defaultShardRegionArea = CreateTestProbe();
            shardRegionResolver = new Mock<IShardRegionResolver>();
            shardRegionResolver.Setup(m => m.GetShardRegion(ShardRegions.AREAREGION)).Returns(defaultShardRegionArea);
        }

        [Fact]
        public void ActorMustStartAndStopOutsideShardRegion()
        {
            //setup
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(shardRegionResolver.Object)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));
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
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(shardRegionResolver.Object)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));

            //execute
            testeeRef.Tell(new RequestStateCommand(replyToProbe));

            //verify
            replyToProbe.ExpectMsg<RespondStateCommand>(m =>
            {
                Assert.True(m.JsonState.Contains(DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture), System.StringComparison.InvariantCulture));
            });
        }

        [Fact]
        public void ActorMustNotifyAreaRegionOnCreationAndDestruction()
        {
            //setup
            var areRegionProbe = CreateTestProbe();
            var localShardRegionResolver = new Mock<IShardRegionResolver>();
            localShardRegionResolver.Setup(m => m.GetShardRegion(ShardRegions.AREAREGION)).Returns(areRegionProbe);
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(localShardRegionResolver.Object)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));
            Watch(testeeRef);

            var cmdCreate = new ObjectCreateCommand(
                frameTick: 0,
                objectId: DEFAULTOBJECTID,
                parentObjectId: 0,
                ownerId: 0,
                typeId: 0,
                targetPosition: new AVector3 {
                    X = 10.0f,
                    Y = 20.0f,
                    Z = 30.0f,
                },
                targetOrientation: new AQuaternion {
                    X = 40.0f,
                    Y = 50.0f,
                    Z = 60.0f,
                    W = 70.0f,
                });
            var cmdEnvCreate = new ObjectCommandEnvelope(0, cmdCreate, DEFAULTOBJECTID);

            var cmdDestroy = new ObjectDestroyCommand(
                frameTick: 0,
                objectId: DEFAULTOBJECTID,
                targetPosition: new AVector3
                {
                    X = 10.0f,
                    Y = 20.0f,
                    Z = 30.0f
                });
            var cmdEnvDestroy = new ObjectCommandEnvelope(0, cmdDestroy, DEFAULTOBJECTID);

            //execute
            testeeRef.Tell(cmdEnvCreate);

            //verify - object enters area
            areRegionProbe.ExpectMsg<ObjectEnterAreaCommand>(m => {
                Assert.Equal(DEFAULTOBJECTID, m.ObjectId);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdCreate.TargetPosition), m.AreaId);
            });

            //create reaches area subscribers 
            areRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdCreate.TargetPosition), m.ToAreaId);
                Assert.Equal(CommandType.ObjectCreate, m.ObjectCommandEnvelope.Command.Type);
            });

            //execute
            testeeRef.Tell(cmdEnvDestroy);

            //verify - object leaves area
            areRegionProbe.ExpectMsg<ObjectLeaveAreaCommand>(m => {
                Assert.Equal(DEFAULTOBJECTID, m.ObjectId);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdDestroy.TargetPosition), m.AreaId);
            });

            //verify - destroy reaches area subscribers 
            areRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdCreate.TargetPosition), m.ToAreaId);
                Assert.Equal(CommandType.ObjectDestroy, m.ObjectCommandEnvelope.Command.Type);
            });
            ExpectTerminated(testeeRef);
        }

        [Fact]
        public void ActorMustUpdateLocationMustInformOldAndNewArea()
        {
            //setup
            var areaRegionProbe = CreateTestProbe();
            IActorRef testeeRef = CreateDefaultObjectActor(areaRegionProbe);
            float newPosX = 3000.0f;
            float newPosY = 4000.0f;
            float newPosZ = 5000.0f;

            var cmdUpdatePosition = new ObjectUpdatePositionCommand(                
                targetPosition: new AVector3
                {
                    X = newPosX,
                    Y = newPosY,
                    Z = newPosZ
                },
                targetOrientation: new AQuaternion
                {
                    X = 40.0f,
                    Y = 50.0f,
                    Z = 60.0f,
                    W = 70.0f
                },
                startFrameTick: 0,
                stopFrameTick: 0
                );
            var cmdEnvUpdatePosition = new ObjectCommandEnvelope(0, cmdUpdatePosition, DEFAULTOBJECTID);
            var expectedOldAreaId = Locator.GetAreaIdFromWorldPosition(DEFAULTVECTOR);
            var expectedNewAreaId = Locator.GetAreaIdFromWorldPosition(cmdUpdatePosition.TargetPosition);

            //execute
            testeeRef.Tell(cmdEnvUpdatePosition);

            //verify - update position in old area
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(CommandType.ObjectUpdatePosition, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(expectedOldAreaId, m.ToAreaId);
            });

            //verify - notify enter new area
            areaRegionProbe.ExpectMsg<ObjectEnterAreaCommand>(m =>
            {
                Assert.Equal(expectedNewAreaId, m.AreaId);
                Assert.Equal(cmdEnvUpdatePosition.ToObjectId, m.ObjectId);
            });

            //verify - notify leave old area
            areaRegionProbe.ExpectMsg<ObjectLeaveAreaCommand>(m =>
            {
                Assert.Equal(expectedOldAreaId, m.AreaId);
                Assert.Equal(cmdEnvUpdatePosition.ToObjectId, m.ObjectId);
            });

            //verify - update position in new are
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m => 
            {
                Assert.Equal(CommandType.ObjectUpdatePosition, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(expectedNewAreaId, m.ToAreaId);
            });
        }

        [Fact]
        public void ActorMustIgnoreAnyNonOwnerCommand()
        {
            //setup
            const uint LOCKOWNER = 9999;
            const uint NONLOCKOWNER = 8888;
            var areaRegionProbe = CreateTestProbe();
            IActorRef testeeRef = CreateDefaultObjectActor(areaRegionProbe);
            var cmdLock = new ObjectLockCommand(
                objectId: DEFAULTOBJECTID,
                lockOwnerId: LOCKOWNER,
                timeout: TimeSpan.FromMinutes(2)
            );
            var cmdEnvLock = new ObjectCommandEnvelope(LOCKOWNER, cmdLock, DEFAULTOBJECTID);

            var cmdUpdatePosition = new ObjectUpdatePositionCommand(
                targetPosition: new AVector3
                {
                    X = 10.0f,
                    Y = 20.0f,
                    Z = 30.0f,
                },
                targetOrientation: new AQuaternion
                {
                    X = 40.0f,
                    Y = 50.0f,
                    Z = 60.0f,
                    W = 70.0f,
                },
                startFrameTick: 0,
                stopFrameTick: 0
                );
            var cmdEnvUpdatePosition = new ObjectCommandEnvelope(NONLOCKOWNER, cmdUpdatePosition, DEFAULTOBJECTID);

            //execute
            testeeRef.Tell(cmdEnvLock);

            //verify - update position in new are
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(CommandType.ObjectLock, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTVECTOR), m.ToAreaId);
            });

            //execute
            testeeRef.Tell(cmdEnvUpdatePosition);

            areaRegionProbe.ExpectNoMsg();
        }

        [Fact]
        public void ActorMustUnlockAfterTimeout()
        {
            //setup
            const uint LOCKOWNER = 9999;
            const uint NONLOCKOWNER = 8888;
            var areaRegionProbe = CreateTestProbe();
            IActorRef testeeRef = CreateDefaultObjectActor(areaRegionProbe);
            var cmdLock = new ObjectLockCommand(
                objectId: DEFAULTOBJECTID,
                lockOwnerId: LOCKOWNER,
                timeout: TimeSpan.FromMilliseconds(0.1)
            );
            var cmdEnvLock = new ObjectCommandEnvelope(LOCKOWNER, cmdLock, DEFAULTOBJECTID);

            var cmdUpdatePosition = new ObjectUpdatePositionCommand(
                targetPosition: new AVector3
                {
                    X = 10.0f,
                    Y = 20.0f,
                    Z = 30.0f,
                },
                targetOrientation: new AQuaternion {
                    X = 40.0f,
                    Y = 50.0f,
                    Z = 60.0f,
                    W = 70.0f,
                },
                startFrameTick: 0,
                stopFrameTick: 0
                );
            var cmdEnvUpdatePosition = new ObjectCommandEnvelope(NONLOCKOWNER, cmdUpdatePosition, DEFAULTOBJECTID);

            //execute
            testeeRef.Tell(cmdEnvLock);
            
            //verify - update position in new are
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(CommandType.ObjectLock, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTVECTOR), m.ToAreaId);
            });

            //execute
            testeeRef.Tell(ReceiveTimeout.Instance);

            //verify - instance is unlocked
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(CommandType.ObjectUnlock, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTVECTOR), m.ToAreaId);
            });

            //execute
            testeeRef.Tell(cmdEnvUpdatePosition);

            //verify - update position unlocked 
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(CommandType.ObjectUpdatePosition, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTVECTOR), m.ToAreaId);
            });
        }

        private IActorRef CreateDefaultObjectActor(TestProbe areaRegionProbe)
        {
            return CreateDefaultObjectActor(areaRegionProbe, DEFAULTVECTOR);
        }

        private IActorRef CreateDefaultObjectActor(TestProbe areaRegionProbe, AVector3 pos)
        {
            //setup
            var localShardRegionResolver = new Mock<IShardRegionResolver>();
            localShardRegionResolver.Setup(m => m.GetShardRegion(ShardRegions.AREAREGION)).Returns(areaRegionProbe);
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(localShardRegionResolver.Object)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));
            Watch(testeeRef);
            var cmdCreate = new ObjectCreateCommand(
                frameTick: 0,
                objectId: DEFAULTOBJECTID,
                parentObjectId: 0,
                ownerId: 0,
                typeId: 0,
                targetPosition: pos,
                targetOrientation: new AQuaternion
                {
                    X = 40.0f,
                    Y = 50.0f,
                    Z = 60.0f,
                    W = 70.0f
                });
            var cmdEnvCreate = new ObjectCommandEnvelope(0, cmdCreate, DEFAULTOBJECTID);

            //execute
            testeeRef.Tell(cmdEnvCreate);

            //verify
            areaRegionProbe.ExpectMsg<ObjectEnterAreaCommand>();
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>();
            return testeeRef;
        }
    }
}
