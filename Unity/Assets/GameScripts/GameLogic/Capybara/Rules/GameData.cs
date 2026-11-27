using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if !CAPYBARA_SERVER
using UnityEngine;
#endif

namespace CapybaraRebuild
{
    [Serializable]
    public sealed class GameData
    {
        public Chapter[] chapters;
        public Chapter[] dailyRoute;
        public WheelDefinition[] wheels;
        public ChapterMap[] maps;
        public RecordedOpening recordedOpening;
        public StageGate damageStatisticsUnlock;
        public EnemyRetry enemyRetry;
        public Challenge[] challenges;
        public ChallengeOffer[] challengeOffers;
        public TowerChest[] towerChests;
        public EnemyNumberAttributes[] enemyNumberAttributes;
        public string rogueWaveGrowth;
        public int angelRecoveryPercent;
        public Talent[] talents;
        public TalentRank[] talentRanks;
        public Relic[] relics;
        public ActivityReward[] activity;
        public TalentMilestone[] talentMilestones;
        public EventType[] events;
        public EventResource[] resources;
        public Monster[] monsters;
        public Member[] members;
        public Skill[] builds;
        public int[] skillRefreshWeights;
        public Drop[] drops;
        public Item[] items;
        public Pet[] pets;
        public Stat[] stats;
        public Experience[] experience;
        public Upgrade[] upgrades;
        public Gear[] gear;
        public CampCatalog campCatalog;
        public OfflineJourneyStops offlineJourneyStops;
        public JourneyEventModels journeyEventModels;

#if !CAPYBARA_SERVER
        public static GameData Load() => AdventureCatalog.Load().Data;
#endif
    }

