using System.Net;
using ET;
using UnityEngine;

namespace GameScripts.GameLogic
{
    [MessageHandler(SceneType.NetClient)]
    public class Main2NetClient_LoginHandler : MessageHandler<Scene, Main2NetClient_Login, NetClient2Main_Login>
    {
#if UNITY_EDITOR
        private static string LocalServerPreferenceKey => $"GameNetty.UseLocalServer:{UnityEngine.Application.dataPath}";

        public static bool UseLocalServer
        {
            get => UnityEditor.EditorPrefs.GetBool(LocalServerPreferenceKey, false);
            set => UnityEditor.EditorPrefs.SetBool(LocalServerPreferenceKey, value);
        }
#endif

        protected override async ETTask Run(Scene root, Main2NetClient_Login request, NetClient2Main_Login response)
        {
            string account = request.Account;
            string password = request.Password;

            root.RemoveComponent<RouterAddressComponent>();
            root.RemoveComponent<NetComponent>();
            root.RemoveComponent<NetWSComponent>();

            string routerHost = GetRouterHost();
            Log.Info($"login http {routerHost}:{ConstValue.RouterHttpPort}");
            RouterAddressComponent routerAddressComponent =
                    root.AddComponent<RouterAddressComponent, string, int>(routerHost, ConstValue.RouterHttpPort);
            await routerAddressComponent.Init();

            root.GetComponent<FiberParentComponent>().ParentFiberId = request.OwnerFiberId;
            NetWSComponent netWs = root.AddComponent<NetWSComponent>();

            IPEndPoint innerRealm = routerAddressComponent.GetRealmAddress(account);
            IPAddress routerIp = routerAddressComponent.RouterManagerIPAddress;
            IPEndPoint realmAddress = new IPEndPoint(routerIp, innerRealm.Port);
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
            IPEndPoint gateAddress = new IPEndPoint(routerIp, innerGate.Port);
            Log.Info($"login websocket gate {gateAddress} (server returned {r2CLogin.Address})");

            Session gateSession = netWs.Create(gateAddress);
            await WaitWebsocketAsync(root);
            gateSession.AddComponent<ClientSessionErrorComponent>();
            root.AddComponent<SessionComponent>().Session = gateSession;
            C2G_LoginGate c2GLoginGate = C2G_LoginGate.Create();
            c2GLoginGate.Key = r2CLogin.Key;
            c2GLoginGate.GateId = r2CLogin.GateId;
            G2C_LoginGate g2CLoginGate = (G2C_LoginGate)await gateSession.Call(c2GLoginGate, 15000);

            if (g2CLoginGate.Error != 0 || g2CLoginGate.PlayerId == 0)
            {
                throw new RpcException(g2CLoginGate.Error != 0 ? g2CLoginGate.Error : ErrorCore.ERR_RpcFail, g2CLoginGate.Message);
            }

            gateSession.AddComponent<PingComponent>();
            response.PlayerId = g2CLoginGate.PlayerId;
            Log.Info("登陆gate成功!");
            await ETTask.CompletedTask;
        }

        private static string GetRouterHost()
        {
#if UNITY_EDITOR
            if (UseLocalServer)
            {
                return IPAddress.Loopback.ToString();
            }
#endif
            // NetClient runs on the Unity main scheduler, where Resources can be accessed.
            string configuredHost = Resources.Load<TextAsset>("GameNettyRouterHost")?.text.Trim();
            return string.IsNullOrEmpty(configuredHost) ? IPAddress.Loopback.ToString() : configuredHost;
        }

        private static async ETTask WaitWebsocketAsync(Scene root)
        {
            await root.GetComponent<TimerComponent>().WaitAsync(300);
        }
    }
}
