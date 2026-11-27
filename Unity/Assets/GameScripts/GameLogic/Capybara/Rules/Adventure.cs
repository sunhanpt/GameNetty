using System;
using System.Collections.Generic;
using System.Linq;
#if !CAPYBARA_SERVER
using UnityEngine;
#endif

namespace CapybaraRebuild
{
    public enum Phase { Camp, Trail, Event, Skill, Battle, Result }
    public enum SkillOfferOrigin { Unspecified, Begin, Event, LevelUp }
    public enum JournalFortune { None, Lucky, Great, Unlucky }

    [Serializable]
    public sealed class Profile
    {
        public int gold, highest = 1, stamina = 30, gearLevel, petId, forgeLevel, talentLevel;
        public int[] talentAttributeLevels;
        public int bestDay, campChapter;
        public bool chestTabUnlocked;
        public bool damageStatisticsUnlocked;
        public List<ChallengeProgress> challenges = new List<ChallengeProgress>();
        public List<ChapterProgress> chapterProgress = new List<ChapterProgress>();
        public string lastDailyClaim, activityDate;
        public string dailyRewardDate;
        public string dungeonTicketDate;
        public List<Reward> dungeonTicketsUsed;
        public int dailyRewardStage;
        public int claimedRewardTowerId;
        public int activityPoints;
        public List<int> activityClaimed = new List<int>();
        public int mountId, artifactId;
        public List<int> ownedRelics = new List<int>();
        public int[] wornEquipment;
        public List<EquipmentChestProgress> equipmentChestProgress;
        public List<int> cleared = new List<int>(); public List<Reward> inventory = new List<Reward>();
    }

    [Serializable]
    public sealed class Run
    {
        public int chapter, seed, stage, hp, gold, experience, level = 1, eventId, resourceId, stat, round;
        public int milestoneGold, refundedStamina, monsterId, eventKind, shield, recharge;
        public Phase phase;
        public bool won, skillAtStart, revived, sweep, replayOpening, legacyQueue, wheelOpened;
        public int[] eventQueue;
        public int wheelId, pendingWheelRewardId;
        public int waveIndex, challengeMode, challengeId, extraStartChoices;
        public int challengeOfferId, offerRoundsLeft, offerPicksLeft, challengeOptionType;
        public bool challengeBoxOpened;
        public int offlineStopDay, offlineStopKind;
        public SkillOfferOrigin skillOfferOrigin;
        public int skillRefreshCount;
        public string dailyDate;
        public bool dailyLoop;
        public int dailySegment, dailySegmentStart;
        public List<int> pendingWaves = new List<int>();
        public List<string> eventAttributes = new List<string>();
        public List<int> skills = new List<int>();
        public List<int> choices = new List<int>();
        public List<int> claimed = new List<int>(); public List<Reward> loot = new List<Reward>();
        public List<Fighter> enemies = new List<Fighter>();
        public List<BattleStatistic> battleStatistics = new List<BattleStatistic>();
        public BattleReward pendingBattleReward;
        public List<BattleReward> battleRewards = new List<BattleReward>();
        public List<string> journal = new List<string>();
        public List<int> journalDays = new List<int>();
        public List<JournalFortune> journalFortunes = new List<JournalFortune>();
        public List<JournalModifier> journalModifiers = new List<JournalModifier>();
        public int journalSerial;
    }

    [Serializable] public sealed class Fighter { public string name; public int memberId, slot, hp, maxHp, attack, defense, poison, burn, bleed, poisonTurns, burnTurns, bleedTurns, stunTurns; }
    [Serializable] public sealed class BattleReward { public int day, experience, gold; }
    [Serializable] public sealed class JournalModifier { public int serial; public string attributes, skillName; }

    [Serializable]
    public sealed class Save
    {
        public int version = 1;
        public Profile profile = new Profile();
        public Run run;
    }

    public sealed partial class Adventure
    {
        internal const string JournalAttributePrefix = "获得属性加成 · ";
        private readonly GameData data;
        private readonly Dictionary<int,EventType> types;
        private readonly Dictionary<int,EventResource> resources;
        private readonly Dictionary<int,Member> members;
        private readonly Dictionary<int,Skill> skills;
        private readonly Action<Save> persist;
        private readonly Func<DateTime> localDate;
        private readonly Dictionary<(int mode,int id),Challenge> challenges;
        private readonly ILookup<int,Monster> formationGroups;
        public Save Save { get; }
        public Run Run => Save.run;
        public Profile Profile => Save.profile;
        public Chapter Chapter => Run == null ? null : Run.dailyLoop ? data.dailyRoute[Run.dailySegment] :
            data.chapters.FirstOrDefault(row => row.id == Run.chapter);
        public Phase Phase => Run?.phase ?? Phase.Camp;
        public bool CampShopUnlocked => Profile.highest>1 || Profile.bestDay>=39;
        public bool CampTalentUnlocked => Profile.highest>1 || Profile.bestDay>=1;
        public bool CampChestsUnlocked => Profile.chestTabUnlocked || Profile.highest>3 || Profile.cleared.Contains(3);
        public bool DamageStatisticsUnlocked => Profile.damageStatisticsUnlocked;
        public bool HasCampChests => Profile.inventory.Any(item=>IsCampChest(item.id) && item.count>0);
        public static bool IsCampChest(int id) => id>=71 && id<=75;
        public Challenge ActiveChallenge => Run!=null && challenges.TryGetValue((Run.challengeMode,Run.challengeId),out var value) ? value : null;
        internal bool UsesJourneyMovement => Run!=null && !Run.sweep && Run.challengeMode is 0 or 3 or 4 or 6 or 8;
        internal bool IsBattlePrompt => Phase==Phase.Event && Run.eventKind is >=3 and <=5 && !HasWheel;
        public int StageCount => Run.dailyLoop ? Math.Max(ActiveChallenge.rewardStage.Last(),Run.dailySegmentStart+Chapter.totalStage) :
            ActiveChallenge?.waves.Length ?? (Run.sweep ? Chapter.journeyStage : Chapter.totalStage);
        public int RoundLimit => ActiveChallenge!=null && !Run.dailyLoop ? 20 : Math.Max(1,Chapter.maxRound);
        public bool CanSettleDaily => Run?.dailyLoop==true && Phase==Phase.Trail && Run.stage>=ActiveChallenge.rewardStage.Last();
        public void SettleDaily()
        {
            if(!CanSettleDaily) return;
            End(true);
            Commit();
        }
        public SkillOfferOrigin CurrentSkillOffer => Phase!=Phase.Skill ? SkillOfferOrigin.Unspecified :
            Run.skillOfferOrigin!=SkillOfferOrigin.Unspecified ? Run.skillOfferOrigin :
            Run.skillAtStart ? SkillOfferOrigin.Begin : Run.eventKind==6 ? SkillOfferOrigin.Event : SkillOfferOrigin.LevelUp;

        public int SkillRefreshesRemaining => CurrentSkillOffer==SkillOfferOrigin.LevelUp && ActiveChallenge==null
            ? Math.Max(0,(int)Math.Floor(Player().Get("GetSelectSkillCountByUpgrade"))-Run.skillRefreshCount) : 0;

        public Adventure(GameData data, Save save, Action<Save> persist, Func<DateTime> localDate=null)
        {
            this.data = data;
            this.persist = persist;
            this.localDate=localDate ?? (()=>DateTime.Today);
            Save = save != null && save.version == 1 && save.profile != null ? save : new Save();
            Profile.highest=Math.Max(Profile.highest,data.chapters.Min(chapter=>chapter.id));
            challenges=(data.challenges ?? Array.Empty<Challenge>()).ToDictionary(c=>(c.mode,c.id));
            formationGroups=data.monsters.ToLookup(row=>row.group);
            Profile.challenges ??= new List<ChallengeProgress>();
            var hardProgress=Progress(3,0);
            if(hardProgress!=null && !hardProgress.hasClearIds)
            {
                hardProgress.clearedLevels=challenges.Values.Where(c=>c.mode==3 && c.legacyAvailable)
                    .OrderBy(c=>c.level).Take(hardProgress.wins).Select(c=>c.id).ToList();
                hardProgress.hasClearIds=true;
            }
            Profile.chapterProgress ??= new List<ChapterProgress>();
            Profile.ownedRelics ??= new List<int>();
            Profile.activityClaimed ??= new List<int>();
            Profile.cleared ??= new List<int>();
            Profile.inventory ??= new List<Reward>();
            if (Save.run != null && (Save.run.chapter <= 0 || Chapter == null)) Save.run = null;
            if(Run!=null && Run.challengeMode!=0 && ActiveChallenge==null) Save.run=null;
            if(Run!=null) RecordCampChestUnlock();
            RecordDamageStatisticsUnlock();
            if(Run!=null)
            {
                Run.battleStatistics ??= new List<BattleStatistic>();
                Run.battleRewards ??= new List<BattleReward>();
                RecordChapterStage();
                Run.pendingWaves ??= new List<int>();
                Run.journal ??= new List<string>();
                Run.journalFortunes ??= new List<JournalFortune>();
                if(Run.journalFortunes.Count>Run.journal.Count)
                    Run.journalFortunes.RemoveRange(Run.journal.Count,Run.journalFortunes.Count-Run.journal.Count);
                while(Run.journalFortunes.Count<Run.journal.Count)
                    Run.journalFortunes.Add(JournalFortune.None);
                Run.journalSerial=Math.Max(Run.journalSerial,Run.journal.Count);
                Run.journalModifiers ??= new List<JournalModifier>();
                if(Run.journalDays?.Count != Run.journal.Count)
                {
                    Run.journalDays = new List<int>(Run.journal.Count);
                    int stage = 0;
                    foreach(string line in Run.journal)
                    {
                        int end = line.StartsWith("抵达第",StringComparison.Ordinal) ? line.IndexOf(" 步",StringComparison.Ordinal) : -1;
                        if(end > 3 && int.TryParse(line.Substring(3,end-3),out int day)) stage = day;
                        Run.journalDays.Add(stage);
                    }
                }
            }
            types = data.events.GroupBy(r=>r.id).ToDictionary(g=>g.Key,g=>g.First());
            resources = data.resources.GroupBy(r=>r.id).ToDictionary(g=>g.Key,g=>g.First());
            members = data.members.GroupBy(r=>r.id).ToDictionary(g=>g.Key,g=>g.First());
            skills = data.builds.GroupBy(r=>r.id).ToDictionary(g=>g.Key,g=>g.First());
            InitializeEquipment();
            if(Run != null && ActiveChallenge == null && !Run.sweep && Run.eventQueue?.Length != Chapter.totalStage)
            {
                Run.eventQueue = BuildQueue(Chapter,Run.seed);
                Run.legacyQueue = true;
                if(Chapter.id == 1) MoveEvent(Run.eventQueue,401,7);
                if(Run.phase == Phase.Event) SetWheel();
            }
            else if(Run!=null && ActiveChallenge==null && !Run.sweep)
            {
                // Repair only upcoming early days whose explicit band was shadowed by the old start-event fill.
                int end=Math.Min(Chapter.startEvent.Length,Run.eventQueue.Length);
                int[] configured=null;
                for(int i=Math.Max(0,Run.stage);i<end;i++)
                {
                    if(Run.eventQueue[i]!=Chapter.startEvent[i]) continue;
                    configured??=BuildQueue(Chapter,Run.seed);
                    Run.eventQueue[i]=configured[i];
                }
            }
        }

