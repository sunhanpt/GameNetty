using MemoryPack;
using System.Collections.Generic;

namespace ET
{
    [MemoryPackable]
    [Message(AdventureMessage.C2G_Adventure)]
    [ResponseType(nameof(G2C_Adventure))]
    public partial class C2G_Adventure : MessageObject, ISessionRequest
    {
        public static C2G_Adventure Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(C2G_Adventure), isFromPool) as C2G_Adventure;
        }

        [MemoryPackOrder(0)]
        public int RpcId { get; set; }

        [MemoryPackOrder(1)]
        public AdventureAction Action { get; set; }

        [MemoryPackOrder(2)]
        public long ExpectedRevision { get; set; }

        [MemoryPackOrder(3)]
        public int ChapterId { get; set; }

        [MemoryPackOrder(4)]
        public int EquipmentId { get; set; }

        [MemoryPackOrder(5)]
        public int TargetId { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.RpcId = default;
            this.Action = default;
            this.ExpectedRevision = default;
            this.ChapterId = default;
            this.EquipmentId = default;
            this.TargetId = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(AdventureMessage.G2C_Adventure)]
    public partial class G2C_Adventure : MessageObject, ISessionResponse
    {
        public static G2C_Adventure Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(G2C_Adventure), isFromPool) as G2C_Adventure;
        }

        [MemoryPackOrder(0)]
        public int RpcId { get; set; }

        [MemoryPackOrder(1)]
        public int Error { get; set; }

        [MemoryPackOrder(2)]
        public string Message { get; set; }

        [MemoryPackOrder(3)]
        public long PlayerId { get; set; }

        [MemoryPackOrder(4)]
        public long RunId { get; set; }

        [MemoryPackOrder(5)]
        public long Revision { get; set; }

        [MemoryPackOrder(6)]
        public AdventurePhase Phase { get; set; }

        [MemoryPackOrder(7)]
        public long Coins { get; set; }

        [MemoryPackOrder(8)]
        public int ChapterId { get; set; }

        [MemoryPackOrder(9)]
        public int ConfigVersion { get; set; }

        [MemoryPackOrder(10)]
        public string ChapterName { get; set; }

        [MemoryPackOrder(11)]
        public string EnemyName { get; set; }

        [MemoryPackOrder(12)]
        public bool Won { get; set; }

        [MemoryPackOrder(13)]
        public int RewardCoins { get; set; }

        [MemoryPackOrder(14)]
        public List<AdventureRound> Rounds { get; set; } = new();

        [MemoryPackOrder(15)]
        public long LastSettledRunId { get; set; }

        [MemoryPackOrder(16)]
        public bool LastSettlementWon { get; set; }

        [MemoryPackOrder(17)]
        public int LastSettlementCoins { get; set; }

        [MemoryPackOrder(18)]
        public AdventureSceneInfo Scene { get; set; }

        [MemoryPackOrder(19)]
        public List<AdventureChapterOption> Chapters { get; set; } = new();

        [MemoryPackOrder(20)]
        public List<AdventureEquipmentOption> Equipment { get; set; } = new();

        [MemoryPackOrder(21)]
        public List<AdventureGrowthOption> Growth { get; set; } = new();

        [MemoryPackOrder(22)]
        public List<AdventureChallengeOption> Challenges { get; set; } = new();

        [MemoryPackOrder(23)]
        public int ChallengeId { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.RpcId = default;
            this.Error = default;
            this.Message = default;
            this.PlayerId = default;
            this.RunId = default;
            this.Revision = default;
            this.Phase = default;
            this.Coins = default;
            this.ChapterId = default;
            this.ConfigVersion = default;
            this.ChapterName = default;
            this.EnemyName = default;
            this.Won = default;
            this.RewardCoins = default;
            this.Rounds.Clear();
            this.LastSettledRunId = default;
            this.LastSettlementWon = default;
            this.LastSettlementCoins = default;
            this.Scene = default;
            this.Chapters.Clear();
            this.Equipment.Clear();
            this.Growth.Clear();
            this.Challenges.Clear();
            this.ChallengeId = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(AdventureMessage.AdventureRound)]
    public partial class AdventureRound : MessageObject
    {
        public static AdventureRound Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(AdventureRound), isFromPool) as AdventureRound;
        }

        [MemoryPackOrder(0)]
        public int Number { get; set; }

        [MemoryPackOrder(1)]
        public int PlayerDamage { get; set; }

        [MemoryPackOrder(2)]
        public int EnemyDamage { get; set; }

        [MemoryPackOrder(3)]
        public int PlayerHealth { get; set; }

        [MemoryPackOrder(4)]
        public int EnemyHealth { get; set; }

        [MemoryPackOrder(5)]
        public int CompanionDamage { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.Number = default;
            this.PlayerDamage = default;
            this.EnemyDamage = default;
            this.PlayerHealth = default;
            this.EnemyHealth = default;
            this.CompanionDamage = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(AdventureMessage.AdventureSceneInfo)]
    public partial class AdventureSceneInfo : MessageObject
    {
        public static AdventureSceneInfo Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(AdventureSceneInfo), isFromPool) as AdventureSceneInfo;
        }

        [MemoryPackOrder(0)]
        public string ResourceKey { get; set; }

        [MemoryPackOrder(1)]
        public string TravelText { get; set; }

        [MemoryPackOrder(2)]
        public string EncounterText { get; set; }

        [MemoryPackOrder(3)]
        public string EventTitle { get; set; }

        [MemoryPackOrder(4)]
        public string EventText { get; set; }

        [MemoryPackOrder(5)]
        public string TakeLabel { get; set; }

        [MemoryPackOrder(6)]
        public string SkipLabel { get; set; }

        [MemoryPackOrder(7)]
        public int PlayerHealth { get; set; }

        [MemoryPackOrder(8)]
        public int EnemyHealth { get; set; }

        [MemoryPackOrder(9)]
        public string CompanionName { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.ResourceKey = default;
            this.TravelText = default;
            this.EncounterText = default;
            this.EventTitle = default;
            this.EventText = default;
            this.TakeLabel = default;
            this.SkipLabel = default;
            this.PlayerHealth = default;
            this.EnemyHealth = default;
            this.CompanionName = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(AdventureMessage.AdventureChapterOption)]
    public partial class AdventureChapterOption : MessageObject
    {
        public static AdventureChapterOption Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(AdventureChapterOption), isFromPool) as AdventureChapterOption;
        }

        [MemoryPackOrder(0)]
        public int Id { get; set; }

        [MemoryPackOrder(1)]
        public string Name { get; set; }

        [MemoryPackOrder(2)]
        public string ResourceKey { get; set; }

        [MemoryPackOrder(3)]
        public int RewardCoins { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.Id = default;
            this.Name = default;
            this.ResourceKey = default;
            this.RewardCoins = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(AdventureMessage.AdventureEquipmentOption)]
    public partial class AdventureEquipmentOption : MessageObject
    {
        public static AdventureEquipmentOption Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(AdventureEquipmentOption), isFromPool) as AdventureEquipmentOption;
        }

        [MemoryPackOrder(0)]
        public int Id { get; set; }

        [MemoryPackOrder(1)]
        public string Name { get; set; }

        [MemoryPackOrder(2)]
        public AdventureEquipmentSlot Slot { get; set; }

        [MemoryPackOrder(3)]
        public int Rank { get; set; }

        [MemoryPackOrder(4)]
        public int MaxRank { get; set; }

        [MemoryPackOrder(5)]
        public bool Equipped { get; set; }

        [MemoryPackOrder(6)]
        public int Attack { get; set; }

        [MemoryPackOrder(7)]
        public int Health { get; set; }

        [MemoryPackOrder(8)]
        public int Price { get; set; }

        [MemoryPackOrder(9)]
        public long UpgradeCost { get; set; }

        [MemoryPackOrder(10)]
        public int NextAttack { get; set; }

        [MemoryPackOrder(11)]
        public int NextHealth { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.Id = default;
            this.Name = default;
            this.Slot = default;
            this.Rank = default;
            this.MaxRank = default;
            this.Equipped = default;
            this.Attack = default;
            this.Health = default;
            this.Price = default;
            this.UpgradeCost = default;
            this.NextAttack = default;
            this.NextHealth = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(AdventureMessage.AdventureGrowthOption)]
    public partial class AdventureGrowthOption : MessageObject
    {
        public static AdventureGrowthOption Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(AdventureGrowthOption), isFromPool) as AdventureGrowthOption;
        }

        [MemoryPackOrder(0)]
        public int Id { get; set; }

        [MemoryPackOrder(1)]
        public string Name { get; set; }

        [MemoryPackOrder(2)]
        public AdventureGrowthKind Kind { get; set; }

        [MemoryPackOrder(3)]
        public int Rank { get; set; }

        [MemoryPackOrder(4)]
        public int MaxRank { get; set; }

        [MemoryPackOrder(5)]
        public long Cost { get; set; }

        [MemoryPackOrder(6)]
        public int AttackPerRank { get; set; }

        [MemoryPackOrder(7)]
        public int HealthPerRank { get; set; }

        [MemoryPackOrder(8)]
        public bool Assigned { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.Id = default;
            this.Name = default;
            this.Kind = default;
            this.Rank = default;
            this.MaxRank = default;
            this.Cost = default;
            this.AttackPerRank = default;
            this.HealthPerRank = default;
            this.Assigned = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    [MemoryPackable]
    [Message(AdventureMessage.AdventureChallengeOption)]
    public partial class AdventureChallengeOption : MessageObject
    {
        public static AdventureChallengeOption Create(bool isFromPool = false)
        {
            return ObjectPool.Instance.Fetch(typeof(AdventureChallengeOption), isFromPool) as AdventureChallengeOption;
        }

        [MemoryPackOrder(0)]
        public int Id { get; set; }

        [MemoryPackOrder(1)]
        public string Name { get; set; }

        [MemoryPackOrder(2)]
        public string ResourceKey { get; set; }

        [MemoryPackOrder(3)]
        public int RewardCoins { get; set; }

        [MemoryPackOrder(4)]
        public int AttemptsLeft { get; set; }

        [MemoryPackOrder(5)]
        public int DailyAttempts { get; set; }

        [MemoryPackOrder(6)]
        public bool Unlocked { get; set; }

        public override void Dispose()
        {
            if (!this.IsFromPool)
            {
                return;
            }

            this.Id = default;
            this.Name = default;
            this.ResourceKey = default;
            this.RewardCoins = default;
            this.AttemptsLeft = default;
            this.DailyAttempts = default;
            this.Unlocked = default;

            ObjectPool.Instance.Recycle(this);
        }
    }

    public static class AdventureMessage
    {
        public const ushort C2G_Adventure = 11001;
        public const ushort G2C_Adventure = 11002;
        public const ushort AdventureRound = 11003;
        public const ushort AdventureSceneInfo = 11004;
        public const ushort AdventureChapterOption = 11005;
        public const ushort AdventureEquipmentOption = 11006;
        public const ushort AdventureGrowthOption = 11007;
        public const ushort AdventureChallengeOption = 11008;
    }
}