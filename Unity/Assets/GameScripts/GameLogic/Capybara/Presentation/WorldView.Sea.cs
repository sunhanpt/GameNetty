using UnityEngine;
using UnityEngine.UI;
using static CapybaraRebuild.AdventureScreen;

namespace CapybaraRebuild
{
    internal sealed partial class WorldView
    {
        private const float SeaLightTransition=2f;
        private readonly RawImage seaDusk,seaNight,seaSurface;
        private SeaRaft playerRaft,enemyRaft;
        private Run seaRun;
        private int seaLight;
        private float seaLightElapsed;
        private Vector2 seaLightFrom,seaLightTo,seaLightBlend;

        private bool UsesSeaRafts => (campView || journeyMap) && SeaRaftMotion.IsRaftMap(LandscapeId);

        private sealed class SeaRaft
        {
            internal RectTransform root;
            internal RawImage wake,hull,bowWaves;
        }

        private RawImage CreateSeaLighting(RectTransform frame,string label)
        {
            var lighting=ImageAt(frame,Texture(label),(0,0,1,1));
            lighting.name=label;
            lighting.transform.SetSiblingIndex(UiChild(frame,"Arena").GetSiblingIndex());
            lighting.enabled=false;
            return lighting;
        }

        private void ResetSeaScene(Adventure game,bool battle)
        {
            var run=IsSeaJourney && LandscapeId==DaytimeSeaMap?game.Run:null;
            seaSurface.enabled=IsSeaJourney && seaSurface.texture!=null;
            seaSurface.rectTransform.anchorMax=new Vector2(1,LandscapeId==DaytimeSeaMap?.881f:.76f);
            if(run!=seaRun)
            {
                seaRun=run;
                seaLight=run==null?0:SeaRaftMotion.LightForDay(run.seed,run.stage);
                seaLightBlend=seaLightFrom=seaLightTo=SeaLightWeights(seaLight);
                seaLightElapsed=SeaLightTransition;
            }
            if(run!=null) SetSeaTime(run.seed,run.stage);
            ApplySeaLighting();

            playerRaft=enemyRaft=null;
            if(!UsesSeaRafts) return;
            playerRaft=CreateSeaRaft(false);
            if(battle && game.Run.enemies.Count>0) enemyRaft=CreateSeaRaft(true);
        }

        private SeaRaft CreateSeaRaft(bool enemy)
        {
            var raft=InstantiateUi("Widgets/SeaRaft",actorLayer);
            raft.name=enemy?"Enemy raft":"Player raft";
            var wake=UiChild(raft,"Wake").GetComponent<RawImage>();
            var hull=UiChild(raft,"Hull").GetComponent<RawImage>();
            if(enemy) hull.uvRect=new Rect(1,0,-1,1);
            return new SeaRaft{root=raft,wake=wake,hull=hull,bowWaves=UiChild(raft,"Bow waves").GetComponent<RawImage>()};
        }

        private static Vector2 SeaLightWeights(int light) => light==1?Vector2.right:light==2?Vector2.up:Vector2.zero;

        private void SetSeaTime(int seed,int day)
        {
            int light=SeaRaftMotion.LightForDay(seed,day);
            if(light==seaLight) return;
            seaLight=light;
            seaLightFrom=seaLightBlend;
            seaLightTo=SeaLightWeights(light);
            seaLightElapsed=0;
        }

        private void ApplySeaLighting()
        {
            SetLayer(seaDusk,seaLightBlend.x);
            SetLayer(seaNight,seaLightBlend.y);

            void SetLayer(RawImage layer,float alpha)
            {
                layer.enabled=seaRun!=null && layer.texture!=null && alpha>0;
                layer.color=new Color(1,1,1,alpha);
            }
        }

        private void AdvanceSeaEncounter(float dt)
        {
            float previous=dashElapsed;
            dashElapsed+=dt;
            float progress=SeaRaftMotion.ApproachProgress(dashElapsed);
            travel+=(progress-encounterProgress)*.30f;
            encounterProgress=progress;
            if(previous<SeaRaftMotion.ContactTime && dashElapsed>=SeaRaftMotion.ContactTime && enemyRaft!=null)
                SplashAtContact();
            if(dashElapsed>=SeaRaftMotion.Duration) dashElapsed=-1;
        }

