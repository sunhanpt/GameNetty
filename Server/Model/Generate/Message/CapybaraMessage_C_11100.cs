using MemoryPack;
using System.Collections.Generic;

namespace ET
{
    [MemoryPackable]
    [Message(CapybaraMessage.C2G_CapybaraAdventure)]
    [ResponseType(nameof(G2C_CapybaraAdventure))]
    public partial class C2G_CapybaraAdventure : MessageObject, ISessionRequest
    {
        public static C2G_CapybaraAdventure Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(C2G_CapybaraAdventure), isFromPool) as C2G_CapybaraAdventure;
        }

        [MemoryPackOrder(0)]
        public int RpcId { get; set; }

        [MemoryPackOrder(1)]
        public string CatalogVersion { get; set; }

        [MemoryPackOrder(2)]
        public string CommandId { get; set; }

        [MemoryPackOrder(3)]
        public long ExpectedRevision { get; set; }

        [MemoryPackOrder(4)]
        public int Action { get; set; }

        [MemoryPackOrder(5)]
        public int Id { get; set; }

        [MemoryPackOrder(6)]
        public int Option { get; set; }

        [MemoryPackOrder(7)]
        public int Currency { get; set; }

        [MemoryPackOrder(8)]
        public int Cost { get; set; }

        [MemoryPackOrder(9)]
        public int Draws { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.RpcId = default;
            this.CatalogVersion = default;
            this.CommandId = default;
            this.ExpectedRevision = default;
            this.Action = default;
            this.Id = default;
            this.Option = default;
            this.Currency = default;
            this.Cost = default;
            this.Draws = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(CapybaraMessage.G2C_CapybaraAdventure)]
    public partial class G2C_CapybaraAdventure : MessageObject, ISessionResponse
    {
        public static G2C_CapybaraAdventure Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(G2C_CapybaraAdventure), isFromPool) as G2C_CapybaraAdventure;
        }

        [MemoryPackOrder(0)]
        public int RpcId { get; set; }

        [MemoryPackOrder(1)]
        public int Error { get; set; }

        [MemoryPackOrder(2)]
        public string Message { get; set; }

        [MemoryPackOrder(3)]
        public long Revision { get; set; }

        [MemoryPackOrder(4)]
        public long GameDayTicks { get; set; }

        [MemoryPackOrder(5)]
        public string StateJson { get; set; }

        [MemoryPackOrder(6)]
        public string ResultJson { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.RpcId = default;
            this.Error = default;
            this.Message = default;
            this.Revision = default;
            this.GameDayTicks = default;
            this.StateJson = default;
            this.ResultJson = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    public static class CapybaraMessage
    {
        public const ushort C2G_CapybaraAdventure = 11101;
        public const ushort G2C_CapybaraAdventure = 11102;
    }
}