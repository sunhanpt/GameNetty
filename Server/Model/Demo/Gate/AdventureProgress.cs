using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace ET.Server
{
    [EnableClass]
    public sealed class AdventureProgress
    {
        [BsonId]
        public string Account { get; set; }
        public long PlayerId { get; set; }
        public long Revision { get; set; }
        public long RunId { get; set; }
        public long Coins { get; set; }
        public AdventurePhase Phase { get; set; }
        public AdventureAction LastAction { get; set; }
        public int LastEquipmentId { get; set; }
        public int LastTargetId { get; set; }
        public int AssignedCompanionId { get; set; }
        public List<AdventureGrowthRank> Growth { get; set; } = new();
        public List<AdventureChallengeUsage> Challenges { get; set; } = new();
        public int EquippedWeaponId { get; set; }
        public int EquippedArmorId { get; set; }
        public List<AdventureOwnedEquipment> Equipment { get; set; } = new();
        public AdventureChapterSnapshot Chapter { get; set; }
        public bool TookSupplies { get; set; }
        public bool Won { get; set; }
        public List<AdventureRoundState> Rounds { get; set; } = new();
        public AdventureSettlement LastSettlement { get; set; }
    }

    [EnableClass]
    public sealed class AdventureGrowthRank
    {
        public int Id { get; set; }
        public int Rank { get; set; }
    }

    [EnableClass]
    public sealed class AdventureChallengeUsage
    {
        public int Id { get; set; }
        public long UtcDay { get; set; }
        public int Attempts { get; set; }
    }

    [EnableClass]
    public sealed class AdventureOwnedEquipment
    {
        public int Id { get; set; }
        public int Rank { get; set; }
    }

    [EnableClass]
    public sealed class AdventureChapterSnapshot
    {
        public int Id { get; set; }
        public int ChallengeId { get; set; }
        public int Version { get; set; }
        public string ChapterName { get; set; }
        public string EnemyName { get; set; }
        public int PlayerHealth { get; set; }
        public int PlayerAttack { get; set; }
        public int CompanionAttack { get; set; }
        public string CompanionName { get; set; }
        public int SupplyAttackBonus { get; set; }
        public int EnemyHealth { get; set; }
        public int EnemyAttack { get; set; }
        public int RoundLimit { get; set; }
        public int RewardCoins { get; set; }
        public AdventureSceneSnapshot Scene { get; set; }
    }

    [EnableClass]
    public sealed class AdventureSceneSnapshot
    {
        public string ResourceKey { get; set; }
        public string TravelText { get; set; }
        public string EncounterText { get; set; }
        public string EventTitle { get; set; }
        public string EventText { get; set; }
        public string TakeLabel { get; set; }
        public string SkipLabel { get; set; }
    }

    [EnableClass]
    public sealed class AdventureRoundState
    {
        public int Number { get; set; }
        public int PlayerDamage { get; set; }
        public int CompanionDamage { get; set; }
        public int EnemyDamage { get; set; }
        public int PlayerHealth { get; set; }
        public int EnemyHealth { get; set; }
    }

    [EnableClass]
    public sealed class AdventureSettlement
    {
        public long RunId { get; set; }
        public bool Won { get; set; }
        public int RewardCoins { get; set; }
    }
}
