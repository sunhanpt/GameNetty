using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static CapybaraRebuild.AdventureScreen;

namespace CapybaraRebuild
{
    internal sealed partial class WorldView
    {
        private readonly RectTransform root;
        private readonly Font font;
        private readonly RawImage sky;
        private readonly RawImage[] clouds;
        private readonly Image backdrop;
        private readonly RawImage[] far, middle, ground;
        private readonly Color defaultGroundTint;
        private readonly RawImage seaForeground;
        private readonly RectTransform arena;
        private int arenaId;
        private readonly RawImage offlineDistant, offlineRoadside;
        private readonly Image offlineRoad;
        private ChapterMap map;
        private readonly Dictionary<int,Texture2D> mapTextures=new Dictionary<int,Texture2D>();
        private const int WindyTownMap=2008;
        private const int WhisperWoodlandMap=2009;
        private const int DaytimeSeaMap=1001;
        private int LandscapeId => map?.id switch {72008=>WindyTownMap,72009=>WhisperWoodlandMap,72010=>DaytimeSeaMap,_=>map?.id??0};
        // Upper road edge measured in the original 1080-wide middle tiles.
        private const float TownRoadTop=694f, WoodlandRoadTop=994f;
        private static readonly int[] TownMiddle={20821,20822,20823};
        private static readonly int[] TownGround={12,14,13};
        private bool campView, journeyMap, approachPending;
        private readonly RectTransform actorLayer, effects;
        private readonly RawImage[] encounterRibbons;
        private Texture2D encounterRibbonBrush, encounterDustBrush;
        private readonly List<Actor> enemies=new List<Actor>();
        private readonly List<Particle> particles=new List<Particle>();
        private readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        private Sprite pill;
        private Actor hero,pet,prop;
        private RectTransform journeyProp;
        private JourneyModel journeyPropModel;
        private Vector2 journeyPropViewport;
        private bool journeyPropAtSea;
        private Transform foregroundAttacker,attackerNextSibling;
        private float time, walk=-1, travel;
        private float dashElapsed=-1, encounterProgress=1;
        private int nextDashPuff;
        private static readonly float[] DashPuffTimes={.055f,.14f,.23f,.34f,.49f};
        private ActorAnimationGraphic fightBanner;
        private float fightElapsed;
        private float icicleEndsAt;
        public bool HasEncounterIntro => approachPending;
        public bool IsEncounterApproaching => dashElapsed>=0;
        public bool IsSeaJourney => journeyMap && UsesSeaRafts;
        public float JourneyProgress => Mathf.Clamp01(walk/.62f);
        private float showSkillNameAfter;
        private const float StrikeMotion = .33f;
        private const float ProjectileTravel = .20f;
        private const float ChapterCameraHeight = 18.2f;
        private const float TrailFootY=.662f;
        // Player_KaPiBaLa: Point_Head + Point_HeadTop, measured from Point_Foot.
        private static readonly Vector2 KnifeCastOffset=new Vector2(.025f,2.342f);
        // Bullet_BingCi's normalized texture-sheet animation advances quickly during emergence.
        private static readonly AnimationCurve IcicleFrameCurve=new AnimationCurve(
            new Keyframe(0,0,0,1.745576f),
            new Keyframe(.2458914f,.4292221f,1.745576f,.4787044f),
            new Keyframe(.5294451f,.5649605f,.4787044f,.9245244f),
            new Keyframe(1,1,.9245244f,1));
        private readonly Color white=Tint(0xFFF1D4);

        private sealed class Actor
        {
            public RectTransform root, image, lifeTrack;
            public Image bar, rage;
            public Text life;
            public Color baseColor=Color.white;
            public Graphic art;
            public ActorAnimationGraphic animation;
            public Vector2 position;
            public float hit=-1, dodge=-1, motion=-1, width, height;
            public Vector2 reach;
            public float attackDuration, attackStart, returnAt, motionDuration;
            public string attackPose="attack";
            public bool caster, ranged;
            public float meleeOffset=1.8f;
            public int hp,max;
        }

        private sealed class Particle
        {
            public RectTransform rect;
            public Vector2 point, velocity;
            public float age, life;
            public Graphic graphic;
            public Color color;
            public RawImage sheet;
            public AnimationCurve frameOverTime;
            public int columns, rows, frames;
            public float rotation, spin, grow;
            public bool encounterDust;
            public bool fade=true;
        }

        public WorldView(RectTransform canvas,Font font)
        {
            this.font=font;
            root=LoadView("World",canvas);
            var frame=UiChild(root,"Frame");
            backdrop=frame.GetComponent<Image>();
            RawImage Layer(string name) => UiChild(frame,name).GetComponent<RawImage>();
            sky=Layer("Distant/Sky");
            far=new[]{Layer("Distant/Far 1"),Layer("Distant/Far 2")};
            clouds=new[]{Layer("Distant/Cloud 1"),Layer("Distant/Cloud 2")};
            offlineDistant=Layer("Distant/Offline distant scenery");
            middle=new[]{Layer("Near/Middle 1"),Layer("Near/Middle 2")};
            ground=new[]{Layer("Near/Ground 1"),Layer("Near/Ground 2"),Layer("Near/Ground 3")};
            defaultGroundTint=ground[0].color;
            secondGround=new[]{Layer("Near/Upper ground 1"),Layer("Near/Upper ground 2"),Layer("Near/Upper ground 3")};
            snowCloud=Layer("Distant/Snow cloud band");
            snowRidge=Layer("Distant/Snow ridge ornaments");
            snowRoadside=Layer("Near/Snow roadside ornaments");
            snowForeground=Layer("Near/Snow foreground ornaments");
            seaForeground=Layer("Near/Sea foreground");
            seaDusk=CreateSeaLighting(frame,"SeaDusk");
            seaNight=CreateSeaLighting(frame,"SeaNight");
            offlineRoad=UiChild(frame,"Near/Offline road").GetComponent<Image>();
            offlineRoad.enabled=false;
            offlineRoadside=Layer("Near/Offline roadside");
            arena=UiChild(frame,"Arena");
            seaSurface=ImageAt(frame,Texture("SeaCurrent"),(0,.548f,1,.881f));
            seaSurface.name="Sea surface current";
            seaSurface.transform.SetSiblingIndex(arena.GetSiblingIndex());
            actorLayer=UiChild(frame,"Actors");
            effects=UiChild(frame,"Effects");
            encounterRibbons=new[]{Layer("Effects/Encounter speed ribbon"),Layer("Effects/Encounter speed highlight")};
        }

        public Texture2D Texture(string name)
        {
            if(!textures.TryGetValue(name,out var value) || value==null)
            {
                value=name.StartsWith("Map/",StringComparison.Ordinal) ? Resources.Load<Texture2D>(name) :
                    Resources.Load<Texture2D>("Reference/"+name)??Resources.Load<Texture2D>("Art/"+name);
                if(value!=null) textures[name]=value;
            }
            return value;
        }

        internal Sprite PillSprite()
        {
            if(pill!=null) return pill;
            pill=Resources.Load<Sprite>("Reference/Pill");
            return pill;
        }

        private Texture2D MapTexture(int id)
        {
            if(!mapTextures.TryGetValue(id,out var texture))
            {
                texture=Resources.Load<Texture2D>("Map/"+id);
                mapTextures.Add(id,texture);
            }
            return texture;
        }

