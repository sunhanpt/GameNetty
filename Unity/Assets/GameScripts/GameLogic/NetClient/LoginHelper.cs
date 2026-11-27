namespace ET
{
    public static class LoginHelper
    {
        public static async ETTask Login(Scene root, string account, string password)
        {
            root.RemoveComponent<ClientSenderComponent>();
            
            ClientSenderComponent clientSenderComponent = root.AddComponent<ClientSenderComponent>();
            
            long playerId = await clientSenderComponent.LoginAsync(account, password);
            if (playerId == 0)
            {
                throw new RpcException(ErrorCore.ERR_RpcFail, "登录未返回有效的玩家 ID。");
            }
            
            Log.Info($"Login success playerId:{playerId}");
        }
    }
}
