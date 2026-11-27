namespace ET.Server
{
    public static class AdventureChallengeSystem
    {
        public static string Enter(AdventureProgress progress, AdventureChapterSnapshot chapter, int challengeId, long utcDay)
        {
            AdventureChallengeConfig config = AdventureChallengeConfigCategory.Instance.GetOrDefault(challengeId);
            if (config == null || chapter == null || config.ChapterId != chapter.Id || config.DailyAttempts <= 0 || config.RewardCoins < 0)
            {
                return "所选挑战不可用。";
            }
            if (progress.LastSettlement == null) return "完成一次普通冒险结算后解锁。";

            AdventureChallengeUsage usage = progress.Challenges.Find(item => item.Id == challengeId);
            int attempts = AttemptsToday(usage, utcDay);
            if (attempts >= config.DailyAttempts) return "今日挑战次数已用完。";

            if (usage == null)
            {
                usage = new AdventureChallengeUsage { Id = challengeId, UtcDay = utcDay };
                progress.Challenges.Add(usage);
            }
            else if (usage.UtcDay < utcDay)
            {
                usage.UtcDay = utcDay;
            }
            usage.Attempts = attempts + 1;
            chapter.ChallengeId = challengeId;
            chapter.ChapterName = config.Name;
            chapter.EnemyHealth += config.EnemyHealthBonus;
            chapter.EnemyAttack += config.EnemyAttackBonus;
            chapter.RewardCoins = config.RewardCoins;
            return null;
        }

        public static AdventureChallengeOption Describe(AdventureProgress progress, AdventureChallengeConfig config, long utcDay)
        {
            AdventureChallengeOption option = AdventureChallengeOption.Create();
            option.Id = config.Id;
            option.Name = config.Name;
            AdventureChapterConfig chapter = AdventureChapterConfigCategory.Instance.Get(config.ChapterId);
            option.ResourceKey = AdventureMapConfigCategory.Instance.Get(chapter.MapId).ResourceKey;
            option.RewardCoins = config.RewardCoins;
            option.DailyAttempts = config.DailyAttempts;
            option.AttemptsLeft = System.Math.Max(0, config.DailyAttempts - AttemptsToday(progress.Challenges.Find(item => item.Id == config.Id), utcDay));
            option.Unlocked = progress.LastSettlement != null;
            return option;
        }

        private static int AttemptsToday(AdventureChallengeUsage usage, long utcDay) => usage != null && usage.UtcDay >= utcDay ? usage.Attempts : 0;
    }
}