        private void SetMap(ChapterMap value)
        {
            if(map?.id!=value?.id || map?.hasArt!=value?.hasArt)
            {
                travel=0;
                ConfigureOfflineScenery(value);
            }
            map=value;
            SetLandScenery();
            if(offlineRoad.enabled) return;
            sky.texture=(map?.sky?.Length>0 ? MapTexture(map.sky[0]) : null) ?? Texture("Town-sky-1");
            sky.enabled=sky.texture!=null;
            var cloud=map!=null?Texture("Map/cloud-"+LandscapeId):null;
            clouds[0].texture=cloud;
            clouds[1].texture=(cloud!=null?Texture("Map/cloud-"+LandscapeId+"-2"):null)??cloud;
            foreach(var tile in clouds) tile.enabled=tile.texture!=null;
            seaForeground.texture=LandscapeId==DaytimeSeaMap?MapTexture(1003):null;
            seaForeground.enabled=seaForeground.texture!=null;
            backdrop.color=campView?Tint(0x87B97D):
                ColorUtility.TryParseHtmlString(map?.groundColor,out var tint)?tint:Tint(0xB0D4B2);
            var groundTint=landScenery!=null?backdrop.color:defaultGroundTint;
            foreach(var tile in ground) tile.color=groundTint;
            foreach(var tile in secondGround) tile.color=groundTint;
        }

        private void ConfigureOfflineScenery(ChapterMap value)
        {
            UnityEngine.Object.Destroy(offlineDistant.texture);
            UnityEngine.Object.Destroy(offlineRoadside.texture);
            offlineDistant.texture=offlineRoadside.texture=null;
            bool enabled=value!=null && !value.hasArt;
            offlineDistant.enabled=offlineRoadside.enabled=offlineRoad.enabled=enabled;
            if(!enabled) return;

            var terrain=OfflineLandscape.TerrainColor(value);
            offlineDistant.texture=OfflineLandscape.Distant(value.id,terrain);
            offlineRoadside.texture=OfflineLandscape.Roadside(value.id,terrain);
            offlineRoad.color=Color.Lerp(terrain,Tint(0xDDD1AE),.61f);
            sky.enabled=seaForeground.enabled=false;
            foreach(var layer in new[]{clouds,far,middle,ground})
                foreach(var tile in layer) tile.enabled=false;
        }

        private void SetArena(int id)
        {
            if(id==arenaId) return;
            arenaId=id;
            foreach(Transform child in arena) UnityEngine.Object.Destroy(child.gameObject);
            arena.gameObject.SetActive(id!=0);
            if(id==0) return;
            // Local transforms and sprite pivots of the two scenery renderers in each art-map prefab.
            var (stem,backX,backY,frontY,scaleX,backScaleY,frontScaleY)=id switch
            {
                10011 or 10012=>("tower_map1_normal",0f,1.9f,-8.93f,1.46035f,.44178f,1.22023f),
                10013=>("tower_map2_normal",0f,5.12f,6.66f,1.51f,1.12f,1.488f),
                10014=>("tower_map2_boss",0f,5.88f,10.92f,1.51f,1.12f,1.488f),
                10015=>("tower_map3_normal",0f,4.01f,12.21f,1.51f,1.44f,1.488f),
                10016=>("tower_map3_boss",.22f,7.13f,12.36f,1.51f,1.44f,1.488f),
                10017=>("tower_map4_normal",0f,3.73f,5.86f,1.51f,1.44f,1.488f),
                10018=>("tower_map4_boss",0f,5.68f,11.72f,1.51f,1.44f,1.488f),
                100005=>("worldboss_map",0f,-.8f,1.73f,1.2f,1.2f,1.2f),
                _=>throw new InvalidOperationException("Unknown art map "+id)
            };
            bool firstTower=id==10011 || id==10012;
            PlaceArenaLayer(stem+"_back",backX,backY,scaleX,backScaleY,0);
            PlaceArenaLayer(stem+"_front",0,frontY,scaleX,frontScaleY,firstTower?.5f:1f);
            if(firstTower)
            {
                foreach(var point in new[]{new Vector2(-4.3f,5.94f),new Vector2(-5.21f,5.27f),
                    new Vector2(-2.99f,3.29f),new Vector2(4.274f,2.23f),new Vector2(5.204f,3.03f)})
                    PlaceArenaLayer("tower_map1_light",point.x,point.y,.8f,.8f,.5f);
            }
        }

        private void PlaceArenaLayer(string key,float x,float y,float scaleX,float scaleY,float pivotY)
        {
            var art=Texture("Map/"+key);
            if(art==null) return;
            // Convert the original 100 pixels per world unit to the portrait canvas reference.
            const float unitsToCanvas=ReferenceWidth/10.8f;
            var image=MountRaw(arena, "WorldView/PlaceArenaLayer/image", texture: art);
            image.name=key;
            var rect=image.rectTransform;
            rect.pivot=new Vector2(.5f,pivotY);
            rect.sizeDelta=new Vector2(art.width*scaleX,art.height*scaleY)*(unitsToCanvas/100f);
            rect.anchoredPosition=new Vector2(x,y)*unitsToCanvas;
        }

        private void ScrollLayer(RawImage[] tiles,int[] ids,float position,float top,float tileWidth,float layerHeight=0)
        {
            if(tileWidth<=0) return;
            float offset=Mathf.Repeat(position,tileWidth);
            int tileIndex=Mathf.FloorToInt(position/tileWidth);
            for(int i=0;i<tiles.Length;i++)
            {
                var tile=tiles[i];
                int count=ids?.Length??0;
                int index=count>0 ? (tileIndex+i)%count : 0;
                if(index<0) index+=count;
                tile.texture=count>0?MapTexture(ids[index]):null;
                if(tile.texture!=null)
                {
                    float height=layerHeight>0?layerHeight:tileWidth*tile.texture.height/tile.texture.width;
                    tile.rectTransform.sizeDelta=new Vector2(tileWidth+.5f,height);
                    tile.rectTransform.anchoredPosition=new Vector2(i*tileWidth-offset,top-root.rect.height);
                }
                tile.enabled=tile.texture!=null;
            }
        }