        public IEnumerable<Chapter> Available => data.chapters.Where(row => row.id <= Profile.highest);
        internal Chapter CampChapter => Available.FirstOrDefault(row=>row.id==Profile.campChapter) ?? Available.LastOrDefault();
        internal bool CanStartChapter(Chapter chapter) => Run==null && chapter!=null &&
            chapter.id<=Profile.highest && Profile.stamina>=chapter.stamina;

        internal bool SelectCampChapter(int chapterId)
        {
            if(Run!=null || !Available.Any(chapter=>chapter.id==chapterId)) return false;
            if(Profile.campChapter==chapterId) return true;
            Profile.campChapter=chapterId;
            Commit();
            return true;
        }

        public Skill FindSkill(int id) => skills[id];
        internal string BattleSkillName(int id) => skills.TryGetValue(id,out var skill)?skill.name:null;
        internal int ChapterBestStage(Chapter chapter) => Profile.cleared.Contains(chapter.id)?chapter.totalStage:
            Profile.chapterProgress.FirstOrDefault(row=>row.chapter==chapter.id)?.stage??
            (chapter.id==1 && Profile.highest==1?Math.Min(Profile.bestDay,chapter.totalStage):0);
        internal Reward[] ChapterRewards(Chapter chapter,int stage)
        {
            int index=Array.IndexOf(chapter.rewardStage,stage);
            if(index<0 || index>=chapter.dropID.Length) return Array.Empty<Reward>();
            int dropId=chapter.dropID[index];
            return data.drops.FirstOrDefault(drop=>drop.id==dropId)?.rewards??Array.Empty<Reward>();
        }
        internal int SkillGroupMaxLevel(int groupId) => data.builds.Where(s=>s.groupId==groupId).Max(s=>s.level);
        public int NextExperience => data.experience.FirstOrDefault(r=>r.groupId==101 && r.lv==Run.level)?.expToNext ?? 100;
        public string ResourcePath => Run != null && resources.TryGetValue(Run.resourceId, out var resource) ? resource.path : "";

        public ChapterMap Map
        {
            get
            {
                int id = ActiveChallenge is Challenge c && c.mapId>0 ? c.mapId : (Chapter ?? CampChapter)?.mapId ?? 2008;
                return (data.maps ?? Array.Empty<ChapterMap>()).FirstOrDefault(map => map.id == id);
            }
        }

        public bool IsRecordedEncounter => Run?.replayOpening == true && Run.stage == data.recordedOpening?.battleDay;
        public int MemberModel(int id) => members.TryGetValue(id,out var member) ? member.modelId : 0;

        public int EnemyModel(Fighter enemy,int index)
        {
            var member=EnemyMember(enemy,index);
            if(member!=null) return member.modelId;
            return index==0 && Run.legacyQueue && Run.stage==7 && Run.chapter==1?220081:0;
        }

        internal Member EnemyMember(Fighter enemy,int index)
        {
            if(members.TryGetValue(enemy.memberId,out var member)) return member;
            // Old battle saves may only have the formation and list order.
            if(!Run.dailyLoop && ActiveChallenge is Challenge challenge)
            {
                var wave=challenge.waves[Run.stage-1];
                return index<wave.members.Length && members.TryGetValue(wave.members[index],out member)?member:null;
            }
            var formation=data.monsters.FirstOrDefault(row=>row.id==Run.monsterId);
            if(formation!=null && formation.members.Length==Run.enemies.Count)
            {
                int memberIndex=enemy.slot>0 && formation.slots!=null?Array.IndexOf(formation.slots,enemy.slot):index;
                if(memberIndex>=0 && memberIndex<formation.members.Length)
                    members.TryGetValue(formation.members[memberIndex],out member);
            }
            return member;
        }

        public bool Start(int chapter, int seed, bool replayOpening=false)
        {
            var row = Available.FirstOrDefault(c=>c.id==chapter);
            if (!CanStartChapter(row)) return false;
            Profile.stamina -= row.stamina;
            BeginChapter(row, seed, replayOpening);
            Commit();
            return true;
        }

        private void BeginChapter(Chapter row, int seed, bool replayOpening)
        {
            int chapter = row.id;
            ProgressForChapter(chapter).attempts++;
            var opening = data.recordedOpening;
            bool replay = replayOpening && opening?.chapter == chapter;
            var queue = BuildQueue(row,seed);
            if(replay)
            {
                MoveEvent(queue,opening.wheelEvent,opening.wheelDay);
                MoveEvent(queue,opening.battleEvent,opening.battleDay);
            }
            var stop = replay ? (day: 0, kind: 0) : PlanOfflineStop(row,queue,seed);
            Save.run = new Run
            {
                chapter = chapter, seed = seed, phase = Phase.Trail, eventQueue = queue, replayOpening = replay,
                offlineStopDay = stop.day, offlineStopKind = stop.kind
            };
            Run.hp = Player().Stat("HPMax");
            Run.extraStartChoices=Math.Max(0,Player().Stat("GetSkillCountByBegin"));
            Record("踏上旅程。");
            PrepareSkill(SkillOfferOrigin.Begin);
        }

        internal IEnumerable<(int id, string name)> GmLevels(int mode) =>
            mode == 0 ? data.chapters.Select(chapter => (chapter.id, chapter.name)) :
                challenges.Values.Where(challenge => challenge.mode == mode).Select(challenge => (challenge.id, challenge.name));

        // The caller retains this Adventure and uses the returned instance only for the test run.
#if !CAPYBARA_SERVER && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        internal Adventure CreateGmRun(int mode, int id, int seed)
        {
            var chapter = mode == 0 ? data.chapters.FirstOrDefault(row => row.id == id) : null;
            challenges.TryGetValue((mode, id), out var challenge);
            if (chapter == null && challenge == null) return null;

            var profile = JsonUtility.FromJson<Profile>(JsonUtility.ToJson(Profile));
            var test = new Adventure(data, new Save { profile = profile }, null, localDate);
            if (chapter != null)
                test.BeginChapter(chapter, seed, false);
            else
            {
                if (mode == 6)
                {
                    profile.dailyRewardDate = null;
                    profile.dailyRewardStage = 0;
                }
                test.BeginChallenge(challenge, seed);
            }
            return test;
        }

#endif
        public static string ChallengeName(int mode)
        {
            switch(mode)
            {
                case 1: return "试炼之塔";
                case 2: return "资源副本";
                case 3: return "困难章节";
                case 4: return "肉鸽秘境";
                case 5: return "首领试炼";
                case 6: return "每日远征";
                case 8: return "赛季副本";
                default: return "";
            }
        }

        public int ActivityPoints => Profile.activityDate==DayKey ? Profile.activityPoints : 0;
        public IEnumerable<ActivityReward> ActivityRewards => data.activity ?? Array.Empty<ActivityReward>();
        public bool ActivityClaimed(ActivityReward reward) => Profile.activityDate==DayKey && Profile.activityClaimed.Contains(reward.id);
        public bool CanClaimActivity(ActivityReward reward) => Run==null && ActivityPoints>=reward.threshold && !ActivityClaimed(reward);
        public bool ClaimActivity(int id)
        {
            var reward=ActivityRewards.FirstOrDefault(x=>x.id==id);
            if(reward==null || !CanClaimActivity(reward)) return false;
            Profile.activityClaimed.Add(id);
            foreach(var prize in reward.prizes) ApplyProfileReward(prize);
            Commit();
            return true;
        }

        private void ApplyProfileReward(Reward reward)
        {
            if(reward.id==1) Profile.gold+=reward.count;
            else if(reward.id==9) Profile.stamina=Math.Min(30,Profile.stamina+reward.count);
            else AddReward(Profile.inventory,reward);
        }

        private void AddActivity(int points)
        {
            string day=DayKey;
            if(Profile.activityDate!=day)
            {
                Profile.activityDate=day;
                Profile.activityPoints=0;
                Profile.activityClaimed.Clear();
            }
            Profile.activityPoints=Math.Min(100,Profile.activityPoints+points);
        }

        private string DayKey => localDate().ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);

        public IEnumerable<Challenge> ChallengeChoices(int mode)
        {
            if(mode==3)
            {
                var cleared=Progress(mode,0)?.clearedLevels;
                return challenges.Values.Where(c=>c.mode==mode && cleared?.Contains(c.id)!=true).OrderBy(c=>c.level).Take(1);
            }
            if(mode==6)
            {
                var daily=challenges.Values.Where(c=>c.mode==6).OrderBy(c=>c.id).ToArray();
                if(daily.Length==0 || Profile.lastDailyClaim==DayKey) return Array.Empty<Challenge>();
                return new[]{daily[(int)(localDate().Date.Ticks/TimeSpan.TicksPerDay%daily.Length)]};
            }
            return challenges.Values.Where(c=>c.mode==mode).GroupBy(c=>c.track).OrderBy(g=>g.Key)
                .Select(g=>g.OrderBy(c=>c.level).ThenBy(c=>c.id).Skip(Progress(mode,g.Key)?.wins ?? 0).FirstOrDefault())
                .Where(c=>c!=null);
        }

        private ChallengeProgress Progress(int mode,int track) =>
            Profile.challenges.FirstOrDefault(c=>c.mode==mode && c.track==track);

        public TowerChest NextTowerChest => data.towerChests.FirstOrDefault(chest=>chest.tower>Profile.claimedRewardTowerId);
        public bool CanClaimTowerChest => Run==null && NextTowerChest is TowerChest chest &&
            (Progress(1,0)?.wins??0)>=chest.floors;
        public bool ClaimTowerChest(int tower)
        {
            if(!CanClaimTowerChest || NextTowerChest.tower!=tower) return false;
            ApplyProfileReward(NextTowerChest.reward);
            Profile.claimedRewardTowerId=tower;
            Commit();
            return true;
        }

        public int RemainingDungeonTickets(Challenge challenge)
        {
            if(string.CompareOrdinal(DayKey,Profile.dungeonTicketDate)>0) return challenge.ticketLimit;
            int used=Profile.dungeonTicketsUsed?.Find(entry=>entry.id==challenge.ticketId)?.count??0;
            return Math.Max(0,challenge.ticketLimit-used);
        }

        public bool CanStartChallenge(Challenge challenge) => Run==null && challenge!=null &&
            Profile.highest>=challenge.unlock && Profile.stamina>=challenge.fee &&
            (challenge.ticketId==0 || RemainingDungeonTickets(challenge)>0) &&
            ChallengeChoices(challenge.mode).Any(c=>c.id==challenge.id);

        public bool StartChallenge(int mode,int id,int seed)
        {
            if(!challenges.TryGetValue((mode,id),out var challenge) || !CanStartChallenge(challenge)) return false;
            if(challenge.ticketId!=0)
            {
                Profile.dungeonTicketsUsed??=new List<Reward>();
                if(string.CompareOrdinal(DayKey,Profile.dungeonTicketDate)>0)
                {
                    Profile.dungeonTicketDate=DayKey;
                    Profile.dungeonTicketsUsed.Clear();
                }
                AddReward(Profile.dungeonTicketsUsed,new Reward{id=challenge.ticketId,count=1});
            }
            Profile.stamina-=challenge.fee;
            BeginChallenge(challenge,seed);
            Commit();
            return true;
        }

        private void BeginChallenge(Challenge challenge,int seed)
        {
            int mode=challenge.mode;
            Save.run=new Run{chapter=1,seed=seed,challengeMode=mode,challengeId=challenge.id,phase=Phase.Trail};
            if(mode==6)
            {
                Run.dailyDate=DayKey;
                Run.dailyLoop=true;
                Run.eventQueue=BuildQueue(Chapter,seed);
            }
            Run.hp=Player().Stat("HPMax");
            Record("开始"+challenge.name+"。");
            if(challenge.firstSkill>0) StartChallengeOffer(challenge.firstSkill,SkillOfferOrigin.Begin);
            else if(mode>=3) PrepareSkill(SkillOfferOrigin.Begin);
        }

