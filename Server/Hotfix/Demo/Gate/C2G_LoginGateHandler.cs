using System;


namespace ET.Server
{
    [MessageSessionHandler(SceneType.Gate)]
    public class C2G_LoginGateHandler : MessageSessionHandler<C2G_LoginGate, G2C_LoginGate>
    {
        protected override async ETTask Run(Session session, C2G_LoginGate request, G2C_LoginGate response)
        {
            Scene root = session.Root();
            string account = root.GetComponent<GateSessionKeyComponent>().Get(request.Key);
            if (account == null)
            {
                response.Error = ErrorCore.ERR_ConnectGateKeyError;
                response.Message = "Gate key验证失败!";
                return;
            }
            
            session.RemoveComponent<SessionAcceptTimeoutComponent>();
            long sessionInstanceId = session.InstanceId;
            using (await root.GetComponent<CoroutineLockComponent>().Wait(CoroutineLockType.GateLogin, account.GetLongHashCode()))
            {
                if (session.InstanceId != sessionInstanceId) return;

                PlayerComponent playerComponent = root.GetComponent<PlayerComponent>();
                Player player = playerComponent.GetByAccount(account);
                if (player == null)
                {
                    DBComponent db = root.GetComponent<DBManagerComponent>().GetZoneDB(root.Zone());
                    AdventureProgress progress = await AdventureProgressStore.GetOrCreate(
                        AdventureProgressStore.GetCollection(db), account, IdGenerater.Instance.GenerateId());
                    if (session.InstanceId != sessionInstanceId) return;

                    player = playerComponent.AddChildWithId<Player, string>(progress.PlayerId, account);
                    try
                    {
                        PlayerSessionComponent playerSession = player.AddComponent<PlayerSessionComponent>();
                        playerSession.AddComponent<MailBoxComponent, MailBoxType>(MailBoxType.GateSession);
                        await playerSession.AddLocation(LocationType.GateSession);

                        player.AddComponent<MailBoxComponent, MailBoxType>(MailBoxType.UnOrderedMessage);
                        await player.AddLocation(LocationType.Player);
                        playerComponent.Add(player);
                    }
                    catch
                    {
                        player.Dispose();
                        throw;
                    }
                }

                if (session.InstanceId != sessionInstanceId) return;

                SessionPlayerComponent sessionPlayer = session.GetComponent<SessionPlayerComponent>() ?? session.AddComponent<SessionPlayerComponent>();
                sessionPlayer.Player = player;
                player.GetComponent<PlayerSessionComponent>().Session = session;
                response.PlayerId = player.Id;
            }
        }

        private static async ETTask CheckRoom(Player player, Session session)
        {
            Fiber fiber = player.Fiber();
            await fiber.WaitFrameFinish();

            // G2Room_Reconnect g2RoomReconnect = G2Room_Reconnect.Create();
            // g2RoomReconnect.PlayerId = player.Id;
            // using Room2G_Reconnect room2GateReconnect = await fiber.Root.GetComponent<MessageSender>().Call(
            //     player.GetComponent<PlayerRoomComponent>().RoomActorId,
            //     g2RoomReconnect) as Room2G_Reconnect;
            // G2C_Reconnect g2CReconnect = G2C_Reconnect.Create();
            // g2CReconnect.StartTime = room2GateReconnect.StartTime;
            // g2CReconnect.Frame = room2GateReconnect.Frame;
            // g2CReconnect.UnitInfos.AddRange(room2GateReconnect.UnitInfos);
            // session.Send(g2CReconnect);
            
            session.AddComponent<SessionPlayerComponent>().Player = player;
            player.GetComponent<PlayerSessionComponent>().Session = session;
        }
    }
}