        public void Refresh(Adventure game,bool newWave=false)
        {
            EndAction();
            foreach(Transform child in actorLayer)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
            enemies.Clear();
            foreach(var particle in particles)
            {
                particle.rect.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(particle.rect.gameObject);
            }
            particles.Clear();
            icicleEndsAt=time;
            if(fightBanner!=null)
            {
                fightBanner.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(fightBanner.gameObject);
                fightBanner=null;
            }
            walk=-1;
            dashElapsed=-1;
            foreach(var ribbon in encounterRibbons) ribbon.enabled=false;
            hero=pet=prop=null;
            journeyProp=null;
            journeyPropModel=null;
            bool camp=game==null || game.Phase==Phase.Camp;
            bool battle=!camp && game.Phase==Phase.Battle;
            if(campView!=camp) travel=0;
            campView=camp;
            SetMap(game?.Map);
            SetArena(game?.ActiveChallenge?.artMapId??0);
            journeyMap=arenaId==0 && game?.UsesJourneyMovement==true;
            approachPending=journeyMap && battle && game.Run.round==0 && !newWave;
            encounterProgress=approachPending?0:1;
            ResetSeaScene(game,battle);
            float trailX=UsesSeaRafts?SeaRaftMotion.CruiseX:BattleMotion.HeroTrail;
            hero=Create("Hero",camp?new Vector2(.505f,.50f):new Vector2(battle?BattleMotion.HeroHome:trailX,TrailFootY),camp?242:218,
                showLife:battle);
            if(battle)
            {
                hero.hp=game.Run.hp;
                hero.max=game.Player().Stat("HPMax");
                UpdateBar(hero);
                UpdateRage(game.Run.recharge);
            }
            if(game?.ActivePet!=null)
            {
                float petX=UsesSeaRafts && !battle?trailX-.135f:.11f;
                pet=Create("Pet",camp?new Vector2(.67f,.50f):new Vector2(petX,.662f),110);
            }
            if(battle)
            {
                var run=game.Run;
                int count=run.enemies.Count;
                for(int i=0;i<count;i++)
                {
                    var enemy=run.enemies[i];
                    float x=count==1?BattleMotion.EnemyHome:.59f+i*.31f/Math.Max(1,count-1);
                    float y=count==1?.662f:.652f+(i%2)*.042f;
                    if(UsesSeaRafts) (x,y)=SeaRaftMotion.EnemySeat(i,count);
                    var member=game.EnemyMember(enemy,i);
                    int modelId=member?.modelId??game.EnemyModel(enemy,i);
                    bool mage=modelId==220081;
                    bool boss=member!=null?member.roleType==5:run.eventKind==5 && run.pendingWaves.Count==0 && i==0;
                    string art=mage?"Mage":boss?"Boss":member?.roleType==4?"Wolf":"Slime";
                    float width=count>2?126:boss?221:190;
                    float modelScale=member?.modelScale??0f;
                    // The base enemy width was calibrated at the common 0.5 member scale.
                    if(modelScale>0) width*=modelScale/.5f;
                    width=Mathf.Min(width,ViewportWidth*.42f/ActorScale);
                    if(count>1)
                        width=Mathf.Min(width,Mathf.Max(20,(.31f/(count-1)-BattleSpacing.BodyGap)*ViewportWidth/ActorScale));
                    var actor=Create(art,new Vector2(x,y),width,enemy.name,true,modelId);
                    if(modelId>0) actor.root.name=$"{enemy.name} [{modelId}]";
                    actor.caster=mage;
                    actor.ranged=mage || (member?.rangedAttack??false);
                    if(member!=null && member.meleeOffset>0) actor.meleeOffset=member.meleeOffset;
                    actor.hp=enemy.hp;
                    actor.max=enemy.maxHp;
                    actor.root.gameObject.SetActive(enemy.hp>0);
                    UpdateBar(actor);
                    enemies.Add(actor);
                }
                if(approachPending) ShowBattleBars(false);
            }
            else if(!camp && (game.HasJourneyChoice || game.HasWheel))
            {
                if(game.Run.eventKind==12) DrawFishingFloat();
                else if(game.CurrentJourneyModel() is JourneyModel model) DrawJourneyModel(model,game.JourneyModels);
            }
            else if(!camp && game.Phase==Phase.Event)
            {
                string art=game.Run.eventKind switch
                {
                    // The battle action creates the actual enemies at the start of the approach.
                    3 or 4 or 5 or 12 => null,
                    6 => "Spark",
                    9 => "Chest",
                    _ => "Camp"
                };
                if(art!=null) prop=Create(art,new Vector2(.74f,.66f),177);
            }
            Animate(0,game?.Phase??Phase.Camp,paused:false,battlePrompt:game?.IsBattlePrompt==true);
        }

        private void DrawJourneyModel(JourneyModel model,JourneyEventModels layout)
        {
            bool sea=layout.UsesSeaOffsets(map?.id??0) || LandscapeId==DaytimeSeaMap;
            float offset=sea?model.seaOffset:model.landOffset;
            const float x=.74f;
            float y=.66f+(offset-3.2f)*.012f;
            string name=$"Journey model {model.id} - {model.art}";
            if(model.sourceModelId>0)
            {
                var actor=Create(model.art,new Vector2(x,y),175,name,modelId:model.sourceModelId,allowFallback:false);
                if(actor!=null)
                {
                    prop=actor;
                    return;
                }
            }
            var atlas=Texture("JourneyPropsAtlas");
            if(atlas==null) return;
            var image=ImageAt(actorLayer,atlas,(x,y,x,y));
            image.name=name;
            image.rectTransform.pivot=new Vector2(.5f,0);
            image.rectTransform.sizeDelta=Vector2.one*(228*ActorScale);
            int column=model.cell%layout.columns, row=model.cell/layout.columns;
            image.uvRect=new Rect((float)column/layout.columns,1-(float)(row+1)/layout.rows,
                1f/layout.columns,1f/layout.rows);
            if(model.worldBounds?.HasArea!=true || model.artBounds?.HasArea!=true) return;

            journeyProp=image.rectTransform;
            journeyPropModel=model;
            journeyPropAtSea=sea;
            journeyPropViewport=Vector2.zero;
            LayoutJourneyProp();
        }

        private void LayoutJourneyProp()
        {
            if(journeyProp==null) return;
            var viewport=actorLayer.rect.size;
            if(viewport==journeyPropViewport || viewport.x<=0 || viewport.y<=0) return;
            journeyPropViewport=viewport;
            // The chapter camera follows the player's movement node, which stops at the event root's X.
            // Project the measured prefab bounds through its 9.1-unit orthographic camera.
            const float playerOriginY=.674f; // Calibrated against the original town prayer recording.
            float pixelsPerUnit=ChapterWorldScale(viewport);
            var world=journeyPropModel.worldBounds;
            var art=journeyPropModel.artBounds;
            float offsetX=journeyPropAtSea?journeyPropModel.seaOffsetX:journeyPropModel.landOffsetX;
            float offsetY=journeyPropAtSea?journeyPropModel.seaOffset:journeyPropModel.landOffset;
            var baseAnchor=new Vector2(.5f,playerOriginY);
            journeyProp.anchorMin=journeyProp.anchorMax=baseAnchor;
            journeyProp.pivot=new Vector2(art.x+art.width*.5f,art.y);
            journeyProp.sizeDelta=new Vector2(world.width/art.width,world.height/art.height)*pixelsPerUnit;
            float bottom=world.y+offsetY;
            journeyProp.anchoredPosition=new Vector2(world.x+world.width*.5f+offsetX,bottom)*pixelsPerUnit;
            if(bottom>=0) journeyProp.SetAsFirstSibling();
        }

        private static float ChapterWorldScale(Vector2 viewport) =>
            Mathf.Min(viewport.x/(ChapterCameraHeight*9f/16f),viewport.y/ChapterCameraHeight);

        private void DrawFishingFloat()
        {
            var ripple=MountPart(actorLayer, "WorldView/DrawFishingFloat/ripple").GetComponent<Image>();
            ripple.name="Fishing ripple";
            ripple.sprite=PillSprite();
            var marker=MountPart(actorLayer, "WorldView/DrawFishingFloat/marker").GetComponent<Image>();
            marker.name="Fishing float";
            marker.sprite=PillSprite();
            var tip=MountPart(marker.rectTransform, "WorldView/DrawFishingFloat/tip").GetComponent<Image>();
            tip.sprite=marker.sprite;
        }

