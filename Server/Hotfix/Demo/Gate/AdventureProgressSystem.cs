using System;

namespace ET.Server
{
    public static class AdventureProgressSystem
    {
        public static AdventureChapterSnapshot CaptureChapter(AdventureChapterConfig chapter)
        {
            AdventureMapConfig map = AdventureMapConfigCategory.Instance.Get(chapter.MapId);
            AdventureEventConfig adventureEvent = AdventureEventConfigCategory.Instance.Get(chapter.EventId);
            return new AdventureChapterSnapshot
            {
                Id = chapter.Id,
                Version = chapter.Version,
                ChapterName = chapter.ChapterName,
                EnemyName = chapter.EnemyName,
                PlayerHealth = chapter.PlayerHealth,
                PlayerAttack = chapter.PlayerAttack,
                SupplyAttackBonus = chapter.SupplyAttackBonus,
                EnemyHealth = chapter.EnemyHealth,
                EnemyAttack = chapter.EnemyAttack,
                RoundLimit = chapter.RoundLimit,
                RewardCoins = chapter.RewardCoins,
                Scene = new AdventureSceneSnapshot
                {
                    ResourceKey = map.ResourceKey,
                    TravelText = map.TravelText,
                    EncounterText = map.EncounterText,
                    EventTitle = adventureEvent.Title,
                    EventText = adventureEvent.Description,
                    TakeLabel = adventureEvent.TakeLabel,
                    SkipLabel = adventureEvent.SkipLabel
                }
            };
        }

        public static bool IsReplay(AdventureProgress progress, AdventureAction action, long expectedRevision, int chapterId = 0, int equipmentId = 0, int targetId = 0)
        {
            return action != AdventureAction.Snapshot && progress.Revision > 0 &&
                expectedRevision == progress.Revision - 1 && progress.LastAction == action &&
                (action != AdventureAction.Begin || chapterId == 0 || progress.Chapter?.Id == chapterId) &&
                (!AdventureEquipmentSystem.IsEquipmentAction(action) || progress.LastEquipmentId == equipmentId) &&
                (!UsesTarget(action) || progress.LastTargetId == targetId);
        }

        public static string Apply(AdventureProgress progress, AdventureAction action, long expectedRevision,
            AdventureChapterSnapshot defaultChapter, long nextRunId, int equipmentId = 0, int targetId = 0, long utcDay = 0)
        {
            if (action == AdventureAction.Snapshot)
            {
                return null;
            }

            if (expectedRevision != progress.Revision)
            {
                return "进度已更新，请确认当前状态后再操作。";
            }

            switch (progress.Phase, action)
            {
                case (AdventurePhase.Camp, AdventureAction.Begin):
                case (AdventurePhase.Camp, AdventureAction.BeginChallenge):
                    if (action == AdventureAction.BeginChallenge)
                    {
                        string challengeError = AdventureChallengeSystem.Enter(progress, defaultChapter, targetId, utcDay);
                        if (challengeError != null) return challengeError;
                    }
                    progress.RunId = nextRunId;
                    progress.Chapter = defaultChapter;
                    AdventureEquipmentSystem.ApplyBonuses(progress, progress.Chapter);
                    AdventureGrowthSystem.ApplyBonuses(progress, progress.Chapter);
                    progress.Phase = AdventurePhase.Trail;
                    break;
                case (AdventurePhase.Camp, AdventureAction.BuyEquipment or AdventureAction.Equip or AdventureAction.Unequip or AdventureAction.UpgradeEquipment):
                    string equipmentError = AdventureEquipmentSystem.Apply(progress, action, equipmentId);
                    if (equipmentError != null) return equipmentError;
                    break;
                case (AdventurePhase.Camp, AdventureAction.TrainTalent or AdventureAction.RecruitCompanion or
                    AdventureAction.AssignCompanion or AdventureAction.DismissCompanion):
                    string growthError = AdventureGrowthSystem.Apply(progress, action, targetId);
                    if (growthError != null) return growthError;
                    break;
                case (AdventurePhase.Trail, AdventureAction.Advance):
                    progress.Phase = AdventurePhase.Event;
                    break;
                case (AdventurePhase.Event, AdventureAction.TakeSupplies):
                case (AdventurePhase.Event, AdventureAction.SkipSupplies):
                    progress.TookSupplies = action == AdventureAction.TakeSupplies;
                    progress.Phase = AdventurePhase.Battle;
                    break;
                case (AdventurePhase.Battle, AdventureAction.Fight):
                    ResolveBattle(progress);
                    progress.Phase = AdventurePhase.Result;
                    break;
                case (AdventurePhase.Result, AdventureAction.Settle):
                    int reward = progress.Won ? progress.Chapter.RewardCoins : 0;
                    progress.Coins += reward;
                    progress.LastSettlement = new AdventureSettlement { RunId = progress.RunId, Won = progress.Won, RewardCoins = reward };
                    ReturnToCamp(progress);
                    break;
                case (AdventurePhase.Trail or AdventurePhase.Event or AdventurePhase.Battle, AdventureAction.Abandon):
                    ReturnToCamp(progress);
                    break;
                default:
                    return "当前阶段不能执行此操作。";
            }

            progress.LastAction = action;
            progress.LastEquipmentId = AdventureEquipmentSystem.IsEquipmentAction(action) ? equipmentId : 0;
            progress.LastTargetId = UsesTarget(action) ? targetId : 0;
            ++progress.Revision;
            return null;
        }

        private static bool UsesTarget(AdventureAction action) => action == AdventureAction.BeginChallenge || AdventureGrowthSystem.IsGrowthAction(action);

        private static void ReturnToCamp(AdventureProgress progress)
        {
            progress.RunId = 0;
            progress.Chapter = null;
            progress.TookSupplies = false;
            progress.Won = false;
            progress.Rounds.Clear();
            progress.Phase = AdventurePhase.Camp;
        }

        private static void ResolveBattle(AdventureProgress progress)
        {
            AdventureChapterSnapshot chapter = progress.Chapter;
            int playerHealth = chapter.PlayerHealth;
            int enemyHealth = chapter.EnemyHealth;
            int playerAttack = chapter.PlayerAttack + (progress.TookSupplies ? chapter.SupplyAttackBonus : 0);

            for (int round = 1; round <= chapter.RoundLimit && playerHealth > 0 && enemyHealth > 0; ++round)
            {
                int playerDamage = Math.Min(playerAttack, enemyHealth);
                enemyHealth -= playerDamage;
                int companionDamage = round % 2 == 0 ? Math.Min(chapter.CompanionAttack, enemyHealth) : 0;
                enemyHealth -= companionDamage;
                int enemyDamage = enemyHealth > 0 ? Math.Min(chapter.EnemyAttack, playerHealth) : 0;
                playerHealth -= enemyDamage;

                progress.Rounds.Add(new AdventureRoundState
                {
                    Number = round,
                    PlayerDamage = playerDamage + companionDamage,
                    CompanionDamage = companionDamage,
                    EnemyDamage = enemyDamage,
                    PlayerHealth = playerHealth,
                    EnemyHealth = enemyHealth
                });
            }

            progress.Won = enemyHealth == 0;
        }
    }
}
