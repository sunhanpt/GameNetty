using System.Net;
using ET;

namespace GameScripts.GameLogic
{
    [MessageHandler(SceneType.NetClient)]
    public class Main2NetClient_LoginHandler : MessageHandler<Scene, Main2NetClient_Login, NetClient2Main_Login>
    {
        protected override async ETTask Run(Scene root, Main2NetClient_Login request, NetClient2Main_Login response)
        {
            string account = request.Account;
            string password = request.Password;

            root.RemoveComponent<RouterAddressComponent>();
            root.RemoveComponent<NetComponent>();
            root.RemoveComponent<NetWSComponent>();

            Log.Info($"login http {ConstValue.RouterHttpHost}:{ConstValue.RouterHttpPort}");
            RouterAddressComponent routerAddressComponent =
                    root.AddComponent<RouterAddressComponent, string, int>(ConstValue.RouterHttpHost, ConstValue.RouterHttpPort);
            await routerAddressComponent.Init();

            root.GetComponent<FiberParentComponent>().ParentFiberId = request.OwnerFiberId;
            NetWSComponent netWs = root.AddComponent<NetWSComponent>();

            IPEndPoint innerRealm = routerAddressComponent.GetRealmAddress(account);
            IPEndPoint realmAddress = ToPublicEndPoint(innerRealm);
            Log.Info($"login websocket realm {realmAddress} info={JsonHelper.ToJson(routerAddressComponent.Info)}");

            R2C_Login r2CLogin;
            using (Session session = netWs.Create(realmAddress))
            {
                await WaitWebsocketAsync(root);
                C2R_Login c2RLogin = C2R_Login.Create();
                c2RLogin.Account = account;
                c2RLogin.Password = password;
                r2CLogin = (R2C_Login)await session.Call(c2RLogin, 15000);
            }

            IPEndPoint innerGate = NetworkHelper.ToIPEndPoint(r2CLogin.Address);
            IPEndPoint gateAddress = ToPublicEndPoint(innerGate);
            Log.Info($"login websocket gate {gateAddress} (server returned {r2CLogin.Address})");

            Session gateSession = netWs.Create(gateAddress);
            await WaitWebsocketAsync(root);
            gateSession.AddComponent<ClientSessionErrorComponent>();
            root.AddComponent<SessionComponent>().Session = gateSession;
            C2G_LoginGate c2GLoginGate = C2G_LoginGate.Create();
            c2GLoginGate.Key = r2CLogin.Key;
            c2GLoginGate.GateId = r2CLogin.GateId;
            G2C_LoginGate g2CLoginGate = (G2C_LoginGate)await gateSession.Call(c2GLoginGate, 15000);

            response.PlayerId = g2CLoginGate.PlayerId;
            Log.Info("登陆gate成功!");
            await ETTask.CompletedTask;
        }

        private static IPEndPoint ToPublicEndPoint(IPEndPoint inner)
        {
            if (!IPAddress.TryParse(ConstValue.RouterHttpHost, out IPAddress publicIp))
            {
                publicIp = NetworkHelper.GetHostAddress(ConstValue.RouterHttpHost);
            }

            return new IPEndPoint(publicIp, inner.Port);
        }

        private static async ETTask WaitWebsocketAsync(Scene root)
        {
            await root.GetComponent<TimerComponent>().WaitAsync(300);
        }
    }
}