        private Actor Create(string art,Vector2 point,float width,string label=null,bool showLife=false,int modelId=0,bool allowFallback=true)
        {
            var rt=NewRect(label??art,actorLayer,(point.x,point.y,point.x,point.y));
            rt.pivot=new Vector2(.5f,0);
            var texture=Texture(art);
            float height=texture!=null ? width*texture.height/texture.width : width;
            rt.sizeDelta=new Vector2(width,height);
            Graphic image=null;
            ActorAnimationGraphic animation=null;
            if(modelId>0 || art=="Hero" || art=="Mage")
            {
                var node=MountPart(rt, "WorldView/Create/node", name: "Spine "+(modelId>0?modelId.ToString():art));
                node.gameObject.SetActive(false);
                animation=node.GetComponent<ActorAnimationGraphic>();
                if(modelId>0?animation.LoadEnemy(modelId):animation.Load(art))
                {
                    height=width/animation.SourceAspect;
                    image=animation;
                    node.gameObject.SetActive(true);
                }
                else
                {
                    UnityEngine.Object.Destroy(node.gameObject);
                    animation=null;
                }
            }
            if(image==null)
            {
                if(!allowFallback)
                {
                    rt.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(rt.gameObject);
                    return null;
                }
                var shadow=MountPart(rt, "WorldView/Create/shadow");
                shadow.localScale=new Vector3(1,.65f,1);
                image=MountRaw(rt, "WorldView/Create/ImageAt 2", texture: texture);
            }
            var actor=new Actor{root=rt,image=image.rectTransform,art=image,animation=animation,position=point,width=width,height=height};
            if(showLife)
            {
                var vitals=InstantiateUi(label==null?"Widgets/ActorVitals":"Widgets/EnemyVitals",rt);
                actor.lifeTrack=UiChild(vitals,"Health");
                actor.bar=UiChild(actor.lifeTrack,"Fill").GetComponent<Image>();
                actor.life=UiChild(vitals,"Value").GetComponent<Text>();
                if(label==null) actor.rage=UiChild(vitals,"Rage/Fill").GetComponent<Image>();
            }
            return actor;
        }

        public void BeginWalk(int seed,int nextDay)
        {
            walk=0;
            if(IsSeaJourney && LandscapeId==DaytimeSeaMap) SetSeaTime(seed,nextDay);
        }

        public float BeginEncounterApproach()
        {
            if(!approachPending || hero==null) return 0;
            approachPending=false;
            dashElapsed=0;
            nextDashPuff=0;
            return UsesSeaRafts?SeaRaftMotion.Duration:BattleMotion.EncounterDuration;
        }

        public float PlayFightBanner()
        {
            var node=MountPart(effects, "WorldView/PlayFightBanner/Fight Spine");
            var animation=node.GetComponent<ActorAnimationGraphic>();
            if(!animation.LoadEffect("Fight"))
            {
                UnityEngine.Object.Destroy(node.gameObject);
                return 0;
            }
            fightBanner=animation;
            fightElapsed=0;
            return fightBanner.Duration("effect");
        }

        public void ShowBattleBars(bool show=true)
        {
            SetVisible(hero);
            foreach(var enemy in enemies) SetVisible(enemy);

            void SetVisible(Actor actor)
            {
                if(actor?.lifeTrack==null) return;
                actor.lifeTrack.gameObject.SetActive(show);
                actor.life.gameObject.SetActive(show);
                if(actor.rage!=null) actor.rage.transform.parent.gameObject.SetActive(show);
            }
        }

        public void UpdateRage(int value)
        {
            if(hero?.rage!=null) hero.rage.fillAmount=value/100f;
        }

        private float ViewportWidth => root.rect.width>0?root.rect.width:604;

        private float ActorScale => Mathf.Clamp(ViewportWidth/1000f,.55f,1.18f);

        private float BodyClearance(Actor left,Actor right) =>
            (left.width+right.width)*.31f*ActorScale/ViewportWidth+BattleSpacing.BodyGap;

        private float AttackPosition(Actor source,Actor target)
        {
            // The original melee offset is a centre-to-centre distance in scene units.
            float reach=source.meleeOffset*.067f;
            float x=BattleSpacing.StopBefore(source.position.x,target.position.x,target.position.x,
                Mathf.Max(reach,BodyClearance(source,target)));
            return ClearPosition(source,x);
        }

        private float ClearPosition(Actor source,float x)
        {
            float start=source.position.x;
            if(Mathf.Approximately(x,start)) return start;
            x=StopAt(hero,x);
            x=StopAt(pet,x);
            foreach(var enemy in enemies) x=StopAt(enemy,x);
            return x;

            float StopAt(Actor blocker,float position)
            {
                if(blocker==null || blocker==source || !blocker.root.gameObject.activeSelf ||
                    blocker.max>0 && blocker.hp<=0) return position;
                return BattleSpacing.StopBefore(start,position,blocker.position.x,BodyClearance(source,blocker));
            }
        }

        private void AdvanceEncounter(float dt)
        {
            if(dashElapsed<0 || dt<=0) return;
            if(UsesSeaRafts)
            {
                AdvanceSeaEncounter(dt);
                return;
            }
            dashElapsed+=dt;
            float progress=BattleMotion.EncounterCameraProgress(dashElapsed);
            travel+=(progress-encounterProgress)*BattleMotion.EncounterTravel;
            encounterProgress=progress;
            while(nextDashPuff<DashPuffTimes.Length && DashPuffTimes[nextDashPuff]<=dashElapsed)
            {
                EmitDashDust(DashPuffTimes[nextDashPuff],nextDashPuff==DashPuffTimes.Length-1);
                nextDashPuff++;
            }
            if(dashElapsed>=BattleMotion.EncounterDuration) dashElapsed=-1;
        }

        private void DrawDashRibbons()
        {
            bool visible=dashElapsed>=0 && !UsesSeaRafts;
            foreach(var ribbon in encounterRibbons) ribbon.enabled=visible;
            if(!visible) return;
            if(encounterRibbonBrush==null) encounterRibbonBrush=MakeDashBrush(true);
            float x=BattleMotion.EncounterHeroScreenX(dashElapsed);
            float fade=Mathf.Clamp01(dashElapsed/.065f)*(1-Mathf.InverseLerp(.38f,.58f,dashElapsed));
            for(int i=0;i<encounterRibbons.Length;i++)
            {
                var ribbon=encounterRibbons[i];
                ribbon.texture=encounterRibbonBrush;
                var point=new Vector2(x-.112f-i*.032f,hero.position.y+.018f+i*.014f);
                ribbon.rectTransform.anchorMin=ribbon.rectTransform.anchorMax=point;
                ribbon.rectTransform.sizeDelta=new Vector2(ViewportWidth*(.095f+fade*.12f-i*.029f),i==0?11:5);
                ribbon.rectTransform.localRotation=Quaternion.Euler(0,0,i==0?4:-3);
                ribbon.color=i==0?new Color(.92f,.98f,1,.92f*fade):new Color(.66f,.91f,.92f,.78f*fade);
            }
        }

        private void EmitDashDust(float elapsed,bool braking)
        {
            if(encounterDustBrush==null) encounterDustBrush=MakeDashBrush(false);
            Color tint=new Color(.99f,.92f,.75f,.83f);
            float x=BattleMotion.EncounterHeroWorldX(elapsed);
            for(int i=0;i<2;i++)
            {
                var point=new Vector2(x-.041f-i*.037f,hero.position.y+.005f+i*.006f);
                var image=ImageAt(effects,encounterDustBrush,(point.x,point.y,point.x,point.y));
                image.name=braking?"Encounter braking dust":"Encounter step dust";
                image.color=tint;
                image.rectTransform.sizeDelta=new Vector2(braking?34:25,i==0?10:8);
                particles.Add(new Particle{rect=image.rectTransform,graphic=image,point=point,color=tint,
                    life=braking?.23f:.29f,grow=.85f,velocity=new Vector2(-.13f-i*.058f,.023f+i*.011f),encounterDust=true});
            }
        }

        private static Texture2D MakeDashBrush(bool ribbon)
        {
            int width=ribbon?64:32, height=16;
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            for(int x=0;x<width;x++)
            {
                float u=x/(float)(width-1);
                float v=y/(float)(height-1)*2-1;
                float alpha=ribbon
                    ? Mathf.SmoothStep(0,1,u)*Mathf.Clamp01((1-u)*11)*
                        Mathf.Clamp01(1-Mathf.Abs(v)/Mathf.Lerp(.08f,.82f,u))
                    : Mathf.Clamp01(1-(u*2-1)*(u*2-1)-v*v);
                pixels[y*width+x]=new Color32(255,255,255,(byte)(255*alpha));
            }
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            return texture;
        }

