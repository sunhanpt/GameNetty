using System;
using System.Collections.Generic;
using System.Linq;

namespace CapybaraRebuild
{
    public sealed partial class Adventure
    {
        internal static readonly string[] TalentAttributes = { "HPMax", "Attack", "Defence" };
        internal IReadOnlyList<Talent> TalentStages => data.talents ?? Array.Empty<Talent>();
        internal IReadOnlyList<TalentRank> TalentRanks => data.talentRanks ?? Array.Empty<TalentRank>();
        internal IReadOnlyList<TalentMilestone> TalentMilestones => data.talentMilestones ?? Array.Empty<TalentMilestone>();
        internal Talent CurrentTalent => FindTalent(out _);

        private bool HasIndividualTalentLevels => Profile.talentAttributeLevels?.Length == TalentAttributes.Length;

        private Talent FindTalent(out int levelInStage)
        {
            levelInStage = Math.Max(0, Profile.talentLevel);
            foreach (var stage in TalentStages)
            {
                if (levelInStage < stage.levels) return stage;
                levelInStage -= stage.levels;
            }
            return null;
        }

        internal int TalentAttributeLevel(int attribute)
        {
            var stage = FindTalent(out int shared);
            if (stage == null) return TalentStages.LastOrDefault()?.levels ?? 0;
            return HasIndividualTalentLevels
                ? Math.Clamp(Profile.talentAttributeLevels[attribute], 0, stage.levels)
                : shared;
        }

        internal int TalentExperience
        {
            get
            {
                var stage = FindTalent(out int shared);
                if (stage == null) return TalentStages.Sum(talent => talent.levels) * TalentAttributes.Length;
                if (!HasIndividualTalentLevels) return Math.Max(0, Profile.talentLevel) * TalentAttributes.Length;
                return stage.experience + Profile.talentAttributeLevels.Sum(level => Math.Clamp(level, 0, stage.levels));
            }
        }

        public int TalentCost => CurrentTalent?.cost ?? 0;
        public bool CanTrainTalent => Run == null && TalentCost > 0 && Profile.gold >= TalentCost;

        // Existing callers can keep training all attributes together while using a legacy save.
        public bool TrainTalent()
        {
            if (!CanTrainTalent) return false;
            if (HasIndividualTalentLevels)
            {
                var stage = CurrentTalent;
                for (int i = 0; i < TalentAttributes.Length; i++)
                    if (TalentAttributeLevel(i) < stage.levels) return TrainTalentAttribute(i);
                return false;
            }

            Profile.gold -= TalentCost;
            Profile.talentLevel++;
            GrantTalentRewards(Profile.talentLevel - 1, Profile.talentLevel);
            Commit();
            return true;
        }

        internal bool TrainTalentAttribute(int attribute)
        {
            if ((uint)attribute >= (uint)TalentAttributes.Length || !CanTrainTalent || !CampTalentUnlocked) return false;
            var stage = FindTalent(out int shared);
            if (TalentAttributeLevel(attribute) >= stage.levels) return false;

            int previousExperience = TalentExperience;
            if (!HasIndividualTalentLevels)
            {
                Profile.talentAttributeLevels = Enumerable.Repeat(shared, TalentAttributes.Length).ToArray();
                Profile.talentLevel -= shared;
            }
            Profile.gold -= stage.cost;
            Profile.talentAttributeLevels[attribute]++;
            if (Profile.talentAttributeLevels.All(level => level >= stage.levels))
            {
                Profile.talentLevel += stage.levels;
                Array.Clear(Profile.talentAttributeLevels, 0, TalentAttributes.Length);
            }
            GrantTalentRewards(previousExperience, TalentExperience);
            Commit();
            return true;
        }

        internal static IEnumerable<Reward> TalentItems(TalentMilestone milestone)
        {
            if (milestone.kind != 1) yield break;
            foreach (string term in (milestone.reward ?? "").Split('|'))
            {
                var parts = term.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int item) && item > 0 &&
                    int.TryParse(parts[1], out int count) && count > 0)
                    yield return new Reward { id = item, count = count };
            }
        }

        private void GrantTalentRewards(int previousExperience, int experience)
        {
            foreach (var milestone in TalentMilestones)
            {
                if (milestone.level <= previousExperience || milestone.level > experience) continue;
                foreach (var reward in TalentItems(milestone))
                {
                    if (reward.id == 1) Profile.gold += reward.count;
                    else if (reward.id == 9) Profile.stamina = Math.Min(30, Profile.stamina + reward.count);
                    else AddReward(Profile.inventory, reward);
                }
            }
        }

        private void AddTalentBonuses(Attributes result)
        {
            var current = FindTalent(out int shared);
            foreach (var stage in TalentStages)
            {
                if (stage == current)
                {
                    if (!HasIndividualTalentLevels) result.AddScaled(stage.attributes, shared);
                    else
                    {
                        var increments = new Attributes();
                        increments.Add(stage.attributes);
                        for (int i = 0; i < TalentAttributes.Length; i++)
                        {
                            string key = TalentAttributes[i];
                            result.Add(key, increments.Get(key) * TalentAttributeLevel(i));
                        }
                    }
                    break;
                }
                result.AddScaled(stage.attributes, stage.levels);
                result.Add(stage.completion);
            }

            int experience = HasIndividualTalentLevels ? TalentExperience : Profile.talentLevel;
            foreach (var milestone in TalentMilestones)
                if ((milestone.kind == 2 || milestone.kind == 3) && milestone.level <= experience)
                    result.Add(milestone.reward);
        }
    }
}
