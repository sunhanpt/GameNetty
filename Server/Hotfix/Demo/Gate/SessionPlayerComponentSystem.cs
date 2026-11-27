namespace ET.Server
{
    [EntitySystemOf(typeof(SessionPlayerComponent))]
    public static partial class SessionPlayerComponentSystem
    {
        [EntitySystem]
        private static void Destroy(this SessionPlayerComponent self)
        {
            Scene root = self.Root();
            if (root.IsDisposed)
            {
                return;
            }
            Player player = self.Player;
            PlayerSessionComponent playerSession = player?.GetComponent<PlayerSessionComponent>();
            Session current = playerSession?.Session;
            if (playerSession == null || (current != null && current != self.Parent))
            {
                return;
            }

            playerSession.Session = null;
            if (player.GetComponent<GateMapComponent>() == null)
            {
                return;
            }

            root.GetComponent<MessageLocationSenderComponent>().Get(LocationType.Unit).Send(player.Id, G2M_SessionDisconnect.Create());
        }
        
        [EntitySystem]
        private static void Awake(this SessionPlayerComponent self)
        {

        }
    }
}