        private void StartChallengeBattle(Challenge challenge)
        {
            var wave=challenge.waves[Run.stage-1];
            Run.eventId=wave.eventId;
            Run.challengeOptionType=0;
            Run.challengeBoxOpened=false;
            var random=new SeededRandom(Run.seed,Run.stage,challenge.id);
            if(wave.options?.Length>0)
            {
                var option=random.Weighted(wave.options,row=>row.weight);
                Run.resourceId=option.id;
                Run.challengeOptionType=option.type;
                Run.eventKind=6;
                Run.enemies.Clear();
                Record(option.type==1?"遇见天使。":option.type==2?"遇见恶魔。":"发现宝箱。");
                int count=option.type==3?Math.Max(1,3-(int)Player().Get("SelectSkillCountCap")):1;
                if(!PrepareSkill(SkillOfferOrigin.Event,option.buildId,count))
                {
                    if(option.type==1) HealAtAngel();
                    Run.challengeOptionType=0;
                    FinishStage();
                }
                return;
            }
            Run.eventKind=Run.stage==challenge.waves.Length ? 5 : 3;
            Run.monsterId=challenge.id;
            Run.resourceId=0;
            Monster formation=null;
            if(wave.groups?.Length>0)
            {
                var pool=formationGroups[wave.groups[random.Next(wave.groups.Length)]].ToArray();
                formation=pool[random.Next(pool.Length)];
                Run.monsterId=formation.id;
                Run.eventKind=2+formation.battleType;
            }
            Run.round=0;
            Run.waveIndex=0;
            Run.pendingWaves.Clear();
            Run.battleStatistics.Clear();
            Run.enemies.Clear();
            var ids=formation?.members??wave.members;
            for(int index=0;index<ids.Length;index++)
            {
                int id=ids[index];
                if(!members.TryGetValue(id,out var member)) continue;
                var a=new Attributes();
                a.Add(member.attributes);
                if(formation!=null)
                {
                    a.ConvertEnemyBase();
                    a.Add(challenge.attributes);
                    a.ConvertEnemyBase();
                }
                a.Add(wave.attrs);
                a.ConvertEnemyBase();
                if(!string.IsNullOrEmpty(wave.growth))
                {
                    a.Add(wave.growth);
                    a.ConvertEnemyBase();
                }
                if(challenge.mode==4)
                    for(int cleared=1;cleared<Run.stage;cleared++)
                    {
                        a.Add(data.rogueWaveGrowth);
                        a.ConvertEnemyBase();
                    }
                int[] rates=null;
                if((challenge.mode==3 || (challenge.mode==2 && formation!=null)) && member.roleType is >=3 and <=5)
                {
                    int count=ids.Count(other=>members[other].roleType==member.roleType);
                    var scale=data.enemyNumberAttributes.First(row=>row.count==count);
                    rates=member.roleType==3?scale.normalAttr:member.roleType==4?scale.eliteAttr:scale.bossAttr;
                }
                int maxHp=Math.Max(1,a.EnemyStat("HPMax",(rates?[0]??10000)/10000d));
                int attack=Math.Max(1,a.EnemyStat("Attack",(rates?[1]??10000)/10000d));
                int defense=a.EnemyStat("Defence");
                if(challenge.mode==5 || challenge.mode==6)
                {
                    // Local boss practice and old three-fight daily saves use their existing difficulty.
                    float progress=Math.Min(3f,(challenge.level-1)*.035f)+Math.Min(1f,(Run.stage-1)*.04f);
                    maxHp=(int)Math.Round(Math.Clamp(maxHp,180,900)*(1+progress));
                    attack=(int)Math.Round(Math.Clamp(attack,24,105)*(1+progress));
                    defense=Math.Min(defense,65);
                }
                Run.enemies.Add(new Fighter{memberId=id,slot=formation?.slots[index]??0,name=member.name,hp=maxHp,maxHp=maxHp,
                    attack=attack,defense=defense});
            }
            ResetBattleBonuses();
            Run.phase=Phase.Battle;
            Record($"第 {Run.stage} / {challenge.waves.Length} 场战斗。");
        }

        public bool CanSweep(Chapter chapter) => Run==null && chapter!=null &&
            Profile.cleared.Contains(chapter.id) && chapter.journeyStage>0 && Profile.stamina>=chapter.sweepCost;

        public bool Sweep(int chapterId,int seed)
        {
            var chapter=data.chapters.FirstOrDefault(c=>c.id==chapterId);
            if(!CanSweep(chapter)) return false;
            var random=new SeededRandom(seed,chapterId,73);
            int gold=0;
            var ranges=chapter.journeyGold;
            for(int i=0;i<chapter.journeyStage;i++)
            {
                if(ranges==null || ranges.Length==0) break;
                var range=ranges[Math.Min(ranges.Length-1,i*ranges.Length/chapter.journeyStage)];
                gold+=range.min+random.Next(Math.Max(1,range.max-range.min+1));
            }

            Profile.stamina-=chapter.sweepCost;
            Profile.gold+=gold;
            Save.run=new Run{chapter=chapter.id,seed=seed,stage=chapter.journeyStage,gold=gold,
                sweep=true,won=true,phase=Phase.Result};
            Run.hp=Player().Stat("HPMax");
            AddActivity(10);
            Record($"扫荡完成 · 消耗 {chapter.sweepCost} 体力 · 获得 {gold} 金币");
            Commit();
            return true;
        }

        public IEnumerable<Relic> Relics => (data.relics ?? Array.Empty<Relic>()).OrderBy(x=>x.kind).ThenBy(x=>x.id);
        public bool IsEquipped(Relic relic) => (relic.kind==1 ? Profile.mountId : Profile.artifactId)==relic.id;
        public bool Owns(Relic relic) => Profile.ownedRelics.Contains(relic.token);
        public int ItemCount(int id) => Profile.inventory.FirstOrDefault(r=>r.id==id)?.count ?? 0;
        public bool CanEquip(Relic relic) => Run==null && !IsEquipped(relic) && (Owns(relic) || ItemCount(relic.token)>0);

        public bool EquipRelic(int kind,int id)
        {
            var relic=Relics.FirstOrDefault(x=>x.kind==kind && x.id==id);
            if(relic==null || !CanEquip(relic)) return false;
            if(!Owns(relic))
            {
                var item=Profile.inventory.First(r=>r.id==relic.token);
                item.count--;
                if(item.count==0) Profile.inventory.Remove(item);
                Profile.ownedRelics.Add(relic.token);
            }
            if(kind==1) Profile.mountId=id;
            else Profile.artifactId=id;
            Commit();
            return true;
        }

        public int RelicPrice(Relic relic) => relic.kind==1 ? 1200 : 1800;
        public bool CanBuyRelic(Relic relic) => Run==null && Profile.cleared.Contains(1) && !Owns(relic) &&
            ItemCount(relic.token)==0 && Profile.gold>=RelicPrice(relic);
        public bool BuyRelic(int kind,int id)
        {
            var relic=Relics.FirstOrDefault(x=>x.kind==kind && x.id==id);
            if(relic==null || !CanBuyRelic(relic)) return false;
            Profile.gold-=RelicPrice(relic);
            Profile.ownedRelics.Add(relic.token);
            if(kind==1) Profile.mountId=id;
            else Profile.artifactId=id;
            Commit();
            return true;
        }

        public bool UpgradeGear()
        {
            if (Run != null || Profile.gearLevel >= data.gear.Length || Profile.gold < GearPrice) return false;
            Profile.gold -= GearPrice;
            AddLegacyEquipment(data.gear[Profile.gearLevel].id);
            Profile.gearLevel++;
            Commit();
            return true;
        }

        public IReadOnlyList<Pet> Pets => data.pets;
        public Pet ActivePet => data.pets.FirstOrDefault(p=>p.id==Profile.petId);
        public const int PetRecruitPrice=350;
        public int PetDamage
        {
            get
            {
                var pet=ActivePet;
                if(pet==null || !members.TryGetValue(pet.memberId,out var member)) return 0;
                var attributes=new Attributes();
                attributes.Add(member.attributes);
                return Math.Max(0,attributes.Stat("Attack"));
            }
        }

        public bool EquipPet(int id)
        {
            if(Run!=null || !Profile.cleared.Contains(1) || Profile.petId==id || !data.pets.Any(p=>p.id==id)) return false;
            if(Profile.petId==0)
            {
                if(Profile.gold<PetRecruitPrice) return false;
                Profile.gold-=PetRecruitPrice;
            }
            Profile.petId=id;
            Commit();
            return true;
        }

        public const int ForgeMaterialId=24;
        public const int ForgeMaxLevel=10;
        public int ForgePrice => 50*(Profile.forgeLevel+1);
        public int ForgeMaterialCount => Profile.inventory.FirstOrDefault(r=>r.id==ForgeMaterialId)?.count ?? 0;
        public bool CanForge => Run==null && Profile.wornEquipment.Any(id=>id>0) && Profile.forgeLevel<ForgeMaxLevel &&
            Profile.gold>=ForgePrice && ForgeMaterialCount>=2;

        public bool Forge()
        {
            if(!CanForge) return false;
            var stone=Profile.inventory.First(r=>r.id==ForgeMaterialId);
            stone.count-=2;
            if(stone.count==0) Profile.inventory.Remove(stone);
            Profile.gold-=ForgePrice;
            Profile.forgeLevel++;
            Commit();
            return true;
        }

        public int GearPrice => 100 * (Profile.gearLevel + 1);
        public bool CanUpgradeGear => Profile.gearLevel < data.gear.Length;
        public string NextGear => CanUpgradeGear ? data.gear[Profile.gearLevel].name : "";

        public void RestAtCamp()
        {
            if (Run != null || Profile.stamina >= 30) return;
            Profile.stamina = 30;
            Commit();
        }

        public Attributes Player()
        {
            var result = new Attributes();
            if (members.TryGetValue(1, out var hero)) result.Add(hero.attributes);
            if(data.campCatalog==null)
                foreach(var item in data.gear.Take(Profile.gearLevel)) result.Add(item.attributes);
            else
                foreach(int id in Profile.wornEquipment)
                    if(Equipment(id) is EquipmentItem worn) result.Add(worn.attributes);
            if(Profile.forgeLevel>0) result.Add($"Attack={4*Profile.forgeLevel}|HPMax={30*Profile.forgeLevel}");
            AddTalentBonuses(result);
            foreach(var relic in Relics.Where(IsEquipped)) result.Add(relic.attributes);
            if(Run==null) return result;
            foreach (var bonus in Run.eventAttributes) result.Add(bonus);
            foreach (var id in Run.skills)
            {
                result.Add(skills[id].attributes);
                result.Add(skills[id].skillAttributes);
            }
            return result;
        }

