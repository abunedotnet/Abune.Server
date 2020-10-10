namespace Abune.Server.Test.Actor
{
    using Abune.Server.Actor;
    using Abune.Server.Actor.Command;
    using Abune.Server.Command;
    using Abune.Server.Test.TestKit;
    using Abune.Shared.Command;
    using Abune.Shared.Message;
    using Abune.Shared.Util;
    using Akka.Actor;
    using Akka.TestKit;
    using System;
    using System.Globalization;
    using Xunit;

    public class ObjectActorSpec : AbuneSpec
    {
        private IActorRef defaultShardRegionArea;
        private const ulong DEFAULTOBJECTID = 1234567890L;
        private const float DEFAULTPOSX = 10.0f;
        private const float DEFAULTPOSY = 20.0f;
        private const float DEFAULTPOSZ = 30.0f;

        public ObjectActorSpec()
        {
            defaultShardRegionArea = CreateTestProbe();
        }

        [Fact]
        public void ActorMustStartAndStopOutsideShardRegion()
        {
            //setup
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(defaultShardRegionArea)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));
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
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(defaultShardRegionArea)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));

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
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(areRegionProbe)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));
            Watch(testeeRef);

            var cmdCreate = new ObjectCreateCommand(
                frameTick: 0,
                objectId: DEFAULTOBJECTID,
                parentObjectId: 0,
                ownerId: 0,
                typeId: 0,
                targetPositionX: 10.0f,
                targetPositionY: 20.0f,
                targetPositionZ: 30.0f,
                quaternionX: 40.0f,
                quaternionY: 50.0f,
                quaternionZ: 60.0f,
                quaternionW: 70.0f);
            var cmdEnvCreate = new ObjectCommandEnvelope(0, cmdCreate, DEFAULTOBJECTID);

            var cmdDestroy = new ObjectDestroyCommand(
                frameTick: 0,
                objectId: DEFAULTOBJECTID,
                targetPositionX: 10.0f,
                targetPositionY: 20.0f,
                targetPositionZ: 30.0f);
            var cmdEnvDestroy = new ObjectCommandEnvelope(0, cmdDestroy, DEFAULTOBJECTID);

            //execute
            testeeRef.Tell(cmdEnvCreate);

            //verify - object enters area
            areRegionProbe.ExpectMsg<ObjectEnterAreaCommand>(m => {
                Assert.Equal(DEFAULTOBJECTID, m.ObjectId);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdCreate.TargetPositionX, cmdCreate.TargetPositionY, cmdCreate.TargetPositionZ), m.AreaId);
            });

            //create reaches area subscribers 
            areRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdCreate.TargetPositionX, cmdCreate.TargetPositionY, cmdCreate.TargetPositionZ), m.ToAreaId);
                Assert.Equal(CommandType.ObjectCreate, m.ObjectCommandEnvelope.Command.Type);
            });

            //execute
            testeeRef.Tell(cmdEnvDestroy);

            //verify - object leaves area
            areRegionProbe.ExpectMsg<ObjectLeaveAreaCommand>(m => {
                Assert.Equal(DEFAULTOBJECTID, m.ObjectId);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdDestroy.TargetPositionX, cmdDestroy.TargetPositionY, cmdDestroy.TargetPositionZ), m.AreaId);
            });

            //verify - destroy reaches area subscribers 
            areRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(cmdCreate.TargetPositionX, cmdCreate.TargetPositionY, cmdCreate.TargetPositionZ), m.ToAreaId);
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
                targetPositionX: newPosX,
                targetPositionY: newPosY,
                targetPositionZ: newPosZ,
                quaternionX: 40.0f,
                quaternionY: 50.0f,
                quaternionZ: 60.0f,
                quaternionW: 70.0f,
                startFrameTick: 0,
                stopFrameTick: 0
                );
            var cmdEnvUpdatePosition = new ObjectCommandEnvelope(0, cmdUpdatePosition, DEFAULTOBJECTID);
            var expectedOldAreaId = Locator.GetAreaIdFromWorldPosition(DEFAULTPOSX, DEFAULTPOSY, DEFAULTPOSZ);
            var expectedNewAreaId = Locator.GetAreaIdFromWorldPosition(cmdUpdatePosition.TargetPositionX, cmdUpdatePosition.TargetPositionY, cmdUpdatePosition.TargetPositionZ);

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
                targetPositionX: 10.0f,
                targetPositionY: 20.0f,
                targetPositionZ: 30.0f,
                quaternionX: 40.0f,
                quaternionY: 50.0f,
                quaternionZ: 60.0f,
                quaternionW: 70.0f,
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
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTPOSX, DEFAULTPOSY, DEFAULTPOSZ), m.ToAreaId);
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
                targetPositionX: 10.0f,
                targetPositionY: 20.0f,
                targetPositionZ: 30.0f,
                quaternionX: 40.0f,
                quaternionY: 50.0f,
                quaternionZ: 60.0f,
                quaternionW: 70.0f,
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
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTPOSX, DEFAULTPOSY, DEFAULTPOSZ), m.ToAreaId);
            });

            //execute
            testeeRef.Tell(ReceiveTimeout.Instance);

            //verify - instance is unlocked
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(CommandType.ObjectUnlock, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTPOSX, DEFAULTPOSY, DEFAULTPOSZ), m.ToAreaId);
            });

            //execute
            testeeRef.Tell(cmdEnvUpdatePosition);

            //verify - update position unlocked 
            areaRegionProbe.ExpectMsg<AreaCommandEnvelope>(m =>
            {
                Assert.Equal(CommandType.ObjectUpdatePosition, m.ObjectCommandEnvelope.Command.Type);
                Assert.Equal(Locator.GetAreaIdFromWorldPosition(DEFAULTPOSX, DEFAULTPOSY, DEFAULTPOSZ), m.ToAreaId);
            });
        }

        private IActorRef CreateDefaultObjectActor(TestProbe areaRegionProbe)
        {
            return CreateDefaultObjectActor(areaRegionProbe, DEFAULTPOSX, DEFAULTPOSY, DEFAULTPOSZ);
        }

        private IActorRef CreateDefaultObjectActor(TestProbe areaRegionProbe, float posX, float posY, float posZ)
        {
            //setup
            var testeeRef = Sys.ActorOf(Props.Create(() => new ObjectActor(areaRegionProbe)), DEFAULTOBJECTID.ToString(CultureInfo.InvariantCulture));
            Watch(testeeRef);
            var cmdCreate = new ObjectCreateCommand(
                frameTick: 0,
                objectId: DEFAULTOBJECTID,
                parentObjectId: 0,
                ownerId: 0,
                typeId: 0,
                targetPositionX: posX,
                targetPositionY: posY,
                targetPositionZ: posZ,
                quaternionX: 40.0f,
                quaternionY: 50.0f,
                quaternionZ: 60.0f,
                quaternionW: 70.0f);
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
