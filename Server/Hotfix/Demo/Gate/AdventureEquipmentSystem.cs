namespace ET.Server
{
    public static class AdventureEquipmentSystem
    {
        public static bool IsEquipmentAction(AdventureAction action)
        {
            return action is AdventureAction.BuyEquipment or AdventureAction.Equip or AdventureAction.Unequip or AdventureAction.UpgradeEquipment;
        }

        public static string Apply(AdventureProgress progress, AdventureAction action, int equipmentId)
        {
            AdventureEquipmentConfig config = AdventureEquipmentConfigCategory.Instance.GetOrDefault(equipmentId);
            if (config == null)
            {
                return "这件装备已下线。";
            }

            AdventureOwnedEquipment owned = progress.Equipment.Find(item => item.Id == equipmentId);
            int equippedId = GetEquippedId(progress, config);
            switch (action)
            {
                case AdventureAction.BuyEquipment:
                    if (owned != null) return "你已拥有这件装备。";
                    if (config.Price < 0) return "装备价格配置无效。";
                    if (progress.Coins < config.Price) return "金币不足，无法购买。";
                    progress.Coins -= config.Price;
                    progress.Equipment.Add(new AdventureOwnedEquipment { Id = equipmentId, Rank = 1 });
                    break;
                case AdventureAction.Equip:
                    if (owned == null) return "请先购买这件装备。";
                    if (equippedId == equipmentId) return "这件装备已穿戴。";
                    SetEquippedId(progress, config, equipmentId);
                    break;
                case AdventureAction.Unequip:
                    if (equippedId != equipmentId) return "这件装备尚未穿戴。";
                    SetEquippedId(progress, config, 0);
                    break;
                case AdventureAction.UpgradeEquipment:
                    if (owned == null) return "请先购买这件装备。";
                    if (owned.Rank >= config.MaxRank) return "这件装备已达最高强化等级。";
                    long cost = UpgradeCost(config, owned.Rank);
                    if (cost < 0) return "强化价格配置无效。";
                    if (progress.Coins < cost) return "金币不足，无法强化。";
                    progress.Coins -= cost;
                    ++owned.Rank;
                    break;
                default:
                    return "未知装备操作。";
            }
            return null;
        }

        public static void ApplyBonuses(AdventureProgress progress, AdventureChapterSnapshot chapter)
        {
            foreach (AdventureOwnedEquipment owned in progress.Equipment)
            {
                AdventureEquipmentConfig config = AdventureEquipmentConfigCategory.Instance.GetOrDefault(owned.Id);
                if (config == null || GetEquippedId(progress, config) != owned.Id) continue;
                chapter.PlayerAttack += Attack(config, owned.Rank);
                chapter.PlayerHealth += Health(config, owned.Rank);
            }
        }

        public static AdventureEquipmentOption Describe(AdventureProgress progress, AdventureEquipmentConfig config)
        {
            int rank = progress.Equipment.Find(item => item.Id == config.Id)?.Rank ?? 0;
            int previewRank = rank == 0 ? 1 : rank;
            bool canUpgrade = rank > 0 && rank < config.MaxRank;
            AdventureEquipmentOption option = AdventureEquipmentOption.Create();
            option.Id = config.Id;
            option.Name = config.Name;
            option.Slot = (AdventureEquipmentSlot)config.Slot;
            option.Price = config.Price;
            option.Rank = rank;
            option.MaxRank = config.MaxRank;
            option.Equipped = rank > 0 && GetEquippedId(progress, config) == config.Id;
            option.Attack = Attack(config, previewRank);
            option.Health = Health(config, previewRank);
            option.UpgradeCost = canUpgrade ? UpgradeCost(config, rank) : 0;
            option.NextAttack = canUpgrade ? Attack(config, rank + 1) : option.Attack;
            option.NextHealth = canUpgrade ? Health(config, rank + 1) : option.Health;
            return option;
        }

        private static int GetEquippedId(AdventureProgress progress, AdventureEquipmentConfig config)
        {
            return (AdventureEquipmentSlot)config.Slot switch
            {
                AdventureEquipmentSlot.Weapon => progress.EquippedWeaponId,
                AdventureEquipmentSlot.Armor => progress.EquippedArmorId,
                _ => throw new System.InvalidOperationException($"Equipment {config.Id} has an invalid slot {config.Slot}.")
            };
        }

        private static void SetEquippedId(AdventureProgress progress, AdventureEquipmentConfig config, int equipmentId)
        {
            if ((AdventureEquipmentSlot)config.Slot == AdventureEquipmentSlot.Weapon)
            {
                progress.EquippedWeaponId = equipmentId;
            }
            else
            {
                progress.EquippedArmorId = equipmentId;
            }
        }

        private static int Attack(AdventureEquipmentConfig config, int rank) => config.Attack + (rank - 1) * config.AttackPerRank;

        private static int Health(AdventureEquipmentConfig config, int rank) => config.Health + (rank - 1) * config.HealthPerRank;

        private static long UpgradeCost(AdventureEquipmentConfig config, int rank) => (long)config.UpgradeBaseCost * rank;
    }
}
