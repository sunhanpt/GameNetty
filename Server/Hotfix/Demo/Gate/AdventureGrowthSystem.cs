namespace ET.Server
{
    public static class AdventureGrowthSystem
    {
        public static bool IsGrowthAction(AdventureAction action)
        {
            return action is AdventureAction.TrainTalent or AdventureAction.RecruitCompanion or
                AdventureAction.AssignCompanion or AdventureAction.DismissCompanion;
        }

        public static string Apply(AdventureProgress progress, AdventureAction action, int targetId)
        {
            AdventureGrowthConfig config = AdventureGrowthConfigCategory.Instance.GetOrDefault(targetId);
            AdventureGrowthKind requiredKind = action == AdventureAction.TrainTalent ? AdventureGrowthKind.Talent : AdventureGrowthKind.Companion;
            if (config == null || config.Kind != (int)requiredKind)
            {
                return "所选成长项目不可用。";
            }

            AdventureGrowthRank owned = progress.Growth.Find(item => item.Id == targetId);
            int rank = owned?.Rank ?? 0;
            switch (action)
            {
                case AdventureAction.TrainTalent:
                case AdventureAction.RecruitCompanion:
                    if (rank >= config.MaxRank || (action == AdventureAction.RecruitCompanion && rank > 0))
                    {
                        return action == AdventureAction.TrainTalent ? "天赋已满级。" : "这位旅伴已招募。";
                    }
                    long cost = Cost(config, rank);
                    if (cost < 0 || progress.Coins < cost) return "金币不足，当前无法培养。";
                    progress.Coins -= cost;
                    if (owned == null)
                    {
                        progress.Growth.Add(new AdventureGrowthRank { Id = targetId, Rank = 1 });
                    }
                    else
                    {
                        ++owned.Rank;
                    }
                    break;
                case AdventureAction.AssignCompanion:
                    if (rank == 0) return "请先招募这位旅伴。";
                    if (progress.AssignedCompanionId == targetId) return "这位旅伴已随行。";
                    progress.AssignedCompanionId = targetId;
                    break;
                case AdventureAction.DismissCompanion:
                    if (progress.AssignedCompanionId != targetId) return "这位旅伴当前未随行。";
                    progress.AssignedCompanionId = 0;
                    break;
                default:
                    return "未知成长操作。";
            }
            return null;
        }

        public static void ApplyBonuses(AdventureProgress progress, AdventureChapterSnapshot chapter)
        {
            foreach (AdventureGrowthRank owned in progress.Growth)
            {
                AdventureGrowthConfig config = AdventureGrowthConfigCategory.Instance.GetOrDefault(owned.Id);
                if (config == null)
                {
                    continue;
                }
                if ((AdventureGrowthKind)config.Kind == AdventureGrowthKind.Companion)
                {
                    if (progress.AssignedCompanionId != owned.Id) continue;
                    chapter.CompanionAttack = config.AttackPerRank * owned.Rank;
                    chapter.CompanionName = config.Name;
                }
                else
                {
                    chapter.PlayerAttack += config.AttackPerRank * owned.Rank;
                }
                chapter.PlayerHealth += config.HealthPerRank * owned.Rank;
            }
        }

        public static AdventureGrowthOption Describe(AdventureProgress progress, AdventureGrowthConfig config)
        {
            int rank = progress.Growth.Find(item => item.Id == config.Id)?.Rank ?? 0;
            AdventureGrowthOption option = AdventureGrowthOption.Create();
            option.Id = config.Id;
            option.Name = config.Name;
            option.Kind = (AdventureGrowthKind)config.Kind;
            option.Rank = rank;
            option.MaxRank = config.MaxRank;
            option.Cost = rank < config.MaxRank ? Cost(config, rank) : 0;
            option.AttackPerRank = config.AttackPerRank;
            option.HealthPerRank = config.HealthPerRank;
            option.Assigned = option.Kind == AdventureGrowthKind.Companion && progress.AssignedCompanionId == config.Id;
            return option;
        }

        private static long Cost(AdventureGrowthConfig config, int rank) => (long)config.BaseCost * (rank + 1);
    }
}
