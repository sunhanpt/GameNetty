using System.Net;

namespace ET.Server
{
    [Invoke((long)SceneType.Router)]
    public class FiberInit_Router: AInvokeHandler<FiberInit, ETTask>
    {
        public override async ETTask Handle(FiberInit fiberInit)
        {
            Scene root = fiberInit.Fiber.Root;
            StartSceneConfig startSceneConfig = StartSceneConfigCategory.Instance.Get((int)root.Id);
            
            // 开发期间使用OuterIPPort；云服务器本机没有公网 IP 时改为监听 0.0.0.0（ET_BIND_ANY=1），安全组/防火墙做端口映射
            root.AddComponent<RouterComponent, IPEndPoint, string>(NetworkHelper.ListenEndPoint(startSceneConfig.Port, startSceneConfig.OuterIPPort), startSceneConfig.StartProcessConfig.InnerIP);
            Log.Console($"Router create: {root.Fiber.Id}");
            await ETTask.CompletedTask;
        }
    }
}