        public void Advance()
        {
            if (Phase != Phase.Trail) return;
            if(Run.dailyLoop && Run.stage==Run.dailySegmentStart+Chapter.totalStage)
            {
                Run.dailySegmentStart=Run.stage;
                Run.dailySegment=Math.Min(Run.dailySegment+1,data.dailyRoute.Length-1);
                Run.eventQueue=BuildQueue(Chapter,unchecked(Run.seed+Run.stage));
            }
            Run.stage++;
            RecordCampChestUnlock();
            RecordDamageStatisticsUnlock();
            RecordChapterStage();
            if(ActiveChallenge==null) Profile.bestDay=Math.Max(Profile.bestDay,Run.stage);
            if(!Run.dailyLoop && ActiveChallenge is Challenge challenge)
            {
                StartChallengeBattle(challenge);
                Commit();
                return;
            }
            Run.eventId = Run.eventQueue[Run.stage - Run.dailySegmentStart - 1];
            var kind = types.TryGetValue(Run.eventId, out var row) ? row.kind : 0;
            bool offlineStop=Run.stage==Run.offlineStopDay && (Run.offlineStopKind is 7 or 12) &&
                (kind is 0 or 1) && !Chapter.rewardStage.Contains(Run.stage);

            var random = new SeededRandom(Run.seed, Run.stage, 5);
            Run.pendingWheelRewardId = 0;
            Run.wheelOpened = false;
            if(offlineStop)
            {
                Run.resourceId=Run.wheelId=0;
                kind=Run.offlineStopKind;
            }
            else
            {
                var candidates = row?.resources.Where(id => resources.TryGetValue(id,out var res) &&
                    (Run.legacyQueue || res.weight > 0)).Select(id=>resources[id]).ToArray() ?? Array.Empty<EventResource>();
                Run.resourceId = random.Weighted(candidates,r=>r.weight)?.id ?? 0;
                if(Run.replayOpening && Run.stage == data.recordedOpening.wheelDay)
                    Run.resourceId = data.recordedOpening.wheelResource;
                SetWheel();
                if(kind<3 && resources.TryGetValue(Run.resourceId,out var eventResource))
                    kind=JourneyResourceKind(eventResource,kind);
            }
            Run.eventKind=kind;
            Run.phase = Phase.Event;
            Run.stat = random.Weighted(data.stats,r=>r.weight)?.id ?? 1;
            Record($"抵达第 {Run.stage} 步 · {EventName(kind)}");
            if(!HasWheel)
            {
                if(kind >= 3 && kind <= 5) Record("前方遇到敌人。");
                if(kind == 8) Record("路边有一处可以休息的地方。");
                if(kind == 7) Record("发现一处补给点，可以用旅途金币购买补给。");
                if(kind == 12) Record("水面泛起涟漪，可以在这里垂钓。");
            }
            Commit();
        }

        private void RecordDamageStatisticsUnlock()
        {
            if(data.damageStatisticsUnlock is not StageGate gate) return;
            if(Profile.highest>gate.chapter || Profile.cleared.Contains(gate.chapter) ||
                Run!=null && Run.challengeMode==0 && Run.chapter==gate.chapter && Run.stage>=gate.stage)
                Profile.damageStatisticsUnlocked=true;
        }

        private ChapterProgress ProgressForChapter(int chapter)
        {
            var progress=Profile.chapterProgress.FirstOrDefault(row=>row.chapter==chapter);
            if(progress!=null) return progress;
            progress=new ChapterProgress{chapter=chapter};
            Profile.chapterProgress.Add(progress);
            return progress;
        }

        private void RecordChapterStage()
        {
            if(Run.challengeMode!=0 || Run.sweep) return;
            var progress=ProgressForChapter(Run.chapter);
            progress.stage=Math.Max(progress.stage,Run.stage);
        }

        private void RecordCampChestUnlock()
        {
            if(Run.chapter==3 && Run.challengeMode==0 && !Run.sweep && Run.stage>=30)
                Profile.chestTabUnlocked=true;
        }

