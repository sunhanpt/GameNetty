namespace ET
{
    public enum AdventureAction
    {
        Snapshot = 0,
        Begin = 1,
        Advance = 2,
        TakeSupplies = 3,
        SkipSupplies = 4,
        Fight = 5,
        Settle = 6,
        Abandon = 7,
        BuyEquipment = 8,
        Equip = 9,
        Unequip = 10,
        UpgradeEquipment = 11,
        TrainTalent = 12,
        RecruitCompanion = 13,
        AssignCompanion = 14,
        DismissCompanion = 15,
        BeginChallenge = 16
    }

    public enum AdventurePhase
    {
        Camp = 0,
        Trail = 1,
        Event = 2,
        Battle = 3,
        Result = 4
    }

    public enum AdventureEquipmentSlot
    {
        Weapon = 1,
        Armor = 2
    }

    public enum AdventureGrowthKind
    {
        Talent = 1,
        Companion = 2
    }
}
