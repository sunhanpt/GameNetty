using System.Collections.Generic;
using System;

namespace CapybaraRebuild
{
    public enum CombatActionKind
    {
        Attack, Combo, Counter, PetStrike, ArtifactStrike, EnemyAttack,
        Dodge, Poison, Burn, Bleed, Heal, Revive, SkillStrike, SkillStatus
    }

    public enum CombatEffect { None, Lightning, Fire, Ice, Knife, SwordWave, Spear, Explosion, Poison, Bleed, Heal, Shield, Rage }

    public enum BattleRoundOutcome { Continue, NextWave, Victory, Defeat }

    [Serializable]
    public sealed class BattleStatistic
    {
        public CombatActionKind kind;
        public CombatEffect effect;
        public int skillId;
        public long value;

        internal bool IsRecovery => kind==CombatActionKind.Heal;

        internal static void Record(List<BattleStatistic> rows,CombatAction action)
        {
            // DamageStatisticsDataModule uses real HP and shield changes from the hurt report.
            bool recovery=action.Kind==CombatActionKind.Heal;
            if(action.Source>=0 || (recovery ? action.Amount<=1 : action.Target<0 ||
                action.Kind==CombatActionKind.SkillStatus)) return;
            long value=(long)action.Amount+(recovery?0:action.Absorbed);
            if(value<=0) return;
            var kind=action.Kind==CombatActionKind.Combo?CombatActionKind.Attack:action.Kind;
            var effect=action.SkillId==0?action.Effect:CombatEffect.None;
            int index=rows.FindIndex(r=>r.kind==kind && r.skillId==action.SkillId && r.effect==effect);
            var next=new BattleStatistic{kind=kind,effect=effect,skillId=action.SkillId,value=value};
            // Replace a row so the playback UI can keep a shallow snapshot of committed rounds.
            if(index<0) rows.Add(next);
            else
            {
                next.value+=rows[index].value;
                rows[index]=next;
            }
        }

        internal int RelativePercent(long maximum) => maximum<=0?0:Math.Max(1,(int)(value*100m/maximum));
    }

    internal static class BattleSpacing
    {
        internal const float BodyGap=.016f;

        internal static float StopBefore(float start,float destination,float obstacle,float clearance)
        {
            if(destination>start && obstacle>start)
                return Math.Min(destination,Math.Max(start,obstacle-clearance));
            if(destination<start && obstacle<start)
                return Math.Max(destination,Math.Min(start,obstacle+clearance));
            return destination;
        }
    }

    internal static class BattleMotion
    {
        // The sword's action nodes fire on frame 6 and end on frame 21 at 30 Hz.
        internal const float SwordImpact=6f/30f;
        internal const float IcicleImpact=2f/30f;
        internal const float KnifeFlight=8f/30f;
        internal const float MeleeAdvance=3f/30f;
        internal const float MeleeReturn=6f/30f;
        internal const float EncounterDuration=.60f;
        internal const float EncounterTravel=.48f;
        internal const float HeroTrail=.34f;
        internal const float HeroHome=.245f;
        internal const float EnemyHome=.772f;

        internal static bool CanTravel(Phase phase,bool paused,bool battlePending) =>
            !paused && (phase==Phase.Trail || battlePending);

        // EventMemberController.PlayerNormalSpeed, in world units per second.
        internal static float JourneySpeed(bool onRaft) => onRaft?2f:4f;

        internal static float MeleeFraction(float time,float advance,float returnAt,float duration)
        {
            if(time<=0 || time>=duration) return 0;
            if(time<advance) return EaseOut(time/advance);
            if(time<=returnAt) return 1;
            return 1-EaseOut((time-returnAt)/(duration-returnAt));
        }

        internal static float EncounterProgress(float time) =>
            EaseOut(Math.Max(0,Math.Min(1,time/EncounterDuration)));

        internal static float EncounterCameraProgress(float time)
        {
            const float headStart=.022f;
            float progress=Math.Max(0,Math.Min(1,(time-headStart)/(EncounterDuration-headStart)));
            return progress*progress*(3-2*progress);
        }

        internal static float EncounterHeroWorldX(float time) =>
            HeroTrail+(EncounterTravel+HeroHome-HeroTrail)*EncounterProgress(time);

        internal static float EncounterHeroScreenX(float time) =>
            EncounterHeroWorldX(time)-EncounterTravel*EncounterCameraProgress(time);

        private static float EaseOut(float progress) => progress*(2-progress);
    }

    public readonly struct CombatAction
    {
        public const int Hero = -1, Pet = -2, Artifact = -3;

        public readonly CombatActionKind Kind;
        public readonly int Source, Target, Amount, TargetHp, Absorbed;
        public readonly bool Critical;
        public readonly CombatEffect Effect;
        public readonly int SkillId;

        internal CombatAction(CombatActionKind kind, int source, int target, int amount,
            int targetHp, int absorbed = 0, bool critical = false, CombatEffect effect = CombatEffect.None, int skillId = 0)
        {
            Kind = kind;
            Source = source;
            Target = target;
            Amount = amount;
            TargetHp = targetHp;
            Absorbed = absorbed;
            Critical = critical;
            Effect = effect;
            SkillId = skillId;
        }
    }

    public sealed class BattleRound
    {
        private readonly List<CombatAction> actions = new List<CombatAction>();

        public int Number { get; }
        public IReadOnlyList<CombatAction> Actions => actions;
        public BattleRoundOutcome Outcome { get; internal set; }

        internal BattleRound(int number) => Number = number;

        internal void Add(CombatActionKind kind, int source, int target, int amount, int targetHp,
            int absorbed = 0, bool critical = false, CombatEffect effect = CombatEffect.None, int skillId = 0) =>
            actions.Add(new CombatAction(kind, source, target, amount, targetHp, absorbed, critical, effect, skillId));
    }
}