        public static int[] BuildQueue(Chapter chapter, int seed)
        {
            var queue = new int[chapter.totalStage];
            var random = new SeededRandom(seed, 0, 1);
            foreach (var band in chapter.queue)
            {
                var bag = band.entries.SelectMany(x=>Enumerable.Repeat(x.id, x.count)).ToList();
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int other = random.Next(i + 1);
                    (bag[i], bag[other]) = (bag[other], bag[i]);
                }
                for (int stage=band.first; stage<=Math.Min(band.last,queue.Length) && stage-band.first<bag.Count; stage++)
                    queue[stage-1] = bag[stage-band.first];
            }
            for (int i=0; i<Math.Min(queue.Length,chapter.startEvent.Length); i++)
                if(queue[i]==0) queue[i]=chapter.startEvent[i];
            for (int i=0; i<queue.Length; i++)
                if (queue[i]==0 && chapter.normalEvent.Length>0) queue[i]=chapter.normalEvent[random.Next(chapter.normalEvent.Length)];
            return queue;
        }

        private (int day,int kind) PlanOfflineStop(Chapter chapter,int[] queue,int seed)
        {
            var settings=data.offlineJourneyStops;
            if(settings==null || chapter.totalStage<settings.minimumChapterDays) return default;
            int first=Math.Max(settings.firstDay,chapter.startEvent.Length+1);
            int last=Math.Min(chapter.totalStage-1,chapter.totalStage*Math.Clamp(settings.lastDayPercent,0,100)/100);
            var candidates=new List<int>();
            for(int day=first;day<=last;day++)
            {
                if(chapter.rewardStage.Contains(day) || !types.TryGetValue(queue[day-1],out var type) || type.kind>1) continue;
                if(type.resources?.Any(id=>resources.TryGetValue(id,out var resource) && resource.weight>0 &&
                    (resource.wheelId>0 || resource.battleGroups?.Length>0 || JourneyResourceKind(resource,type.kind)!=type.kind))==true) continue;
                candidates.Add(day);
            }
            if(candidates.Count==0) return default;
            var random=new SeededRandom(seed,chapter.id,74);
            int kind=settings.fishingMaps?.Contains(chapter.mapId)==true ? 12 : 7;
            return (candidates[random.Next(candidates.Count)],kind);
        }

        private static int JourneyResourceKind(EventResource resource,int otherwise)
        {
            string path=resource.path?.ToLowerInvariant() ?? "";
            if(path.Contains("fishing")) return 12;
            if(path.Contains("shop")) return 7;
            if(path.Contains("box")) return 9;
            if(path.Contains("rest")) return 8;
            return otherwise;
        }

        private static void MoveEvent(int[] queue,int eventId,int day)
        {
            if(day < 6 || day > Math.Min(queue.Length,9)) return;
            int index = Array.IndexOf(queue,eventId,5,Math.Min(queue.Length,9)-5);
            if(index >= 0) (queue[day-1],queue[index]) = (queue[index],queue[day-1]);
        }

        private void SetWheel()
        {
            Run.wheelId = resources.TryGetValue(Run.resourceId,out var resource) ? resource.wheelId : 0;
            if(Run.legacyQueue && ((Run.chapter==1 && Run.stage==6) || Run.eventId==201))
            {
                Run.wheelId = 1;
                Run.wheelOpened = true;
            }
        }

        public static string EventName(int kind)
        {
            switch (kind)
            {
                case 3: return "遭遇战";
                case 4: return "精英挑战";
                case 5: return "首领战";
                case 6: return "技能领悟";
                case 7: return "旅途商店";
                case 8: return "营地休息";
                case 9: return "发现宝箱";
                case 10: return "首领前夕";
                case 11: return "旅途奇遇";
                case 12: return "垂钓点";
                case 13: return "结束垂钓";
                case 2: return "旅途抉择";
                default: return "旅途见闻";
            }
        }

        public WheelDefinition Wheel => Phase == Phase.Event && ActiveChallenge == null && Run.wheelId != 0
            ? data.wheels?.FirstOrDefault(wheel => wheel.id == Run.wheelId) : null;

        public bool HasWheel => Wheel != null;
        internal const int JourneySupplyPrice=35;
        public bool CanBuyJourneySupplies => Phase==Phase.Event && Run.eventKind==7 && Run.gold>=JourneySupplyPrice;
        internal bool HasJourneyChoice => Phase==Phase.Event && Run.eventKind is 2 or 7 or 12;
        internal JourneyEventModels JourneyModels => data.journeyEventModels;
        internal JourneyModel CurrentJourneyModel()
        {
            if(Phase!=Phase.Event || (!HasJourneyChoice && !HasWheel) || JourneyModels==null) return null;
            int id=Run.eventKind==7 ? -7 : JourneyModels.bindings.FirstOrDefault(row=>row.resourceId==Run.resourceId)?.modelId??0;
            return id==0?null:JourneyModels.models.FirstOrDefault(row=>row.id==id);
        }
        public string WheelStory => resources.TryGetValue(Run.resourceId,out var resource) ? resource.wheelStory : null;
        public string WheelAction => resources.TryGetValue(Run.resourceId,out var resource) ? resource.wheelAction : null;

        public void OpenWheel()
        {
            if(!HasWheel || Run.wheelOpened) return;
            Run.wheelOpened = true;
            Commit();
        }

        public void SkipWheel()
        {
            if(!HasWheel || Run.pendingWheelRewardId != 0) return;
            Run.wheelId = 0;
            Run.wheelOpened = false;
            Record("离开了好运转盘。");
            FinishStage();
            Commit();
        }

        public int WheelIndex
        {
            get
            {
                var wheel = Wheel;
                if(wheel == null) return -1;
                int selected = Run.pendingWheelRewardId;
                if(selected == 0 && Run.replayOpening && Run.stage == data.recordedOpening.wheelDay)
                    selected = data.recordedOpening.wheelReward;
                int chosen = Array.FindIndex(wheel.rewards,reward=>reward.id==selected);
                if(chosen >= 0) return chosen;

                var random = new SeededRandom(Run.seed,Run.stage,41);
                int roll = random.Next(wheel.rewards.Sum(reward=>Math.Max(0,reward.weight)));
                for(int i=0;i<wheel.rewards.Length;i++)
                {
                    roll -= Math.Max(0,wheel.rewards[i].weight);
                    if(roll < 0) return i;
                }
                throw new InvalidOperationException("Invalid turntable reward pool.");
            }
        }

        public int BeginWheelSpin()
        {
            int index = WheelIndex;
            if(index < 0) return index;
            int rewardId = Wheel.rewards[index].id;
            if(Run.pendingWheelRewardId == rewardId && Run.wheelOpened) return index;
            Run.pendingWheelRewardId = rewardId;
            Run.wheelOpened = true;
            Commit();
            return index;
        }

        public void ResolveWheel()
        {
            int index = BeginWheelSpin();
            if(index < 0) return;
            var reward = Wheel.rewards[index];
            var fortune = reward.kind == 2 || reward.value == 0 ? JournalFortune.None :
                reward.value < 0 ? JournalFortune.Unlucky : JournalFortune.Lucky;
            string attributes = null;
            if (reward.kind != 2 && reward.value != 0)
            {
                string attribute = reward.target switch
                {
                    "HP" => "HP%",
                    "HPMax" => "HPMax%",
                    "Attack" => "Attack%",
                    "Defense" => "Defence%",
                    "Exp" => "Experience",
                    _ => null
                };
                if (attribute != null) attributes = $"{attribute}={reward.value}";
            }
            Record("好运转盘 · " + reward.label, fortune, attributes);
            if(reward.kind == 2)
            {
                if(int.TryParse(reward.target,out int source))
                {
                    var choices = SkillCandidates(source).ToList();
                    var random = new SeededRandom(Run.seed,Run.stage,42);
                    var skill = random.Weighted(choices,item=>item.weight);
                    if(skill != null) LearnSkill(skill);
                    else Record("暂无可获得的技能。");
                }
            }
            else
            {
                int maxHp = Player().Stat("HPMax");
                switch(reward.target)
                {
                    case "Exp":
                        GrantExperience(reward.value);
                        break;
                    case "HP":
                        Run.hp = Math.Clamp(Run.hp + (int)((long)maxHp*reward.value/100),1,maxHp);
                        break;
                    case "Attack":
                    case "Defense":
                    case "HPMax":
                        string key = reward.target == "Defense" ? "Defence" : reward.target;
                        Run.eventAttributes.Add(key+"%="+reward.value);
                        int nextMax = Math.Max(1,Player().Stat("HPMax"));
                        Run.hp = Math.Clamp(Run.hp+Math.Max(0,nextMax-maxHp),1,nextMax);
                        break;
                }
            }
            Run.wheelId = Run.pendingWheelRewardId = 0;
            Run.wheelOpened = false;
            if(Phase != Phase.Skill) FinishStage();
            Commit();
        }

        public void ResolveEvent(int choice)
        {
            if (Phase != Phase.Event || choice < 0 || choice > 1) return;
            if(HasWheel)
            {
                if(choice==0) ResolveWheel();
                else SkipWheel();
                return;
            }
            int kind = Run.eventKind;
            if(choice==1)
            {
                if(!HasJourneyChoice) return;
                Record(kind switch
                {
                    2 => "绕过了岔路。",
                    7 => "离开了旅途商店。",
                    _ => "离开了垂钓点。"
                });
                FinishStage();
                Commit();
                return;
            }
            if (kind >= 3 && kind <= 5)
            {
                BeginBattle(kind - 2);
                Commit();
                return;
            }
            if (kind == 6 && PrepareSkill(SkillOfferOrigin.Event)) { Commit(); return; }

            int maxHp = Player().Stat("HPMax");
            int before = Run.hp;
            if (kind == 7)
            {
                if(!CanBuyJourneySupplies) return;
                Run.gold -= JourneySupplyPrice;
                Run.hp = Math.Min(maxHp, Run.hp + maxHp / 3);
                Record("购买补给。", attributes: $"Gold=-{JourneySupplyPrice}|HP={Run.hp - before}");
            }
            else if (kind==12)
            {
                int roll=new SeededRandom(Run.seed,Run.stage,13).Next(100);
                int reward=roll<70 ? 25+roll/3 : 0;
                Run.gold+=reward;
                int experience = reward > 0 ? 25 : 10;
                Record(reward > 0 ? "钓到鱼。" : "鱼从钩上溜走了。",
                    reward > 0 ? JournalFortune.Lucky : JournalFortune.Unlucky, $"Gold={reward}|Experience={experience}");
                GrantExperience(experience);
            }
            else if (kind == 8 || kind == 10)
            {
                Run.hp = Math.Min(maxHp, Run.hp + maxHp / 6);
                Record(Run.hp == before ? "生命已满。" : "休整完成。", attributes: $"HP={Run.hp - before}");
            }
            else
            {
                int bonus = (kind==9 ? 40 : 12) + new SeededRandom(Run.seed,Run.stage,9).Next(30);
                var fortune = kind==9 ? JournalFortune.Great :
                    (resources.TryGetValue(Run.resourceId,out var resource) ? resource.fortuneType : 0) switch
                    {
                        2 => JournalFortune.Lucky,
                        3 => JournalFortune.Great,
                        _ => JournalFortune.None
                    };
                if(kind==2)
                {
                    var stat=data.stats.FirstOrDefault(s=>s.id==Run.stat);
                    if(stat!=null)
                    {
                        int previous=Player().Stat("HPMax");
                        Run.eventAttributes.Add(stat.attributes);
                        Run.hp+=Math.Max(0,Player().Stat("HPMax")-previous);
                        Record(JournalAttributePrefix + stat.attributes,
                            fortune==JournalFortune.None ? JournalFortune.Lucky : fortune, stat.attributes);
                        fortune=JournalFortune.None;
                    }
                }
                Run.gold += bonus;
                Record(kind==9 ? "发现宝箱。" : "旅途收获。", fortune,
                    $"Gold={bonus}|Experience=20");
                GrantExperience(20);
            }
            if (Phase != Phase.Skill) FinishStage();
            Commit();
        }

        private IEnumerable<Skill> SkillCandidates(int source) => data.builds.Where(s=>s.unlockChapter<=(ActiveChallenge==null?Run.chapter:Profile.highest) && s.source.Contains(source) && s.weight>0
                && s.kind==0 && s.heroSource==0 && (s.outsideSource?.Length??0)<2 && (s.level>1 || s.initShield<=0)
                && (source==108 || BattleSkills.ContainsKey(s.skillId) || Useful(s.attributes) || Useful(s.skillAttributes))
                && !Run.skills.Contains(s.id)
                && MeetsRequirements(s)
                && (s.level<=1 ? !Run.skills.Any(id=>skills[id].groupId==s.groupId) : Run.skills.Any(id=>skills[id].groupId==s.groupId && skills[id].level==s.level-1)))
                .OrderBy(s=>s.id);

        private bool StartChallengeOffer(int id,SkillOfferOrigin origin)
        {
            var rule=ChallengeSkillRule(id);
            Run.challengeOfferId=id;
            Run.offerRoundsLeft=rule.rounds;
            return DealChallengeRound(rule,origin);
        }

        private bool DealChallengeRound(ChallengeOffer rule,SkillOfferOrigin origin)
        {
            if(PrepareSkill(origin,rule.source,rule.count))
            {
                Run.offerPicksLeft=Math.Min(rule.picks,Run.choices.Count);
                return true;
            }
            Run.challengeOfferId=Run.offerRoundsLeft=Run.offerPicksLeft=0;
            return false;
        }

        private ChallengeOffer ChallengeSkillRule(int id) =>
            data.challengeOffers.First(r=>r.id==id && r.season==(Run.challengeMode==8));

        private bool PrepareSkill(SkillOfferOrigin origin,int source=1,int offerSize=0)
        {
            var candidates = SkillCandidates(source).ToList();
            if (candidates.Count == 0) return false;
            var random = new SeededRandom(Run.seed,Run.stage,Run.level+100+Run.offerRoundsLeft);
            int count=offerSize>0?offerSize:SkillOfferSize;
            Run.choices.Clear();
            while (Run.choices.Count<count && candidates.Count>0)
            {
                var picked = random.Weighted(candidates,s=>s.weight);
                Run.choices.Add(picked.id);
                candidates.RemoveAll(s=>s.groupId==picked.groupId);
            }
            Run.skillAtStart = origin==SkillOfferOrigin.Begin;
            Run.skillOfferOrigin = origin;
            Run.phase = Phase.Skill;
            return true;
        }

        private int SkillOfferSize => Math.Max(2,3-(int)Player().Get("SelectSkillCountCap"));

        public bool RefreshSkillChoices()
        {
            if(SkillRefreshesRemaining<=0) return false;

            var seed=SkillRefreshSeed(Run.skillRefreshCount+1);
            ulong state=unchecked((ulong)(long)seed);
            uint roll=NextSkillRandom(ref state);
            int quality=RefreshQuality(roll);
            var candidates=SkillCandidates(1).ToList();
            var matching=candidates.Where(s=>s.quality==quality).ToList();
            if(matching.Count>0) candidates=matching;

            int count=SkillOfferSize;
            var choices=new List<int>(count);
            while(choices.Count<count && candidates.Count>0)
            {
                int index=(int)(roll%(uint)candidates.Sum(skill=>skill.weight));
                int picked=0;
                while(index>=candidates[picked].weight)
                    index-=candidates[picked++].weight;
                choices.Add(candidates[picked].id);
                candidates.RemoveAt(picked);
            }

            if(choices.Count<count)
            {
                while(choices.Count<count) choices.Add(100000);
                state=unchecked((ulong)(long)seed);
                var shuffled=new List<int>(count);
                while(choices.Count>0)
                {
                    int pick=choices.Count==1?0:(int)(NextSkillRandom(ref state)%(uint)choices.Count);
                    shuffled.Add(choices[pick]);
                    choices.RemoveAt(pick);
                }
                choices=shuffled;
            }

            Run.choices.Clear();
            Run.choices.AddRange(choices);
            Run.skillOfferOrigin=SkillOfferOrigin.LevelUp;
            Run.skillRefreshCount++;
            Commit();
            return true;
        }

        private int RefreshQuality(uint roll)
        {
            var weights=data.skillRefreshWeights;
            if(weights==null || weights.Length!=3) return -1;
            var attributes=Player();
            int common=weights[0];
            int gold=(int)(weights[1]*(1+attributes.Get("OrangeSkillRate%")/100));
            int red=(int)(weights[2]*(1+attributes.Get("RedSkillRate%")/100));
            int total=common+gold+red;
            if(total<=0) return -1;
            int ticket=(int)(roll%(uint)total);
            return ticket<common ? 0 : ticket<common+gold ? 1 : 2;
        }

        internal int SkillRefreshSeed(int consumed)
        {
            // The shipped factory generates level seeds first, then alternating normal and ad seeds.
            const int chapterExperienceGroup=101;
            int levels=data.experience.Where(exp=>exp.groupId==chapterExperienceGroup).Max(exp=>exp.lv);
            int skip=levels+2*Math.Min(consumed,99);
            ulong state=unchecked((ulong)(long)Run.seed);
            for(int i=0;i<skip;i++) NextSkillRandom(ref state);
            return unchecked((int)NextSkillRandom(ref state));
        }

        private static uint NextSkillRandom(ref ulong state) => OriginalRandom.Step(ref state);

        private bool MeetsRequirements(Skill skill)
        {
            if (skill.compose != null && skill.compose.Length>0) return false;
            var owned=new HashSet<int>(Run.skills.Select(id=>skills[id].groupId));
            if (string.IsNullOrWhiteSpace(skill.needGroup)) return true;
            return skill.needGroup.Split('|').Any(option=>option.Split('&').All(id=>int.TryParse(id,out int group) && owned.Contains(group)));
        }

        private static bool Useful(string value)
        {
            var a = new Attributes();
            a.Add(value);
            return new[]{"Attack%","HPMax%","Defence%","CritRate%","NormalCritRate%","ComboRate%","NormalCombo%",
                "PoisonBaseAttack%","ThunderBaseAttack%","FireBaseAttack%","BleedBaseAttack%","VampireRate%",
                "Miss%","Revenge%","DamageReduction%","GlobalDamageReduction%","ComboDamageAdd%","RevengeDamageAdd%",
                "NormalDamageAdd%","BigSkillDamageAdd%","SkillDamageAdd%","RecoverAdd%"}.Any(k=>a.Get(k)>0);
        }

        public void SelectSkill(int id)
        {
            if (Phase != Phase.Skill || !Run.choices.Contains(id)) return;
            if(Run.challengeOptionType==3 && !Run.challengeBoxOpened) return;
            bool atStart=CurrentSkillOffer==SkillOfferOrigin.Begin;
            LearnSkill(skills[id]);
            if(Run.challengeOptionType==2)
            {
                int cost=skills[id].demonCost;
                Run.eventAttributes.Add("HPMax%=-"+cost);
                Run.hp=Math.Max(1,Math.Min(Run.hp,Player().Stat("HPMax")));
                Record($"与恶魔交易，生命上限减少 {cost}%。");
            }
            Run.challengeOptionType=0;
            if(Run.challengeOfferId!=0)
            {
                Run.choices.Remove(id);
                if(--Run.offerPicksLeft>0 && Run.choices.Count>0) { Commit(); return; }
                var rule=ChallengeSkillRule(Run.challengeOfferId);
                if(--Run.offerRoundsLeft>0 && DealChallengeRound(rule,CurrentSkillOffer)) { Commit(); return; }
                Run.challengeOfferId=Run.offerRoundsLeft=Run.offerPicksLeft=0;
            }
            Run.choices.Clear();
            Run.skillOfferOrigin=SkillOfferOrigin.Unspecified;
            if (atStart)
            {
                if(Run.extraStartChoices>0)
                {
                    Run.extraStartChoices--;
                    if(PrepareSkill(SkillOfferOrigin.Begin)) { Commit(); return; }
                }
                Run.phase=Phase.Trail;
            }
            else if(!QueueExperienceSkill()) FinishStage();
            Commit();
        }

        internal int AngelRecoveryPercent => data.angelRecoveryPercent;

        private void HealAtAngel()
        {
            int max=Player().Stat("HPMax");
            int before=Run.hp;
            Run.hp=(int)Math.Min(max,Run.hp+(long)max*AngelRecoveryPercent/100);
            Record($"接受天使治疗，恢复 {Run.hp-before} 生命。");
        }

        public void ChooseChallengeAlternative()
        {
            if(Phase!=Phase.Skill || Run.challengeOptionType is not (1 or 2)) return;
            if(Run.challengeOptionType==1) HealAtAngel();
            else Record("拒绝了恶魔的交易。");
            Run.challengeOptionType=0;
            Run.choices.Clear();
            Run.skillOfferOrigin=SkillOfferOrigin.Unspecified;
            FinishStage();
            Commit();
        }

        public void OpenChallengeBox()
        {
            if(Phase!=Phase.Skill || Run.challengeOptionType!=3 || Run.challengeBoxOpened) return;
            Run.challengeBoxOpened=true;
            Commit();
        }

        private void LearnSkill(Skill skill)
        {
            int previous = Player().Stat("HPMax");
            Run.skills.RemoveAll(current=>skills[current].groupId==skill.groupId || (skill.deleteGroup?.Contains(skills[current].groupId) ?? false));
            Run.skills.Add(skill.id);
            int next = Player().Stat("HPMax");
            Run.hp = Math.Clamp(Run.hp + Math.Max(0,next-previous) + next*Math.Max(0,skill.recoverHp)/100,1,next);
            Record("习得「" + skill.name + "」。", skillName: skill.name);
        }

        private void GrantExperience(int amount)
        {
            Run.experience += amount;
            QueueExperienceSkill();
        }

        private bool QueueExperienceSkill()
        {
            var level=data.experience.FirstOrDefault(row=>row.groupId==101 && row.lv==Run.level);
            if(level==null || level.expToNext<=0 || Run.experience<level.expToNext) return false;
            int threshold=level.expToNext;
            Run.experience -= threshold;
            Run.level++;
            return PrepareSkill(SkillOfferOrigin.LevelUp);
        }

        private void BeginBattle(int kind)
        {
            int difficulty=1+Chapter.difficultStage.Count(boundary=>Run.stage>boundary);
            int seed=new SeededRandom(Run.seed,Run.stage,15).Next(int.MaxValue);
            var random=new OriginalRandom(seed);
            var waves=new List<Monster>();
            if(!IsRecordedEncounter && resources.TryGetValue(Run.resourceId,out var resource) && resource.battleGroups!=null)
            {
                foreach(int group in resource.battleGroups)
                {
                    int waveSeed=resource.multiWave?random.NextInt():seed;
                    if(group<=0) continue;
                    var candidates=data.monsters.Where(row=>row.group==group && row.difficult==difficulty &&
                        FormationMembers(row).Any()).ToArray();
                    if(candidates.Length>0) waves.Add(candidates[new OriginalRandom(waveSeed).Range(0,candidates.Length)]);
                }
            }
            if(waves.Count==0) waves.Add(ChooseChapterFormation(kind,difficulty,seed));
            Run.pendingBattleReward=PlanBattleReward(kind,waves,random);
            Run.pendingWaves.Clear();
            Run.pendingWaves.AddRange(waves.Skip(1).Select(wave=>wave.id));
            Run.waveIndex=0;
            LoadWave(waves[0],kind);
            Run.round=0;
            Run.battleStatistics.Clear();
            ResetBattleBonuses();
            Run.phase=Phase.Battle;
        }

        private Monster ChooseChapterFormation(int kind,int difficulty,int seed)
        {
            var groups=Chapter.monsterGroup;
            var pool=data.monsters.Where(row=>groups.Contains(row.group) && row.battleType==kind &&
                FormationMembers(row).Any()).ToArray();
            if(pool.Length==0 && Run.dailyLoop)
                pool=data.monsters.Where(row=>data.dailyRoute[0].monsterGroup.Contains(row.group) &&
                    row.battleType==kind && FormationMembers(row).Any()).ToArray();
            if(pool.Length==0) throw new InvalidOperationException($"Chapter {Run.chapter} has no formations of battle type {kind}.");
            if(kind!=3)
            {
                int nearest=pool.Min(row=>Math.Abs(difficulty-row.difficult));
                pool=pool.Where(row=>Math.Abs(difficulty-row.difficult)==nearest).ToArray();
            }
            if(IsRecordedEncounter)
            {
                var recorded=pool.FirstOrDefault(row=>row.members.Length==1 && MemberModel(row.members[0])==220081);
                if(recorded!=null) return recorded;
            }
            return pool[new OriginalRandom(seed).Range(0,pool.Length)];
        }

        private IEnumerable<int> FormationMembers(Monster formation) => formation.members.Where((id,index)=>
            (formation.slots!=null && index<formation.slots.Length?formation.slots[index]<=5:index<5) &&
            members.ContainsKey(id));

        private BattleReward PlanBattleReward(int kind,List<Monster> waves,OriginalRandom random)
        {
            double rate=Math.Max(0,1+Player().Get("ExpAddRate%")/100d);
            int experience=0;
            foreach(var wave in waves)
                foreach(int id in FormationMembers(wave))
                {
                    var range=members[id].dropExp;
                    if(range==null || range.Length==0) continue;
                    int roll=random.Range(range[0],range[range.Length>1?1:0]+1);
                    experience+=(int)(roll*rate);
                }
            var ranges=Chapter.gold;
            var coins=ranges.Length>0?ranges[Math.Min(ranges.Length-1,kind-1)]:null;
            int gold=coins==null?0:coins.min+new SeededRandom(Run.seed,Run.stage,16).Next(Math.Max(1,coins.max-coins.min+1));
            return new BattleReward{day=Run.stage,gold=gold,experience=experience};
        }

        private void LoadWave(Monster encounter,int kind)
        {
            var chapter=Chapter;
            Run.enemies.Clear();
            Run.monsterId=encounter.id;
            // ChapterController.GetStageAttributesUpgrade indexes the group's ordered rows by stage.
            var upgrades=data.upgrades.Where(r=>r.group==chapter.stageUpgradeGroup).ToArray();
            int growth=Math.Max(0,Run.stage-upgrades.Length);
            var upgrade=Run.stage>0 && upgrades.Length>0?upgrades[Math.Min(Run.stage,upgrades.Length)-1]:null;
            string battleAttributes=encounter.battleType switch
            {
                1=>chapter.normalBattleAttr,2=>chapter.eliteBattleAttr,3=>chapter.bossBattleAttr,
                4=>chapter.npcBattleAttr,_=>null
            };
            if(Run.dailyLoop)
            {
                var daily=ActiveChallenge;
                battleAttributes=encounter.battleType switch
                {
                    1=>daily.normalBattleAttr,2=>daily.eliteBattleAttr,3=>daily.bossBattleAttr,_=>battleAttributes
                };
            }
            var retry=Run.dailyLoop?null:data.enemyRetry;
            int threshold=retry?.thresholds!=null && chapter.id<=retry.thresholds.Length?retry.thresholds[chapter.id-1]:0;
            int attempts=threshold>0?Math.Max(1,ProgressForChapter(chapter.id).attempts):1;
            int retryStacks=threshold>0?Math.Min(retry.maximum,Math.Max(0,attempts-threshold+1)):0;
            for(int i=0;i<encounter.members.Length;i++)
            {
                if(encounter.slots!=null && i<encounter.slots.Length?encounter.slots[i]>5:i>=5) continue;
                int id=encounter.members[i];
                if (!members.TryGetValue(id,out var member)) continue;
                var a=new Attributes();
                a.Add(member.attributes);
                a.ConvertEnemyBase();
                a.Add(Run.dailyLoop?ActiveChallenge.attributes:chapter.attributes);
                a.ConvertEnemyBase();
                a.Add(battleAttributes);
                a.ConvertEnemyBase();
                if (upgrade!=null)
                {
                    int attack=(int)(upgrade.attackUpgrade*Math.Pow(1.015f,growth));
                    int hpMax=(int)(upgrade.hpUpgrade*Math.Pow(1.045f,growth));
                    a.Add($"Attack%={attack}|HPMax%={hpMax}");
                }
                a.ConvertEnemyBase();
                for(int retryStack=0;retryStack<retryStacks;retryStack++) a.Add(retry.attributes);
                if(retryStacks>0) a.ConvertEnemyBase();
                int hp=Math.Max(1,a.EnemyStat("HPMax"));
                int slot=encounter.slots!=null && i<encounter.slots.Length?encounter.slots[i]:0;
                Run.enemies.Add(new Fighter{memberId=id,slot=slot,name=member.name,hp=hp,maxHp=hp,
                    attack=Math.Max(1,a.EnemyStat("Attack")),defense=a.EnemyStat("Defence")});
            }
            if (Run.enemies.Count==0) throw new InvalidOperationException("Encounter has no resolved combat members.");
        }

        private enum SkillTiming { Round, Hit, Rage, Counter }

        private readonly struct BattleSkill
        {
            internal readonly SkillTiming timing;
            internal readonly CombatEffect effect;
            internal readonly int count, firstRound, cooldown;
            internal readonly float chance;

            internal BattleSkill(SkillTiming timing,CombatEffect effect,int count=1,int firstRound=1,int cooldown=1,float chance=100)
            {
                this.timing=timing; this.effect=effect; this.count=count;
                this.firstRound=firstRound; this.cooldown=cooldown; this.chance=chance;
            }
        }

        private static readonly Dictionary<int,BattleSkill> BattleSkills=new Dictionary<int,BattleSkill>
        {
            [100010]=new(SkillTiming.Hit,CombatEffect.Poison),
            [100011]=new(SkillTiming.Rage,CombatEffect.Heal),
            [100022]=new(SkillTiming.Round,CombatEffect.Rage,cooldown:0),
            [100023]=new(SkillTiming.Hit,CombatEffect.Shield,chance:40),
            [100024]=new(SkillTiming.Rage,CombatEffect.Shield),
            [100102]=new(SkillTiming.Hit,CombatEffect.Rage),
            [100213]=new(SkillTiming.Round,CombatEffect.Explosion,cooldown:0),
            [100301]=new(SkillTiming.Round,CombatEffect.Lightning),
            [100302]=new(SkillTiming.Rage,CombatEffect.Lightning,count:2),
            [100303]=new(SkillTiming.Round,CombatEffect.Lightning,count:3,cooldown:0),
            [100304]=new(SkillTiming.Round,CombatEffect.Lightning,count:5,firstRound:5,cooldown:0),
            [100311]=new(SkillTiming.Hit,CombatEffect.Lightning,count:2,chance:45),
            [100401]=new(SkillTiming.Round,CombatEffect.Fire),
            [100402]=new(SkillTiming.Rage,CombatEffect.Fire,count:2),
            [100403]=new(SkillTiming.Round,CombatEffect.Fire,count:3,cooldown:0),
            [100501]=new(SkillTiming.Hit,CombatEffect.Ice,chance:45),
            [100502]=new(SkillTiming.Rage,CombatEffect.Ice),
            [100505]=new(SkillTiming.Round,CombatEffect.Ice,firstRound:2,cooldown:2),
            [100601]=new(SkillTiming.Round,CombatEffect.Knife,firstRound:2,cooldown:2),
            [100602]=new(SkillTiming.Round,CombatEffect.Knife,firstRound:2,cooldown:2),
            [100603]=new(SkillTiming.Round,CombatEffect.Knife,firstRound:2,cooldown:2),
            [100604]=new(SkillTiming.Round,CombatEffect.Knife,firstRound:2,cooldown:2),
            [100605]=new(SkillTiming.Round,CombatEffect.Knife,firstRound:2,cooldown:2),
            [100701]=new(SkillTiming.Hit,CombatEffect.SwordWave,chance:60),
            [100803]=new(SkillTiming.Counter,CombatEffect.Spear)
        };

        private const int FullRecharge=100;

        private void ResetBattleBonuses()
        {
            Run.recharge=0;
            Run.shield=Run.skills.Sum(id=>Math.Max(0,skills[id].initShield))*Player().Stat("HPMax")/100;
        }

        private void IncreaseRecharge(int amount)
        {
            Run.recharge=Math.Min(FullRecharge,Math.Max(0,Run.recharge)+amount);
        }

        private int ResolveSkills(BattleRound report,SkillTiming timing,Attributes player,SeededRandom random,int targetIndex)
        {
            int damage=0;
            foreach(int buildId in Run.skills)
            {
                var skill=skills[buildId];
                if(!BattleSkills.TryGetValue(skill.skillId,out var rule) || rule.timing!=timing) continue;
                int elapsed=Run.round-rule.firstRound;
                if(elapsed<0 || (rule.cooldown==0?elapsed!=0:elapsed%rule.cooldown!=0) || !Roll(random,rule.chance)) continue;
                switch(rule.effect)
                {
                    case CombatEffect.Heal:
                        Heal(report,player.Stat("HPMax"),player.Stat("HPMax")*5/100,buildId);
                        continue;
                    case CombatEffect.Shield:
                        int shield=player.Stat("HPMax")*(timing==SkillTiming.Rage?10:5)/100;
                        Run.shield=Math.Max(Run.shield,shield);
                        if(shield>0) SkillStatus(report,CombatAction.Hero,Run.hp,CombatEffect.Shield,buildId,shield);
                        continue;
                    case CombatEffect.Rage:
                        IncreaseRecharge(timing==SkillTiming.Round?50:25);
                        SkillStatus(report,CombatAction.Hero,Run.hp,CombatEffect.Rage,buildId);
                        continue;
                }

                int count=rule.count;
                if(rule.effect==CombatEffect.Lightning) count+=Math.Max(0,player.Stat("ThunderCount"));
                if(rule.effect==CombatEffect.Knife)
                    count+=Math.Max(0,player.Stat("RoundStartKnifeCount")-1);
                for(int shot=0;shot<count;shot++)
                {
                    if(targetIndex<0 || Run.enemies[targetIndex].hp<=0)
                        targetIndex=Run.enemies.FindIndex(e=>e.hp>0);
                    if(targetIndex<0) break;
                    if(rule.effect==CombatEffect.Fire && timing!=SkillTiming.Hit)
                    {
                        for(int i=0;i<Run.enemies.Count;i++)
                            if(Run.enemies[i].hp>0) damage+=CastAt(report,rule.effect,i,player,random,buildId);
                    }
                    else damage+=CastAt(report,rule.effect,targetIndex,player,random,buildId);
                }
            }
            return damage;
        }

        private int CastAt(BattleRound report,CombatEffect effect,int index,Attributes player,SeededRandom random,int buildId=0,bool rage=false)
        {
            var enemy=Run.enemies[index];
            int attack=Math.Max(1,player.Stat("Attack")-enemy.defense);
            if(effect==CombatEffect.Poison)
            {
                ApplyEffect(attack,40,2,ref enemy.poison,ref enemy.poisonTurns);
                SkillStatus(report,index,enemy.hp,effect,buildId);
                return 0;
            }
            float percent=effect switch
            {
                CombatEffect.Lightning => 30*(1+(player.Get("ThunderAdd%")+player.Get("ThunderAttackAdd%"))/100),
                CombatEffect.Fire => 30,
                CombatEffect.Knife => 45*(1+player.Get("KnifeAdd%")/100),
                CombatEffect.SwordWave => (rage?100:70)*(1+player.Get("SwordkeeAdd%")/100),
                CombatEffect.Explosion => 60,
                _ => 30
            };
            float modifier=player.Get("DamageAdd%")+player.Get("SkillDamageAdd%")+(rage?player.Get("BigSkillDamageAdd%"):0);
            int damage=Strike(report,CombatActionKind.SkillStrike,CombatAction.Hero,index,
                Math.Max(1,(int)Math.Round(attack*percent*Math.Max(0,1+modifier/100)/100)),effect:effect,skillId:buildId);
            if(enemy.hp>0 && effect==CombatEffect.Fire && Roll(random,55+player.Get("TriggerFireBuffAdd%")))
            {
                ApplyEffect(attack,45,3,ref enemy.burn,ref enemy.burnTurns);
                SkillStatus(report,index,enemy.hp,CombatEffect.Fire,buildId);
            }
            if(enemy.hp>0 && (effect==CombatEffect.Explosion || effect==CombatEffect.Ice && Roll(random,20+player.Get("Frozen%"))))
            {
                enemy.stunTurns=1;
                SkillStatus(report,index,enemy.hp,CombatEffect.Ice,buildId);
            }
            if(effect!=CombatEffect.Knife) return damage;
            if(enemy.hp>0 && Roll(random,player.Get("KnifePoison%")))
            {
                ApplyEffect(attack,40,2,ref enemy.poison,ref enemy.poisonTurns);
                SkillStatus(report,index,enemy.hp,CombatEffect.Poison,buildId);
            }
            if(Roll(random,player.Get("KnifeRage%"))) IncreaseRecharge(25);
            if(Roll(random,player.Get("KnifeRevert%"))) Heal(report,player.Stat("HPMax"),player.Stat("HPMax")*3/100,buildId);
            if(enemy.hp>0 && Roll(random,player.Get("KnifeThunder%")))
                damage+=CastAt(report,CombatEffect.Lightning,index,player,random,buildId);
            return damage;
        }

        private static void SkillStatus(BattleRound report,int index,int hp,CombatEffect effect,int skillId,int amount=0) =>
            report.Add(CombatActionKind.SkillStatus,CombatAction.Hero,index,amount,hp,effect:effect,skillId:skillId);

        public BattleRound PlayRound()
        {
            if(Phase!=Phase.Battle) return null;
            Run.round++;
            var report=new BattleRound(Run.round);
            var random=new SeededRandom(Run.seed,Run.stage,500+Run.round);
            var skillRandom=new SeededRandom(Run.seed,Run.stage,800+Run.round);
            var player=Player();
            int maxHp=player.Stat("HPMax");
            int effectDamage=0;
            for(int i=0;i<Run.enemies.Count;i++)
            {
                var enemy=Run.enemies[i];
                if(enemy.hp<=0) continue;
                if(enemy.poisonTurns>0)
                {
                    effectDamage+=Strike(report,CombatActionKind.Poison,CombatAction.Hero,i,enemy.poison);
                    enemy.poisonTurns--;
                }
                if(enemy.burnTurns>0)
                {
                    effectDamage+=Strike(report,CombatActionKind.Burn,CombatAction.Hero,i,enemy.burn);
                    enemy.burnTurns--;
                }
                if(enemy.bleedTurns>0)
                {
                    effectDamage+=Strike(report,CombatActionKind.Bleed,CombatAction.Hero,i,enemy.bleed);
                    enemy.bleedTurns--;
                }
            }

            int damage=ResolveSkills(report,SkillTiming.Round,player,skillRandom,-1);
            int targetIndex=Run.enemies.FindIndex(enemy=>enemy.hp>0);
            bool critical=false;
            bool combo=false;
            if(targetIndex>=0)
            {
                var target=Run.enemies[targetIndex];
                int attack=Math.Max(1,player.Stat("Attack")-target.defense);
                int attackDamage;
                if(Run.recharge>=FullRecharge)
                {
                    Run.recharge=0;
                    attackDamage=CastAt(report,CombatEffect.SwordWave,targetIndex,player,skillRandom,rage:true);
                    attackDamage+=ResolveSkills(report,SkillTiming.Rage,player,skillRandom,targetIndex);
                }
                else
                {
                    critical=Roll(random,8+player.Get("CritRate%")+player.Get("NormalCritRate%"));
                    combo=Roll(random,player.Get("ComboRate%")+player.Get("NormalCombo%"));
                    float normal=Math.Max(0,1+(player.Get("NormalDamageAdd%")+player.Get("DamageAdd%"))/100);
                    attackDamage=Strike(report,CombatActionKind.Attack,CombatAction.Hero,targetIndex,
                        Math.Max(1,(int)Math.Round(attack*normal*(critical?1.6f:1))),critical);
                    if(combo && target.hp>0)
                        attackDamage+=Strike(report,CombatActionKind.Combo,CombatAction.Hero,targetIndex,
                            Math.Max(1,(int)Math.Round(attack*(1+player.Get("ComboDamageAdd%")/100))));
                    IncreaseRecharge(25);
                    attackDamage+=ResolveSkills(report,SkillTiming.Hit,player,skillRandom,targetIndex);
                }
                if(target.hp>0)
                {
                    int thunder=Math.Max(0,(int)Math.Round(attack*player.Get("ThunderBaseAttack%")/100));
                    if(thunder>0) attackDamage+=Strike(report,CombatActionKind.SkillStrike,CombatAction.Hero,targetIndex,
                        thunder,effect:CombatEffect.Lightning);
                    ApplyEffect(attack,player.Get("PoisonBaseAttack%"),3,ref target.poison,ref target.poisonTurns);
                    ApplyEffect(attack,player.Get("FireBaseAttack%"),2,ref target.burn,ref target.burnTurns);
                    ApplyEffect(attack,player.Get("BleedBaseAttack%"),2,ref target.bleed,ref target.bleedTurns);
                }
                float leech=Math.Clamp(player.Get("VampireRate%"),0,100);
                Heal(report,maxHp,(int)Math.Round(attackDamage*leech/100));
                damage+=attackDamage;
            }

            int petHit=0;
            int petDamage=PetDamage;
            if(petDamage>0 && Run.round%2==0)
            {
                int petTarget=Run.enemies.FindIndex(enemy=>enemy.hp>0);
                if(petTarget>=0)
                    petHit=Strike(report,CombatActionKind.PetStrike,CombatAction.Pet,petTarget,
                        Math.Max(1,(int)Math.Round(petDamage*(1+player.Get("PetDamageAdd%")/100))-Run.enemies[petTarget].defense));
            }

            int artifactHit=0;
            if(Run.round%3==0 && player.Get("ArtifactPower")>0)
            {
                int artifactTarget=Run.enemies.FindIndex(enemy=>enemy.hp>0);
                if(artifactTarget>=0)
                    artifactHit=Strike(report,CombatActionKind.ArtifactStrike,CombatAction.Artifact,artifactTarget,
                        (int)Math.Round(player.Get("ArtifactPower")));
            }

            int received=0, blocked=0, dodged=0, retaliated=0;
            float reduction=Math.Clamp(player.Get("GlobalDamageReduction%")+player.Get("DamageReduction%"),0,90);
            for(int i=0;i<Run.enemies.Count;i++)
            {
                var enemy=Run.enemies[i];
                if(enemy.hp<=0) continue;
                if(enemy.stunTurns>0) { enemy.stunTurns--; continue; }
                if(Roll(random,player.Get("Miss%")))
                {
                    dodged++;
                    report.Add(CombatActionKind.Dodge,i,CombatAction.Hero,0,Run.hp);
                    continue;
                }
                int incoming=Math.Max(1,(int)Math.Round((enemy.attack-player.Stat("Defence"))*(1-reduction/100)));
                int absorbed=Math.Min(Run.shield,incoming);
                Run.shield-=absorbed;
                blocked+=absorbed;
                int lost=Math.Min(Run.hp,incoming-absorbed);
                received+=lost;
                Run.hp-=lost;
                report.Add(CombatActionKind.EnemyAttack,i,CombatAction.Hero,lost,Run.hp,absorbed);
                if(Run.hp==0) break;
                if(Roll(random,player.Get("Revenge%")))
                {
                    retaliated+=Strike(report,CombatActionKind.Counter,CombatAction.Hero,i,
                        Math.Max(1,(int)Math.Round((player.Stat("Attack")-enemy.defense)*(1+player.Get("RevengeDamageAdd%")/100))));
                    retaliated+=ResolveSkills(report,SkillTiming.Counter,player,skillRandom,i);
                }
            }

            string bonuses=(critical?" 暴击":"")+(combo?" 连击":"")+(effectDamage>0?$" · 持续 {effectDamage}":"")+
                (blocked>0?$" · 护盾 {blocked}":"")+(dodged>0?$" · 闪避 {dodged}":"")+
                (retaliated>0?$" · 反击 {retaliated}":"")+(petHit>0?$" · 宠物 {petHit}":"")+(artifactHit>0?$" · 神器 {artifactHit}":"");
            Record($"回合 {Run.round} · 伤害 {damage} · 承受 {received}{bonuses}");
            if(Run.enemies.All(e=>e.hp==0) && Run.pendingWaves.Count>0)
            {
                if(Run.round>=RoundLimit)
                {
                    Record("超过回合上限。");
                    End(false);
                    report.Outcome=BattleRoundOutcome.Defeat;
                }
                else
                {
                    var next=data.monsters.First(r=>r.id==Run.pendingWaves[0]);
                    Run.pendingWaves.RemoveAt(0);
                    Run.waveIndex++;
                    LoadWave(next,Run.eventKind-2);
                    Record($"第 {Run.waveIndex+1} 波敌人登场。");
                    report.Outcome=BattleRoundOutcome.NextWave;
                }
            }
            else if(Run.enemies.All(e=>e.hp==0))
            {
                var ranges=ActiveChallenge==null ? Chapter.gold : Array.Empty<Range>();
                var range=ranges.Length>0 ? ranges[Math.Min(ranges.Length-1,Run.eventKind-3)] : null;
                var reward=Run.pendingBattleReward?.day==Run.stage && Run.stage>0?Run.pendingBattleReward:null;
                if(reward!=null)
                {
                    Run.battleRewards.Add(reward);
                    Run.pendingBattleReward=null;
                }
                Run.gold+=reward?.gold??(range==null?25:range.min+random.Next(Math.Max(1,range.max-range.min+1)));
                Heal(report,maxHp,maxHp/10);
                Record("战斗胜利。");
                if(ActiveChallenge is Challenge configured && configured.firstSkill>0)
                {
                    int offer=configured.waves[Run.stage-1].afterSkill;
                    if(offer>0) StartChallengeOffer(offer,SkillOfferOrigin.Event);
                }
                else GrantExperience(reward?.experience??(70+20*(Run.eventKind-3)));
                if(Phase!=Phase.Skill) FinishStage();
                report.Outcome=BattleRoundOutcome.Victory;
            }
            else if(Run.hp==0 && !Run.revived && Run.round<RoundLimit && player.Get("ReviveHpRate%")>0)
            {
                Run.revived=true;
                Run.hp=Math.Max(1,maxHp*(int)Math.Round(player.Get("ReviveHpRate%"))/100);
                report.Add(CombatActionKind.Revive,CombatAction.Hero,CombatAction.Hero,Run.hp,Run.hp);
                Record("恢复意识，继续战斗。");
            }
            else if(Run.hp==0 || Run.round>=RoundLimit)
            {
                Record(Run.hp==0 ? "生命耗尽。" : "超过回合上限。");
                End(false);
                report.Outcome=BattleRoundOutcome.Defeat;
            }
            foreach(var action in report.Actions) BattleStatistic.Record(Run.battleStatistics,action);
            Commit();
            return report;
        }

        private static bool Roll(SeededRandom random,float chance) =>
            chance>0 && random.Next(10000)<Math.Clamp(chance,0,100)*100;

        private int Strike(BattleRound report,CombatActionKind kind,int source,int target,int amount,bool critical=false,
            CombatEffect effect=CombatEffect.None,int skillId=0)
        {
            var enemy=Run.enemies[target];
            int dealt=Math.Min(enemy.hp,amount);
            enemy.hp-=dealt;
            if(dealt>0) report.Add(kind,source,target,dealt,enemy.hp,critical:critical,effect:effect,skillId:skillId);
            return dealt;
        }

        private void Heal(BattleRound report,int maxHp,int amount,int skillId=0)
        {
            int previous=Run.hp;
            if(amount>0) amount=Math.Max(0,(int)Math.Round(amount*(1+Player().Get("RecoverAdd%")/100)));
            Run.hp=Math.Min(maxHp,previous+amount);
            if(Run.hp!=previous) report.Add(CombatActionKind.Heal,CombatAction.Hero,CombatAction.Hero,Run.hp-previous,Run.hp,skillId:skillId);
        }

        private static void ApplyEffect(int attack,float percent,int duration,ref int damage,ref int turns)
        {
            if(percent<=0 || turns>0) return;
            damage=Math.Max(1,(int)Math.Round(attack*percent/(100*duration)));
            turns=duration;
        }

        public void Abandon()
        {
            if (Run==null || Phase==Phase.Result) return;
            End(false);
            Commit();
        }

        private void FinishStage()
        {
            if(Run.dailyLoop)
            {
                int dailyIndex=Array.IndexOf(ActiveChallenge.rewardStage,Run.stage);
                if(dailyIndex>=0 && (Profile.dailyRewardDate!=Run.dailyDate || Run.stage>Profile.dailyRewardStage) &&
                    !Run.claimed.Contains(Run.stage))
                {
                    Run.claimed.Add(Run.stage);
                    var drop=data.drops.First(row=>row.id==ActiveChallenge.dropID[dailyIndex]);
                    var random=new SeededRandom(Run.seed,Run.stage,35);
                    foreach(var reward in drop.rewards)
                        if(random.Next(100)<reward.rate) AddRunReward(reward);
                    Record($"达到每日远征第 {Run.stage} 步奖励。");
                }
                Run.phase=Phase.Trail;
                return;
            }
            if(ActiveChallenge is Challenge challenge)
            {
                foreach(var reward in challenge.waves[Run.stage-1].prizes ?? Array.Empty<Reward>())
                    AddRunReward(reward);
                if(Run.stage<challenge.waves.Length) Run.phase=Phase.Trail;
                else End(true);
                return;
            }
            int index=Array.IndexOf(Chapter.rewardStage,Run.stage);
            if (index>=0 && !Run.claimed.Contains(Run.stage))
            {
                Run.claimed.Add(Run.stage);
                int dropId=index<Chapter.dropID.Length ? Chapter.dropID[index] : 0;
                var rng=new SeededRandom(Run.seed,Run.stage,35);
                foreach(var reward in ChapterRewards(Chapter,Run.stage))
                {
                    if(rng.Next(100)>=reward.rate) continue;
                    AddRunReward(reward);
                }
                if(index<Chapter.staminaReturn.Length) Run.refundedStamina+=Chapter.staminaReturn[index];
                Record($"到达章节奖励节点 · 掉落组 {dropId}");
            }
            if (Run.stage>=Chapter.totalStage) End(true);
            else Run.phase=Phase.Trail;
        }

        private void AddRunReward(Reward reward)
        {
            if(reward.id==1) Run.milestoneGold+=reward.count;
            else if(reward.id==9) Run.refundedStamina+=reward.count;
            else AddReward(Run.loot,reward);
        }

        private void End(bool won)
        {
            if(Phase==Phase.Result) return;
            if(!won) Run.pendingBattleReward=null;
            Run.won=won;
            Run.phase=Phase.Result;
            if(Run.dailyLoop && Run.claimed.Count>0)
            {
                int previous=Profile.dailyRewardDate==Run.dailyDate?Profile.dailyRewardStage:0;
                Profile.dailyRewardDate=Run.dailyDate;
                Profile.dailyRewardStage=Math.Max(previous,Run.claimed.Max());
            }
            if(won) AddActivity(ActiveChallenge?.mode==6?30:20);
            if(won && ActiveChallenge is Challenge challenge)
            {
                if(challenge.mode==6)
                    Profile.lastDailyClaim=Run.dailyDate;
                else
                {
                    var progress=Progress(challenge.mode,challenge.track);
                    if(progress==null)
                    {
                        progress=new ChallengeProgress{mode=challenge.mode,track=challenge.track};
                        Profile.challenges.Add(progress);
                    }
                    progress.wins++;
                    if(challenge.mode==3)
                    {
                        progress.hasClearIds=true;
                        (progress.clearedLevels??=new List<int>()).Add(challenge.id);
                    }
                }
                if(!Run.dailyLoop) foreach(var reward in challenge.prizes) AddRunReward(reward);
            }
            Profile.gold+=Run.gold+Run.milestoneGold;
            foreach(var reward in Run.loot) AddReward(Profile.inventory,reward);
            Profile.stamina=Math.Min(30,Profile.stamina+Run.refundedStamina);
            if(!won || ActiveChallenge!=null) return;
            if(!Profile.cleared.Contains(Run.chapter)) Profile.cleared.Add(Run.chapter);
            var next=data.chapters.FirstOrDefault(c=>c.id>Run.chapter);
            if(next!=null && next.id>Profile.highest)
            {
                if(Profile.campChapter==Run.chapter) Profile.campChapter=next.id;
                Profile.highest=next.id;
            }
        }

        public void ReturnToCamp()
        {
            if(Phase!=Phase.Result) return;
            Save.run=null;
            Commit();
        }

        public string ItemName(int id) => data.items.FirstOrDefault(item=>item.id==id)?.name ?? "道具 "+id;

        private static void AddReward(List<Reward> inventory,Reward reward)
        {
            var existing=inventory.FirstOrDefault(item=>item.id==reward.id);
            if(existing!=null) existing.count+=reward.count;
            else inventory.Add(new Reward{id=reward.id,count=reward.count});
        }

        private void Record(string entry,JournalFortune fortune=JournalFortune.None,string attributes=null,string skillName=null)
        {
            Run.journal.Add(entry);
            Run.journalDays.Add(Run.stage);
            Run.journalFortunes.Add(fortune);
            Run.journalSerial++;
            if (!string.IsNullOrEmpty(attributes) || !string.IsNullOrEmpty(skillName))
                Run.journalModifiers.Add(new JournalModifier { serial = Run.journalSerial, attributes = attributes, skillName = skillName });
            if(Run.journal.Count>96)
            {
                Run.journal.RemoveAt(0);
                Run.journalDays.RemoveAt(0);
                Run.journalFortunes.RemoveAt(0);
                Run.journalModifiers.RemoveAll(modifier => modifier.serial <= Run.journalSerial - Run.journal.Count);
            }
        }

        internal int ChangeCount { get; private set; }

        private void Commit()
        {
            ChangeCount++;
            persist?.Invoke(Save);
        }
    }
}