        private void AddStreak(Vector2 center,Vector2 size,float angle,Color tint,float life,Vector2 velocity)
        {
            var rect=Panel(effects,tint,(center.x,center.y,center.x,center.y));
            rect.sizeDelta=size;
            rect.localRotation=Quaternion.Euler(0,0,angle);
            particles.Add(new Particle{rect=rect,graphic=rect.GetComponent<Image>(),point=center,velocity=velocity,
                color=tint,life=life});
        }

        private Actor Combatant(int slot)
        {
            if(slot==CombatAction.Hero || slot==CombatAction.Artifact) return hero;
            if(slot==CombatAction.Pet) return pet;
            return slot>=0 && slot<enemies.Count?enemies[slot]:null;
        }

        private void BringAttackerForward(Actor source,Actor target)
        {
            if(source==null || target==null || source==target || !source.root.gameObject.activeSelf) return;
            int index=source.root.GetSiblingIndex();
            if(index==actorLayer.childCount-1) return;
            attackerNextSibling=actorLayer.GetChild(index+1);
            foregroundAttacker=source.root;
            foregroundAttacker.SetAsLastSibling();
        }

        public void EndAction()
        {
            if(foregroundAttacker!=null && attackerNextSibling!=null)
                foregroundAttacker.SetSiblingIndex(attackerNextSibling.GetSiblingIndex());
            foregroundAttacker=attackerNextSibling=null;
        }

        public float BeginAction(CombatAction action,string skillName=null)
        {
            if(action.Kind==CombatActionKind.SkillStrike)
            {
                // The ordinary icicle has no caster animation, caster effect, or skill HUD.
                if(action.Effect==CombatEffect.Ice) return 0;
                var caster=Combatant(action.Source);
                BringAttackerForward(caster,Combatant(action.Target));
                if(action.Effect==CombatEffect.Knife && caster!=null)
                {
                    ShowKnifePortal(caster);
                    return .09f;
                }
                bool rage=action.SkillId==0 && action.Effect==CombatEffect.SwordWave;
                if(rage && hero!=null)
                {
                    hero.attackPose="cast";
                    hero.attackDuration=hero.animation?.Duration("cast")??.38f;
                    hero.motion=0;
                    hero.attackStart=0;
                    hero.motionDuration=hero.attackDuration;
                    hero.reach=Vector2.zero;
                    UpdateRage(0);
                }
                if(hero!=null && (rage || skillName!=null) && time>=showSkillNameAfter)
                {
                    Pop(hero.position+new Vector2(0,.18f),rage?"怒气斩":skillName,Tint(0xFFEC90));
                    showSkillNameAfter=time+.26f;
                }
                return rage && hero!=null?Mathf.Min(.23f,hero.attackDuration*.4f):.09f;
            }
            switch(action.Kind)
            {
                case CombatActionKind.Attack:
                case CombatActionKind.Combo:
                case CombatActionKind.Counter:
                case CombatActionKind.PetStrike:
                case CombatActionKind.ArtifactStrike:
                case CombatActionKind.EnemyAttack:
                case CombatActionKind.Dodge:
                    break;
                default:
                    return 0;
            }
            var source=Combatant(action.Source);
            var target=Combatant(action.Target);
            if(source==null || target==null) return 0;
            BringAttackerForward(source,target);
            source.attackPose="attack";
            source.attackDuration=source.animation?.Duration("attack")??StrikeMotion;
            source.motion=0;
            source.hit=-1;
            source.attackStart=0;
            source.reach=Vector2.zero;
            source.motionDuration=source.attackDuration;
            if(source.ranged || action.Kind==CombatActionKind.PetStrike)
            {
                float release=source.animation?.Cue("attack")??-1;
                return release>=0?release:source.attackDuration*.42f;
            }
            if(action.Kind==CombatActionKind.ArtifactStrike)
            {
                source.motionDuration=StrikeMotion;
                return 0;
            }

            source.reach=new Vector2(AttackPosition(source,target)-source.position.x,target.position.y-source.position.y);
            source.attackStart=BattleMotion.MeleeAdvance;
            source.returnAt=source.attackStart+source.attackDuration;
            source.motionDuration=source.returnAt+BattleMotion.MeleeReturn;
            return source.attackStart+(source==hero?BattleMotion.SwordImpact:source.attackDuration*.42f);
        }

        public float LaunchActionProjectile(CombatAction action)
        {
            if(action.Kind==CombatActionKind.SkillStrike)
                return LaunchSkillProjectile(action);
            if(action.Kind!=CombatActionKind.EnemyAttack && action.Kind!=CombatActionKind.Dodge &&
                action.Kind!=CombatActionKind.ArtifactStrike && action.Kind!=CombatActionKind.PetStrike) return 0;
            var source=Combatant(action.Source);
            var target=Combatant(action.Target);
            if(source==null || target==null || (!source.ranged && action.Kind!=CombatActionKind.ArtifactStrike &&
                action.Kind!=CombatActionKind.PetStrike)) return 0;
            FireProjectile(source,target);
            return ProjectileTravel;
        }

        public float ActionRecovery(CombatAction action,float beforeImpact)
        {
            if(action.Kind==CombatActionKind.SkillStatus) return .07f;
            if(action.Kind==CombatActionKind.SkillStrike && action.SkillId>0) return .16f;
            if(beforeImpact<=0) return .17f;
            var source=Combatant(action.Source);
            return Mathf.Max(.21f,(source?.motionDuration??0)-beforeImpact);
        }

        public float BattleExitRecovery
        {
            get
            {
                float remaining=Mathf.Max(RemainingDeath(hero),icicleEndsAt-time);
                foreach(var enemy in enemies) remaining=Mathf.Max(remaining,RemainingDeath(enemy));
                return remaining;
            }
        }

        private static float RemainingDeath(Actor actor) =>
            actor?.animation!=null && actor.hp==0 && actor.max>0 && actor.hit>=0
                ? Mathf.Max(0,actor.animation.Duration("down")-actor.hit)
                : 0;

