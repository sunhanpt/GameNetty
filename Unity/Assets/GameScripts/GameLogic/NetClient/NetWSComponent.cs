using System.Collections.Generic;
using System.Net;

namespace ET
{
    [ComponentOf(typeof(Scene))]
    public class NetWSComponent: Entity, IAwake, IDestroy, IUpdate
    {
        public AService AService;
    }
}
