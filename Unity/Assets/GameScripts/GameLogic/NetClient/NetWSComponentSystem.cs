using System.Net;

namespace ET
{
    [EntitySystemOf(typeof(NetWSComponent))]
    [FriendOf(typeof(NetWSComponent))]
    public static partial class NetWSComponentSystem
    {
        [EntitySystem]
        private static void Awake(this NetWSComponent self)
        {
            self.AService = new WService();
            self.AService.ReadCallback = self.OnRead;
            self.AService.ErrorCallback = self.OnError;
        }

        [EntitySystem]
        private static void Update(this NetWSComponent self)
        {
            self.AService.Update();
        }

        [EntitySystem]
        private static void Destroy(this NetWSComponent self)
        {
            self.AService.Dispose();
        }

        private static void OnError(this NetWSComponent self, long channelId, int error)
        {
            Session session = self.GetChild<Session>(channelId);
            if (session == null)
            {
                return;
            }

            session.Error = error;
            session.Dispose();
        }

        private static void OnRead(this NetWSComponent self, long channelId, MemoryBuffer memoryBuffer)
        {
            Session session = self.GetChild<Session>(channelId);
            if (session == null)
            {
                return;
            }

            session.LastRecvTime = TimeInfo.Instance.ClientNow();
            (ActorId _, object message) = MessageSerializeHelper.ToMessage(self.AService, memoryBuffer);
            LogMsg.Instance.Debug(self.Fiber(), message);
            EventSystem.Instance.Invoke((long)self.IScene.SceneType, new NetComponentOnRead() { Session = session, Message = message });
        }

        public static Session Create(this NetWSComponent self, IPEndPoint ipEndPoint)
        {
            long channelId = NetServices.Instance.CreateConnectChannelId();
            Session session = self.AddChildWithId<Session, AService>(channelId, self.AService);
            session.RemoteAddress = ipEndPoint;
            session.AddComponent<SessionIdleCheckerComponent>();
            self.AService.Create(session.Id, ipEndPoint);
            return session;
        }
    }
}