        private void SplashAtContact()
        {
            if(encounterDustBrush==null) encounterDustBrush=MakeDashBrush(false);
            float seam=(BattleMotion.HeroHome+BattleMotion.EnemyHome)*.5f;
            for(int i=0;i<6;i++)
            {
                float direction=i%2==0?-1:1;
                var point=new Vector2(seam+direction*.010f,.642f+i*.003f);
                var splash=ImageAt(effects,encounterDustBrush,(point.x,point.y,point.x,point.y));
                splash.name="Raft impact spray";
                splash.color=new Color(.79f,.96f,1,.85f);
                splash.rectTransform.sizeDelta=new Vector2(11+i*2,5+i);
                particles.Add(new Particle{rect=splash.rectTransform,point=point,graphic=splash,color=splash.color,
                    velocity=new Vector2(direction*(.085f+i*.011f),.08f+i*.015f),life=.30f+i*.016f,grow=.65f});
            }
        }

        private void AnimateSeaRafts(float dt,bool cruising)
        {
            seaLightElapsed=Mathf.Min(SeaLightTransition,seaLightElapsed+dt);
            float blend=Mathf.SmoothStep(0,1,seaLightElapsed/SeaLightTransition);
            seaLightBlend=Vector2.Lerp(seaLightFrom,seaLightTo,blend);
            ApplySeaLighting();
            seaSurface.uvRect=new Rect(Mathf.Repeat(travel,1),0,1,1);

            float playerX=hero.position.x,enemyX=BattleMotion.EnemyHome,recoil=0;
            if(approachPending || dashElapsed>=0)
            {
                (playerX,enemyX)=SeaRaftMotion.Positions(dashElapsed);
                recoil=SeaRaftMotion.Recoil(dashElapsed);
            }
            float playerLift=DrawRaft(playerRaft,playerX,hero.position.y,0,recoil);
            DrawBowWaves(playerRaft,cruising);
            float playerOffset=playerX-hero.position.x;
            Color light=Color.Lerp(Color.white,Tint(0xF4D9C6),seaLightBlend.x*.50f);
            light=Color.Lerp(light,Tint(0xB7CBED),seaLightBlend.y*.65f);
            PoseRider(hero,playerOffset,playerLift,recoil*12);
            PoseRider(pet,playerOffset,playerLift,recoil*12);
            if(enemyRaft==null) return;
            float enemyLift=DrawRaft(enemyRaft,enemyX,hero.position.y,1.6f,-recoil);
            foreach(var enemy in enemies) PoseRider(enemy,enemyX-BattleMotion.EnemyHome,enemyLift,-recoil*12);

            void PoseRider(Actor rider,float offset,float lift,float tilt)
            {
                if(rider==null) return;
                rider.baseColor=light;
                Pose(rider,offset,dt,lift,tilt);
            }
        }

        private void DrawBowWaves(SeaRaft raft,bool cruising)
        {
            var waves=raft.bowWaves;
            waves.enabled=cruising;
            if(!cruising) return;

            var (spread,opacity)=SeaRaftMotion.BowWave(travel);
            float width=raft.root.rect.width;
            waves.rectTransform.anchoredPosition=new Vector2(width*(.02f+.12f*spread),0);
            waves.rectTransform.sizeDelta=new Vector2(.28f,.39f)*width*Mathf.Lerp(.76f,1.15f,spread);
            waves.color=new Color(.82f,.96f,1,opacity*.95f);
        }

        private float DrawRaft(SeaRaft raft,float x,float y,float wavePhase,float recoil)
        {
            float wave=Mathf.Sin(time*3.3f+wavePhase);
            float width=ViewportWidth*SeaRaftMotion.HullWidth;
            float lift=width*(wave*.011f+Mathf.Abs(recoil)*.023f);
            raft.root.anchorMin=raft.root.anchorMax=new Vector2(x,y);
            raft.root.sizeDelta=new Vector2(width,width*186/389);
            raft.root.anchoredPosition=new Vector2(0,lift);
            raft.root.localRotation=Quaternion.Euler(0,0,wave*.7f+recoil*12);
            raft.wake.color=new Color(.83f,.96f,1,.68f+.16f*wave);
            Color baseTint=wavePhase==0?Color.white:Tint(0xDECAB8);
            raft.hull.color=baseTint*Color.Lerp(Color.white,Tint(0xA5AFC6),seaLightBlend.y*.65f);
            return lift;
        }
    }
}