        public void ShowAction(CombatAction action)
        {
            var target=Combatant(action.Target);
            if(target==null) return;

            target.hp=action.TargetHp;
            UpdateBar(target);
            Vector2 point=target.position+new Vector2(0,.142f);
            switch(action.Kind)
            {
                case CombatActionKind.SkillStatus:
                    if(action.Effect!=CombatEffect.Ice) ShowSkillEffect(action.Effect,target.position);
                    if(action.Effect==CombatEffect.Shield)
                        Pop(point,"护盾 +"+action.Amount,Tint(0xC1ECFF));
                    else if(action.Effect==CombatEffect.Ice) Pop(point,"冰冻",Tint(0xC1ECFF));
                    return;
                case CombatActionKind.Dodge:
                    target.dodge=0;
                    Pop(point,"闪避",Tint(0xC1ECFF));
                    return;
                case CombatActionKind.Heal:
                    if(action.Amount>0)
                    {
                        ShowSkillEffect(CombatEffect.Heal,target.position);
                        Pop(point,"+"+action.Amount,Tint(0x9BED9D));
                    }
                    return;
                case CombatActionKind.Revive:
                    target.hit=target.motion=-1;
                    Pop(point,"复活  +"+action.Amount,Tint(0x9BED9D));
                    Burst(target.position+new Vector2(0,.095f));
                    return;
            }

            if(action.Absorbed>0)
            {
                ShowSkillEffect(CombatEffect.Shield,target.position);
                Pop(point+new Vector2(action.Amount>0?-.07f:0,.042f),"护盾 "+action.Absorbed,Tint(0xC1ECFF));
            }
            if(action.Amount<=0 && action.Absorbed<=0) return;
            target.hit=0;
            if(action.Amount>0)
            {
                string label=action.Critical?"暴击":action.Kind==CombatActionKind.Combo?"连击":
                    action.Kind==CombatActionKind.Counter?"反击":action.Kind==CombatActionKind.PetStrike?"助战":
                    action.Kind==CombatActionKind.ArtifactStrike?"神器":action.Kind==CombatActionKind.Poison?"中毒":
                    action.Kind==CombatActionKind.Burn?"灼烧":action.Kind==CombatActionKind.Bleed?"流血":"";
                var color=action.Kind==CombatActionKind.Poison?Tint(0xAFE081):
                    action.Kind==CombatActionKind.Burn?Tint(0xFFAB62):
                    action.Kind==CombatActionKind.Bleed?Tint(0xFFAF97):
                    action.Critical?Tint(0xFFC361):Color.white;
                Pop(point,(label.Length>0?label+"  ":"")+"-"+action.Amount,color);
            }
            var effect=action.Kind==CombatActionKind.Poison?CombatEffect.Poison:
                action.Kind==CombatActionKind.Burn?CombatEffect.Fire:action.Kind==CombatActionKind.Bleed?CombatEffect.Bleed:action.Effect;
            if(effect==CombatEffect.None && action.Source==CombatAction.Hero &&
                (action.Kind==CombatActionKind.Attack || action.Kind==CombatActionKind.Combo || action.Kind==CombatActionKind.Counter))
                AddEffect("Slash",target.position+new Vector2(0,.08f),new Vector2(110,105),.26f,Color.white,Vector2.zero,2,2);
            else if(effect==CombatEffect.None) Burst(target.position+new Vector2(0,.095f));
            else if(effect==CombatEffect.Ice) ShowIcicleImpact(target);
            else ShowSkillEffect(effect,target.position);
        }

        private void FireProjectile(Actor source,Actor target)
        {
            var from=source.position+new Vector2(0,.082f);
            var to=target.position+new Vector2(0,.048f);
            var texture=source.caster?Texture("MageProjectile"):null;
            var image=ImageAt(effects,texture??Texture("Spark"),(from.x,from.y,from.x,from.y));
            if(texture!=null)
            {
                // Excise the upper-right frame of the original texture sheet in UV space.
                image.uvRect=new Rect(.58f,.515f,.42f,.47f);
                image.rectTransform.sizeDelta=new Vector2(92,49);
                image.rectTransform.localRotation=Quaternion.Euler(0,0,to.x<from.x?180:0);
            }
            else image.rectTransform.sizeDelta=new Vector2(42,42);
            particles.Add(new Particle{rect=image.rectTransform,point=from,velocity=(to-from)/ProjectileTravel,
                life=ProjectileTravel,graphic=image,color=Color.white});
        }

        private float LaunchSkillProjectile(CombatAction action)
        {
            var target=Combatant(action.Target);
            if(target==null) return 0;
            if(action.Effect==CombatEffect.Ice)
            {
                ShowIcicle(target);
                return BattleMotion.IcicleImpact;
            }
            var source=Combatant(action.Source);
            if(source==null) return 0;
            if(action.Effect==CombatEffect.Knife)
            {
                var launch=PointInEffects(source.root,KnifeCastOffset*ChapterWorldScale(root.rect.size));
                var center=PointInEffects(target.root,new Vector2(0,target.root.rect.height*.45f));
                var direction=Vector2.Scale(center-launch,effects.rect.size);
                float heading=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                AddEffect("Knife",launch,new Vector2(47,47),BattleMotion.KnifeFlight,Color.white,
                    (center-launch)/BattleMotion.KnifeFlight,rotation:heading-90);
                return BattleMotion.KnifeFlight;
            }
            var from=source.position+new Vector2(.031f,.065f);
            var to=target.position+new Vector2(0,.065f);
            float angle=Mathf.Atan2((to.y-from.y)*root.rect.height,(to.x-from.x)*root.rect.width)*Mathf.Rad2Deg;
            var velocity=(to-from)/ProjectileTravel;
            switch(action.Effect)
            {
                case CombatEffect.SwordWave:
                    AddEffect("SwordWave",from,new Vector2(105,105),ProjectileTravel,Color.white,velocity,rotation:angle);
                    break;
                case CombatEffect.Fire:
                    AddEffect("Fire",from,new Vector2(83,75),ProjectileTravel,Color.white,velocity,4,4,frameCount:6);
                    break;
                case CombatEffect.Spear:
                    AddEffect("Spear",to+new Vector2(0,.19f),new Vector2(36,117),ProjectileTravel,Color.white,
                        new Vector2(0,-.19f/ProjectileTravel));
                    break;
                case CombatEffect.Explosion:
                    AddEffect("Rocket",from,new Vector2(76,52),ProjectileTravel,Color.white,velocity,rotation:angle);
                    break;
                default:
                    return 0;
            }
            return ProjectileTravel;
        }

        private Vector2 PointInEffects(RectTransform owner,Vector2 offset)
        {
            var local=effects.InverseTransformPoint(owner.TransformPoint(offset));
            var bounds=effects.rect;
            return new Vector2((local.x-bounds.xMin)/bounds.width,(local.y-bounds.yMin)/bounds.height);
        }

        private void ShowKnifePortal(Actor caster)
        {
            var texture=Texture("KnifePortal");
            if(texture==null) return;

            // Base skill 3 uses the black-hole effect at body point 7 (HeadTop).
            float scale=ChapterWorldScale(root.rect.size);
            var foot=new Vector2(.5f,0);
            var image=ImageAt(caster.root,texture,(foot.x,foot.y,foot.x,foot.y));
            var rect=image.rectTransform;
            rect.name="Knife portal";
            rect.anchoredPosition=KnifeCastOffset*scale;
            rect.sizeDelta=new Vector2(2f,1.25f)*scale;
            particles.Add(new Particle{rect=rect,point=foot,life=.45f,graphic=image,color=Color.white,grow=.14f});
        }

        private void ShowIcicle(Actor target)
        {
            var texture=Texture("CombatEffects/Ice");
            if(texture==null) return;

            // Bullet 1201 follows body point 0 (Foot). The prefab's flipbook grows up from that pivot.
            var foot=new Vector2(.5f,0);
            var image=ImageAt(target.root,texture,(foot.x,foot.y,foot.x,foot.y));
            var rect=image.rectTransform;
            rect.name="Icicle";
            rect.pivot=new Vector2(.5f,.25f);
            rect.sizeDelta=new Vector2(3f,4.5f)*ChapterWorldScale(root.rect.size);
            rect.SetSiblingIndex(target.art.transform.GetSiblingIndex()+1);
            image.color=Color.white;
            var particle=new Particle{rect=rect,sheet=image,graphic=image,point=foot,life=.63f,
                color=Color.white,columns=4,rows=4,frames=16,fade=false,frameOverTime=IcicleFrameCurve};
            SetEffectFrame(particle,0);
            particles.Add(particle);
            icicleEndsAt=time+particle.life;
        }

        private void ShowIcicleImpact(Actor target)
        {
            var texture=Texture("Spark");
            if(texture==null) return;

            // Fire bullet 2 places hit effect 101 at body point 6 (Health).
            var parent=target.lifeTrack??target.root;
            var point=new Vector2(.5f,target.lifeTrack!=null?.5f:.8f);
            var image=ImageAt(parent,texture,(point.x,point.y,point.x,point.y));
            image.name="Icicle impact";
            image.rectTransform.sizeDelta=Vector2.one*(1.5f*ChapterWorldScale(root.rect.size));
            image.color=Tint(0xFFE6A1);
            particles.Add(new Particle{rect=image.rectTransform,point=point,life=.2f,graphic=image,
                color=image.color,grow=.18f});
        }