    [Serializable] public sealed class Chapter
    {
        public string name, attributes, normalBattleAttr, eliteBattleAttr, bossBattleAttr, npcBattleAttr;
        public int id, totalStage, maxRound, mapId, stageUpgradeGroup, speedAdd, stamina;
        public int[] difficultStage, startEvent, normalEvent, monsterGroup, rewardStage, staminaReturn, dropID, addSkills;
        public Range[] gold, journeyGold;
        public int journeyStage, sweepCost;
        public QueueBand[] queue;
    }
    [Serializable] public sealed class StageGate { public int chapter, stage; }
    [Serializable] public sealed class OfflineJourneyStops
    {
        public int minimumChapterDays, firstDay, lastDayPercent;
        public int[] fishingMaps;
    }
    [Serializable] public sealed class JourneyEventModels
    {
        public int columns, rows;
        public JourneyModelBinding[] bindings;
        public JourneyModel[] models;
        public int[] seaMapIds;

        public bool UsesSeaOffsets(int mapId) => seaMapIds!=null && Array.IndexOf(seaMapIds,mapId)>=0;
    }
    [Serializable] public sealed class JourneyModelBinding { public int resourceId, modelId; }
    [Serializable] public sealed class JourneyModel
    {
        public int id, cell, sourceModelId;
        public string art;
        public float landOffset, seaOffset, landOffsetX, seaOffsetX;
        public JourneyModelBounds worldBounds, artBounds;
    }
    [Serializable] public sealed class JourneyModelBounds
    {
        public float x, y, width, height;
        public bool HasArea => width>0 && height>0;
    }
    [Serializable] public sealed class EnemyRetry { public string attributes; public int[] thresholds; public int maximum; }
    [Serializable] public sealed class ChapterProgress { public int chapter, stage, attempts; }
    [Serializable] public sealed class Drop { public int id; public Reward[] rewards; }
    [Serializable] public sealed class Pet { public int id, memberId; public string name; }
    [Serializable] public sealed class Item { public int id; public string name; }
    [Serializable] public sealed class Reward { public int id, count, rate; }
    [Serializable] public sealed class CampCatalog
    {
        public EquipmentItem[] equipment;
        public EquipmentChest[] chests;
        public Drop[] pools;
        public int[] diamonds;
        public CampGift[] gifts,chapterPacks;
        public StageGate gemShopUnlock;
    }
    [Serializable] public sealed class EquipmentItem
    {
        public int id, type, quality;
        public string name, icon, attributes;
    }
    [Serializable] public sealed class EquipmentChest
    {
        public int id, priceId, singlePrice, tenPrice, singlePriceOrigin, freeTimes, quickDraw, limitTimes, gold;
        public int[] first;
        public Reward[] rates;
    }
    [Serializable] public sealed class CampGift { public int id, type, chapter; public Reward[] rewards; }
    [Serializable] public sealed class EquipmentChestProgress { public int id, draws, freeUses; public string freeDay; }
    [Serializable] public sealed class Challenge
    {
        public int mode, track, id, level, unlock, fee, mapId, artMapId, firstSkill;
        public int ticketId, ticketLimit;
        public bool legacyAvailable;
        public int[] rewardStage, dropID;
        public string attributes, normalBattleAttr, eliteBattleAttr, bossBattleAttr;
        public string name;
        public ChallengeWave[] waves;
        public Reward[] prizes;
    }
    [Serializable] public sealed class ActivityReward { public int id,threshold; public Reward[] prizes; }
    [Serializable] public sealed class Relic { public int id,kind,token; public string name,attributes; }
    [Serializable] public sealed class Talent { public int id, levels, cost, experience, type; public string attributes, completion, name, description; }
    [Serializable] public sealed class TalentRank { public int first, last; public string name; }
    [Serializable] public sealed class TalentMilestone { public int level, kind, evolution; public string reward, icon, name, description; }
    [Serializable] public sealed class ChallengeWave
    {
        public int[] members, groups;
        public int eventId, afterSkill;
        public ChallengeOption[] options;
        public string attrs, growth;
        public Reward[] prizes;
    }
    [Serializable] public sealed class ChallengeOption { public int id, type, weight, memberId, eventPointId, buildId; }
    [Serializable] public sealed class ChallengeOffer { public int id, count, picks, rounds, source; public bool season; }
    [Serializable] public sealed class TowerChest { public int tower,floors; public string name; public Reward reward; }
    [Serializable] public sealed class EnemyNumberAttributes { public int count; public int[] normalAttr,eliteAttr,bossAttr; }
    [Serializable] public sealed class ChallengeProgress
    {
        public int mode, track, wins;
        public bool hasClearIds;
        public List<int> clearedLevels;
    }
    [Serializable] public sealed class Range { public int min, max; }
    [Serializable] public sealed class QueueBand { public int first, last; public QueueEntry[] entries; }
    [Serializable] public sealed class QueueEntry { public int id, count; }
    [Serializable] public sealed class EventType { public int id, kind; public int[] resources; }
    [Serializable] public sealed class EventResource
    {
        public int id, weight, wheelId, fortuneType;
        public string path, wheelStory, wheelAction;
        public int[] battleGroups;
        public bool multiWave;
    }
    [Serializable] public sealed class Monster { public int id, group, battleType, difficult, enterBattleMode; public float baseSpeed; public int[] members, slots; }
    [Serializable] public sealed class Member
    {
        public int id, modelId, roleType;
        public string name, attributes;
        public int[] dropExp;
        public float modelScale, meleeOffset;
        public bool rangedAttack;
    }
    [Serializable]
    public sealed class WheelDefinition
    {
        public int id;
        public float offsetAngle;
        public WheelReward[] rewards;

        public float SectorCenter(int index)
        {
            int total = rewards.Sum(reward => reward.showWeight);
            int previous = rewards.Take(index).Sum(reward => reward.showWeight);
            return 90f - (previous + rewards[index].showWeight * .5f) * 360f / total;
        }

        internal float StopAngle(int index)
        {
            const float turn=360;
            float delta=offsetAngle-(90-SectorCenter(index));
            return offsetAngle-4*turn-(delta-turn*(float)Math.Floor(delta/turn));
        }
    }

    [Serializable]
    public sealed class WheelReward
    {
        public int id, weight, showWeight, kind, value, color;
        public string target, label, result, icon, textColor;
        public int textStyle;
    }

