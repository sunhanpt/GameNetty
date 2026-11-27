using System;

namespace CapybaraRebuild
{
    internal static class SeaRaftMotion
    {
        internal const float CruiseX=.52f;
        internal const float HullWidth=.46f;
        internal const float ContactTime=.64f;
        internal const float ImpactTime=.54f;
        internal const float Duration=ContactTime+ImpactTime;
        private const float EnemyEntryX=1.27f;
        private const float Midpoint=(BattleMotion.HeroHome+BattleMotion.EnemyHome)*.5f;

        internal static bool IsRaftMap(int mapId) => mapId is 1001 or 1002 or 1003 or 1004 or 72010;

        internal static (float spread,float opacity) BowWave(float travel)
        {
            const float spacing=.24f;
            float spread=travel/spacing;
            spread-=(float)Math.Floor(spread);
            return (spread,(float)Math.Sin(Math.PI*spread));
        }

        internal static (float player,float enemy) Positions(float elapsed)
        {
            if(elapsed<ContactTime)
            {
                float progress=ApproachProgress(elapsed);
                return (CruiseX+(Midpoint-HullWidth*.5f-CruiseX)*progress,
                    EnemyEntryX+(Midpoint+HullWidth*.5f-EnemyEntryX)*progress);
            }

            float impact=elapsed-ContactTime;
            if(impact>=ImpactTime) return (BattleMotion.HeroHome,BattleMotion.EnemyHome);
            float remaining=1-impact/ImpactTime;
            float restHalfGap=(BattleMotion.EnemyHome-BattleMotion.HeroHome)*.5f;
            float halfGap=restHalfGap+(HullWidth*.5f-restHalfGap)*remaining*remaining;
            halfGap=Math.Max(HullWidth*.5f,halfGap+Recoil(elapsed)*.045f);
            return (Midpoint-halfGap,Midpoint+halfGap);
        }

        internal static float ApproachProgress(float elapsed)
        {
            float t=Math.Max(0,Math.Min(1,elapsed/ContactTime));
            return t*(.7f+.3f*t);
        }

        internal static float Recoil(float elapsed)
        {
            float impact=elapsed-ContactTime;
            if(impact<=0 || impact>=ImpactTime) return 0;
            float remaining=1-impact/ImpactTime;
            return (float)(Math.Sin(impact*23)*Math.Exp(-impact*6))*remaining;
        }

        internal static (float x,float y) EnemySeat(int index,int count)
        {
            int columns=Math.Min(count,3);
            int row=index/columns;
            int places=Math.Min(columns,count-row*columns);
            float x=BattleMotion.EnemyHome+(index%columns-(places-1)*.5f)*.16f;
            float y=count<=3?.662f:.635f+Math.Min(row,2)*.042f;
            return (x,y);
        }

        // Original sea lighting rolls a fresh palette on Next Day. Replay the roll from the run's seed.
        internal static int LightForDay(int seed,int day) => day<=1?0:new SeededRandom(seed,day,395).Next(3);
    }
}