        private void ShowSkillEffect(CombatEffect effect,Vector2 target)
        {
            var center=target+new Vector2(0,.06f);
            switch(effect)
            {
                case CombatEffect.Lightning:
                    AddLightning(center);
                    AddEffect("Lightning",center,new Vector2(124,99),.36f,Color.white,Vector2.zero,4,2,grow:.23f);
                    break;
                case CombatEffect.Fire:
                case CombatEffect.Explosion:
                    AddEffect("Fire",center,new Vector2(133,133),.48f,Color.white,Vector2.zero,4,4,grow:.25f);
                    break;
                case CombatEffect.Poison:
                    // The poison sheet contains nine frames arranged in a 3 x 3 grid.
                    AddEffect("Poison",center,new Vector2(103,101),.46f,new Color(.73f,.38f,1,.95f),
                        new Vector2(0,.08f),3,3,grow:.4f);
                    break;
                case CombatEffect.Heal:
                    AddEffect("Heal",center,new Vector2(137,136),.56f,Color.white,Vector2.zero,4,4);
                    break;
                case CombatEffect.Shield:
                    AddEffect("Shield",center,new Vector2(150,148),.43f,Color.white,Vector2.zero,grow:.2f);
                    break;
                default:
                    var tint=effect==CombatEffect.Bleed?Tint(0xF05454):effect==CombatEffect.Rage?Tint(0xFFD76A):Color.white;
                    AddEffect("Slash",center,new Vector2(102,101),.28f,tint,Vector2.zero,2,2,grow:.24f);
                    break;
            }
        }

        private void AddLightning(Vector2 end)
        {
            var start=end+new Vector2(.065f,.34f);
            var middle=end+new Vector2(-.031f,.15f);
            var bend=end+new Vector2(.036f,.098f);
            Bolt(start,middle);
            Bolt(middle,bend);
            Bolt(bend,end);

            void Bolt(Vector2 a,Vector2 b)
            {
                var delta=Vector2.Scale(b-a,root.rect.size);
                float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
                AddStreak((a+b)*.5f,new Vector2(delta.magnitude+3,9),angle,new Color(1,.75f,.19f,.74f),.12f,Vector2.zero);
                AddStreak((a+b)*.5f,new Vector2(delta.magnitude+2,3),angle,Tint(0xFFF2B8),.13f,Vector2.zero);
            }
        }

        private void AddEffect(string key,Vector2 point,Vector2 dimensions,float life,Color tint,Vector2 velocity,
            int columns=1,int rows=1,float rotation=0,float spin=0,float grow=0,int frameCount=0)
        {
            var texture=Texture("CombatEffects/"+key);
            if(texture==null) return;
            var image=ImageAt(effects,texture,(point.x,point.y,point.x,point.y));
            image.rectTransform.sizeDelta=dimensions;
            image.color=tint;
            image.rectTransform.localRotation=Quaternion.Euler(0,0,rotation);
            var particle=new Particle{rect=image.rectTransform,sheet=image,graphic=image,point=point,velocity=velocity,
                life=life,color=tint,columns=columns,rows=rows,rotation=rotation,spin=spin,grow=grow,
                frames=frameCount>0?frameCount:columns*rows};
            SetEffectFrame(particle,0);
            particles.Add(particle);
        }

        private static void SetEffectFrame(Particle particle,float progress)
        {
            if(particle.frameOverTime!=null)
                progress=.9999f*particle.frameOverTime.Evaluate(progress);
            int frame=Mathf.Clamp((int)(particle.frames*progress),0,particle.frames-1);
            float cellW=1f/particle.columns,cellH=1f/particle.rows;
            particle.sheet.uvRect=new Rect((frame%particle.columns)*cellW,1-(frame/particle.columns+1)*cellH,cellW,cellH);
        }

        private static void UpdateBar(Actor actor)
        {
            if(actor.bar==null) return;
            actor.bar.fillAmount=actor.hp/(float)Math.Max(1,actor.max);
            actor.life.text=actor.hp.ToString();
        }

        private void Pop(Vector2 position,string value,Color color)
        {
            var rect=InstantiateUi("Widgets/CombatNumber",effects);
            rect.anchorMin=rect.anchorMax=position;
            var text=rect.GetComponent<Text>();
            text.text=value;
            text.color=color;
            particles.Add(new Particle{rect=rect,point=position,velocity=new Vector2(0,.15f),life=.72f,graphic=text,color=color});
        }

        private void Burst(Vector2 point)
        {
            for(int i=0;i<7;i++)
            {
                float angle=i*2*Mathf.PI/7;
                var image=ImageAt(effects,Texture("Spark"),(point.x,point.y,point.x,point.y));
                image.rectTransform.sizeDelta=Vector2.one*(i==0?46:24);
                var velocity=new Vector2(Mathf.Cos(angle)*.15f,Mathf.Sin(angle)*.21f);
                particles.Add(new Particle{rect=image.rectTransform,point=point,velocity=velocity,life=.27f,graphic=image,color=Color.white});
            }
        }

        public void Animate(float dt,Phase phase,bool paused,bool battlePrompt)
        {
            LayoutJourneyProp();
            time+=dt;
            bool travelling=journeyMap && BattleMotion.CanTravel(phase,paused,battlePrompt || approachPending);
            if(travelling) travel+=dt*BattleMotion.JourneySpeed(UsesSeaRafts)*ChapterWorldScale(root.rect.size)/ViewportWidth;
            if(campView) travel+=dt*.14f;
            if(walk>=0) walk+=dt;
            AdvanceEncounter(dt);
            if(fightBanner!=null)
            {
                fightElapsed+=dt;
                fightBanner.SetPose("effect",fightElapsed,false);
                if(fightElapsed>=fightBanner.Duration("effect"))
                {
                    fightBanner.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(fightBanner.gameObject);
                    fightBanner=null;
                }
            }
            float height=root.rect.height;
            float width=root.rect.width;
            if(offlineRoad.enabled)
            {
                float horizon=campView?.57f:.765f;
                offlineRoad.rectTransform.anchorMax=new Vector2(1,horizon);
                offlineDistant.rectTransform.anchorMin=new Vector2(0,horizon);
                offlineRoadside.rectTransform.anchorMin=new Vector2(0,horizon);
                offlineRoadside.rectTransform.anchorMax=new Vector2(1,horizon+.19f);
                offlineDistant.uvRect=new Rect(Mathf.Repeat(travel*.27f,1),0,1,1);
                offlineRoadside.uvRect=new Rect(Mathf.Repeat(travel,1),0,1,1);
            }
            else if(arenaId==0) AnimateMapLayers(height,width);
            if(UsesSeaRafts) AnimateSeaRafts(dt,travelling);
            else
            {
                float heroOffset=approachPending?BattleMotion.HeroTrail-BattleMotion.HeroHome:
                    IsEncounterApproaching?BattleMotion.EncounterHeroScreenX(dashElapsed)-BattleMotion.HeroHome:0;
                float cameraLag=1-encounterProgress;
                Pose(hero,heroOffset,dt,walking:travelling || campView);
                Pose(pet,0,dt);
                foreach(var enemy in enemies) Pose(enemy,cameraLag*BattleMotion.EncounterTravel,dt);
            }
            Pose(prop,0,dt);
            DrawDashRibbons();
            AnimateParticles(dt);
        }