    [Serializable]
    public sealed class RecordedOpening
    {
        public int chapter, wheelDay, wheelEvent, wheelResource, wheelReward, battleDay, battleEvent;
    }

    [Serializable]
    public sealed class ChapterMap
    {
        public int id;
        public string groundColor;
        public int[] sky, far, middle, ground;
        public float farSpeed, cloudSpeed;
        public bool hasArt;
    }

    [Serializable] public sealed class Skill
    {
        public int id, groupId, level, quality, weight, unlockChapter, skillId, recoverHp, initShield;
        public int kind, heroSource, iconAtlasId, badgeAtlasId, demonCost;
        public int[] source, compose, deleteGroup, outsideSource;
        public string name, attributes, skillAttributes, description, upgradeDescription, fullDescription, needGroup, icon, badge;
    }
    [Serializable] public sealed class Stat { public int id, weight; public string attributes; }
    [Serializable] public sealed class Gear { public int id; public string name, attributes; }
    [Serializable] public sealed class Experience { public int id, lv, groupId, expToNext; }
    [Serializable] public sealed class Upgrade { public int id, group, attackUpgrade, hpUpgrade; public string attr; }

    internal sealed class OriginalRandom
    {
        private ulong state;

        internal OriginalRandom(int seed) => state=unchecked((ulong)(long)seed);

        internal int NextInt() => unchecked((int)Step(ref state));

        internal int Range(int min,int max) => min>=max-1?min:min+(int)(Step(ref state)%(uint)(max-min));

        internal static uint Step(ref ulong state)
        {
            state=unchecked(state*25214903917UL+11UL)&((1UL<<48)-1);
            return (uint)(state>>16);
        }
    }

    public sealed class SeededRandom
    {
        private uint state;
        public SeededRandom(int seed, int stage, int stream)
        {
            unchecked { state = (uint)seed ^ ((uint)stage * 2654435761u) ^ ((uint)stream * 2246822519u); }
            if (state == 0) state = 0x9E3779B9;
        }

        public int Next(int exclusive)
        {
            if (exclusive <= 0) throw new ArgumentOutOfRangeException(nameof(exclusive));
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)exclusive);
        }

        public T Weighted<T>(IReadOnlyList<T> items, Func<T, int> weight)
        {
            if (items.Count == 0) return default;
            var total = items.Sum(item => Math.Max(1, weight(item)));
            var roll = Next(total);
            foreach (var item in items)
            {
                roll -= Math.Max(1, weight(item));
                if (roll < 0) return item;
            }
            throw new InvalidOperationException("Invalid weighted pool.");
        }
    }

    public sealed class Attributes
    {
        private readonly Dictionary<string,float> values = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);

        public void Add(string source) => AddScaled(source, 1);

        internal void AddScaled(string source,float multiplier)
        {
            foreach (var term in (source ?? "").Split('|'))
            {
                var pair = term.Split('=');
                if (pair.Length != 2 || !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
                string key=pair[0].Trim();
                values[key] = Get(key) + value * multiplier;
            }
        }

        public float Get(string key) => values.TryGetValue(key, out var value) ? value : 0;
        internal void Add(string key,float amount) => values[key] = Get(key) + amount;
        public int Stat(string key) => (int)Math.Round(Get(key) * Math.Max(0, 1 + Get(key + "%") / 100));

        internal void ConvertEnemyBase()
        {
            Convert("Attack");
            Convert("HPMax");
            Convert("Defence");

            void Convert(string key)
            {
                values[key]=Get(key)*Math.Max(0,1+Get(key+"%")/100)*Math.Max(0,1+Get("Global"+key+"%")/100);
                values.Remove(key+"%");
                values.Remove("Global"+key+"%");
            }
        }

        internal int EnemyStat(string key,double factor=1)
        {
            double amount=Get(key)*factor;
            return amount>=int.MaxValue?int.MaxValue:amount<=0?0:(int)Math.Round(amount,MidpointRounding.ToEven);
        }
    }
}