        private void AnimateMapLayers(float height,float width)
        {
            if(landScenery!=null)
            {
                LayoutLandScenery(height,width);
                return;
            }

            bool sea=LandscapeId==DaytimeSeaMap;
            sky.rectTransform.anchorMin=new Vector2(0,campView?.50f:.56f);
            sky.rectTransform.anchorMax=Vector2.one;
            LayoutCloudTiles(width,sea,(sea?.04f:campView?.009f:.035f)*height);
            bool town=!campView && (map==null || LandscapeId==WindyTownMap);
            bool woodland=LandscapeId==WhisperWoodlandMap;
            bool journeyRoad=town || (!campView && woodland);
            float position=travel*width;
            ScrollLayer(far,map?.far,position*Mathf.Abs(map?.farSpeed??0)*.5f,height*(sea?.89f:campView?.84f:1),width);
            // Scale both land routes against the town reference so the road stays under the actors.
            float nearWidth=journeyRoad?height*.69f*1080f/1250f:width;
            float foregroundTop=height*(sea?.881f:campView?1:journeyRoad?1.179f:1.16f);
            float nearOffset=town?width*.56f:0;
            if(campView && (LandscapeId==WindyTownMap || woodland))
            {
                nearWidth=width*1.065f;
                bool windyTown=LandscapeId==WindyTownMap;
                foregroundTop=height*.638f+nearWidth*(windyTown?TownRoadTop:WoodlandRoadTop)/1080f;
                nearOffset=windyTown?nearWidth*1.20f:0;
            }
            else if(woodland)
                foregroundTop+=nearWidth*(WoodlandRoadTop-TownRoadTop)/1080f;
            var middleIds=map?.middle is { Length:>0 }?map.middle:TownMiddle;
            // The 210 px sea texture tiles horizontally. Anchor its height to the
            // vertical reference so it cannot cover the sky or distant clouds.
            float waterHeight=sea?height*1084f/1920f:0;
            ScrollLayer(middle,middleIds,position+nearOffset,foregroundTop,nearWidth,waterHeight);
            var groundIds=map?.ground ?? (town?TownGround:null);
            ScrollLayer(ground,groundIds,position,height*(campView?.635f:.787f),nearWidth*906f/1080f);
            if(campView && height>0 && middle[0].enabled)
                sky.rectTransform.anchorMin=new Vector2(0,Mathf.Clamp01((foregroundTop-middle[0].rectTransform.sizeDelta.y)/height));
        }

        private void LayoutCloudTiles(float width,bool sea,float yOffset,float heightAbovePivot=0)
        {
            if(!clouds[0].enabled || width<=0) return;

            float drift=(time*Mathf.Abs(map.cloudSpeed)*.10f+travel*.12f)*width;
            for(int i=0;i<clouds.Length;i++)
            {
                var tile=clouds[i];
                float cloudWidth=width*tile.texture.width/1080f;
                float cloudHeight=width*tile.texture.height/1080f;
                float x=sea
                    ?Mathf.Repeat(width*(i==0?-.29f:.64f)+cloudWidth-drift,width+cloudWidth)-cloudWidth
                    :Mathf.Repeat(width*(i==0?.45f:1.43f)+cloudWidth-drift,2*width)-cloudWidth;
                tile.rectTransform.sizeDelta=new Vector2(cloudWidth,cloudHeight);
                tile.rectTransform.anchoredPosition=new Vector2(x,yOffset+cloudHeight*heightAbovePivot);
            }
        }

        private void AnimateParticles(float dt)
        {
            for(int i=particles.Count-1;i>=0;i--)
            {
                var p=particles[i];
                p.age+=dt;
                if(p.age>=p.life)
                {
                    UnityEngine.Object.Destroy(p.rect.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }
                var center=p.point+p.velocity*p.age;
                if(p.encounterDust) center.x-=BattleMotion.EncounterTravel*encounterProgress;
                p.rect.anchorMin=p.rect.anchorMax=center;
                float progress=p.age/p.life;
                if(p.fade)
                    p.graphic.color=new Color(p.color.r,p.color.g,p.color.b,p.color.a*(1-Mathf.Clamp01((progress-.34f)/.66f)));
                if(p.sheet!=null)
                {
                    SetEffectFrame(p,progress);
                    p.rect.localRotation=Quaternion.Euler(0,0,p.rotation+p.spin*p.age);
                }
                if(p.grow!=0) p.rect.localScale=Vector3.one*(1+p.grow*progress);
            }
        }

        private void Pose(Actor actor,float cameraOffset,float dt,float deckLift=0,float deckTilt=0,bool walking=false)
        {
            if(actor==null || !actor.root.gameObject.activeSelf) return;
            if(actor.hit>=0) actor.hit+=dt;
            if(actor.motion>=0)
            {
                actor.motion+=dt;
                if(actor.motion>=actor.motionDuration)
                {
                    actor.motion=-1;
                    actor.reach=Vector2.zero;
                }
            }
            if(actor.dodge>=0) actor.dodge+=dt;
            float impulse=actor.hit>=0 && actor.hit<.24f?Mathf.Sin(actor.hit/.24f*Mathf.PI):0;
            float lunge=actor.reach.sqrMagnitude>0
                ?BattleMotion.MeleeFraction(actor.motion,actor.attackStart,actor.returnAt,actor.motionDuration):0;
            float sidestep=actor.dodge>=0 && actor.dodge<.20f?Mathf.Sin(actor.dodge/.20f*Mathf.PI)*-29:0;
            var reach=new Vector2(actor.reach.x*root.rect.width,actor.reach.y*root.rect.height)*lunge;
            float scale=ActorScale;
            actor.root.sizeDelta=new Vector2(actor.width,actor.height)*scale;
            float recoil=actor==hero?-impulse*11:impulse*11;
            float shift=recoil+sidestep+reach.x;
            if(enemies.Count>0)
                shift=(ClearPosition(actor,actor.position.x+shift/ViewportWidth)-actor.position.x)*ViewportWidth;
            actor.root.anchoredPosition=new Vector2(shift+cameraOffset*ViewportWidth,reach.y+deckLift);
            actor.root.localRotation=Quaternion.Euler(0,0,deckTilt);
            bool dead=actor.max>0 && actor.hp==0;
            if(actor.animation!=null)
            {
                if(dead) actor.animation.SetPose("down",Mathf.Max(0,actor.hit),false);
                else if(actor.hit>=0 && actor.hit<actor.animation.Duration("hurt"))
                    actor.animation.SetPose("hurt",actor.hit,false);
                else if(dashElapsed>=0 && actor==hero && UsesSeaRafts && dashElapsed<SeaRaftMotion.ContactTime && actor.animation.HasClip("sailApproach"))
                    actor.animation.SetPose("sailApproach",dashElapsed,true);
                else if(dashElapsed>=0 && actor==hero && !UsesSeaRafts)
                {
                    if(actor.animation.HasClip("dash")) actor.animation.SetPose("dash",dashElapsed,false);
                    else actor.animation.SetPose("walk",dashElapsed*2,true);
                }
                else if(actor.motion>=actor.attackStart && actor.motion<actor.attackStart+actor.attackDuration)
                    actor.animation.SetPose(actor.attackPose,actor.motion-actor.attackStart,false);
                else if(walking) actor.animation.SetPose("walk",time,true);
                else actor.animation.SetPose("idle",time,true);
                actor.art.color=actor.baseColor;
            }
            else
            {
                actor.image.localScale=new Vector3(1+impulse*.07f,1-impulse*.11f,1);
                actor.art.color=new Color(actor.baseColor.r,actor.baseColor.g*(1-impulse*.38f),actor.baseColor.b*(1-impulse*.43f),
                    dead?1-Mathf.Clamp01(Mathf.Max(0,actor.hit)/.38f):1);
            }
        }
    }
}
