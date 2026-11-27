using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen : MonoBehaviour
    {
        private Adventure game;
        [SerializeField] private Canvas canvas=null;
        [SerializeField] private CanvasScaler scaler=null;
        [SerializeField] private RectTransform portrait=null;
        [SerializeField] private AudioSource audioSource=null;
        private RectTransform overlay;
        public const int OutputWidth=1080, OutputHeight=1920;
        public const int ReferenceWidth=604, ReferenceHeight=1080;
        private const float PortraitAspect=(float)OutputWidth/OutputHeight;
        private int lastWidth, lastHeight, speed=1;
        private float lastCanvasScale;
        private Rect lastSafeArea;
        private bool campPicker;
        private Font font, accountFont;
        private WorldView world;
        private AudioClip steps,impact,victory;
        private bool busy, paused, playingBattle;
        private Text roundCaption, speedCaption, damageCaption, healthCaption, healthPercent;
        private Text experienceCaption, attackCaption, defenceCaption;
        private Text battleDots;
        private float battleDotsTime;
        private RectTransform battleHeader;
        private Image healthFill;
        private Image experienceFill;
        private int displayedMaxHp, displayedHp, previousHp, targetHp;
        private float healthChangeElapsed;
        private const float HealthChangeDuration=.45f;
        private float BattleTimeStep => paused?0:Time.unscaledDeltaTime*speed;
        private bool campChallenges;
        private int challengeTab=1;
        private float scrollPosition=1;
        private ScrollRect currentScroll;
        private ScrollRect journalScroll;
        private string error;

        private static readonly Color Ink=Tint(0x273736);
        private static readonly Color Paper=Tint(0xFFF3D9);
        private static readonly Color Gold=Tint(0xF8CA75);
        private static readonly Color White=Tint(0xFFF8E8);

        private void Awake()
        {
            accountFont=Resources.Load<Font>("Reference/AdventureFont");
            font=Resources.Load<Font>("Reference/Magic")??accountFont;
            if(font==null)
            {
                var family=new[]{"Microsoft YaHei","Microsoft JhengHei","PingFang SC","Noto Sans CJK SC","SimHei"}
                    .FirstOrDefault(Font.GetOSInstalledFontNames().Contains);
                font=family!=null?Font.CreateDynamicFontFromOSFont(family,28):Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            if(canvas==null || scaler==null || portrait==null || audioSource==null)
            {
                Debug.LogError("Adventure 场景缺少 Canvas、CanvasScaler、Portrait 或 AudioSource 的引用。",this);
                enabled=false;
                return;
            }
            Screen.orientation=ScreenOrientation.Portrait;
            LayoutPortrait();
            world=new WorldView(portrait,font);
            steps=Resources.Load<AudioClip>("Audio/Steps");
            impact=Resources.Load<AudioClip>("Audio/Impact");
            victory=Resources.Load<AudioClip>("Audio/Victory");
        }

        private void Start()
        {
            if (game != null) return;
            error = "请从 GameNetty 登录后进入旅程。";
            Refresh();
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UpdateGmMenu();
#endif
            if(lastWidth!=Screen.width || lastHeight!=Screen.height || lastSafeArea!=Screen.safeArea ||
                !Mathf.Approximately(lastCanvasScale,canvas.scaleFactor)) LayoutPortrait();
            var phase=playingBattle?Phase.Battle:game?.Phase??Phase.Camp;
            bool inBattle=phase==Phase.Battle;
            world?.Animate(inBattle?BattleTimeStep:Time.unscaledDeltaTime,phase,paused,game?.IsBattlePrompt==true);
            if(healthCaption!=null && healthChangeElapsed<HealthChangeDuration)
            {
                healthChangeElapsed=Mathf.Min(HealthChangeDuration,healthChangeElapsed+BattleTimeStep);
                int health=Mathf.RoundToInt(Mathf.Lerp(previousHp,targetHp,healthChangeElapsed/HealthChangeDuration));
                if(health!=displayedHp) UpdateJourneyHealth(health);
            }
            if(battleDots!=null)
            {
                battleDotsTime+=inBattle?BattleTimeStep:Time.unscaledDeltaTime;
                int count=1+(int)(battleDotsTime*5)%3;
                string dots=count==1?"▪":count==2?"▪ ▪":"▪ ▪ ▪";
                if(battleDots.text!=dots) battleDots.text=dots;
            }
            if(heroPreview!=null) heroPreview.SetPose("idle",Time.unscaledTime,true);
            if(!busy && !paused && !syncRequired && game?.Phase==Phase.Battle
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                && gmMenu==null
#endif
            ) StartCoroutine(PlayRound());
        }

        private void Refresh(bool preserveScroll=false)
        {
            if(currentScroll!=null && preserveScroll) scrollPosition=currentScroll.verticalNormalizedPosition;
            else scrollPosition=1;
            currentScroll=null;
            SaveJournalScroll();
            journalScroll=null;
            journalAction=null;
            journalTravelFill=null;
            heroPreview=null;
            campMenu=null;
            roundCaption=speedCaption=damageCaption=healthCaption=healthPercent=null;
            experienceCaption=attackCaption=defenceCaption=null;
            battleDots=null;
            battleDotsTime=0;
            battleHeader=null;
            healthFill=experienceFill=null;
            if(overlay!=null)
            {
                overlay.gameObject.SetActive(false);
                Destroy(overlay.gameObject);
            }
            overlay=LoadView(game==null || game.Phase==Phase.Camp?"Camp":"Journey",portrait);
            UpdateDamageStatisticsLayer();
            if(game==null)
            {
                var caption=InstantiateUi("Widgets/LoadError",overlay).GetComponent<Text>();
                if(error!=null) caption.text=error;
                AddSessionButton(overlay,"前往登录",LeaveAdventure,(.32f,.28f,.68f,.33f));
                return;
            }
            if(game.Phase==Phase.Camp) DrawCamp();
            else DrawJourney();
            RefreshJourneyCoins();
            UpdateSaveError();
        }

        private void UpdateSaveError()
        {
            if(error==null) return;
            var caption=InstantiateUi("Widgets/SaveError",overlay).GetComponent<Text>();
            caption.text=error;
            if(!syncRequired) return;
            AddSessionButton(overlay,"同步进度",()=>StartCoroutine(SyncProgress()),(.14f,.06f,.48f,.11f));
            AddSessionButton(overlay,"返回登录",LeaveAdventure,(.52f,.06f,.86f,.11f));
        }

        private void Change(JourneyAction action, int id=0, int option=0, bool preserveScroll=false,
            Action<AdventureResult> onSuccess=null) =>
            Change(new AdventureCommand(action,id,option),preserveScroll,onSuccess);

        private void Change(AdventureCommand command,bool preserveScroll=false,Action<AdventureResult> onSuccess=null)
        {
            if(busy || syncRequired) return;
            StartCoroutine(ChangeRoutine(command,preserveScroll,onSuccess));
        }

        private IEnumerator ChangeRoutine(AdventureCommand command,bool preserveScroll,Action<AdventureResult> onSuccess)
        {
            busy=true;
            AdventureResult result=null;
            if(command.Action==JourneyAction.ResolveEvent)
                yield return ResolveForJournal(command,reply=>result=reply);
            else yield return RunCommand(command,reply=>result=reply);
            busy=false;
            if(game.Phase!=Phase.Camp)
            {
                campPicker=false;
                campChallenges=false;
                campMore=false;
                campRewards=false;
                campProfile=false;
                campPage=CampPage.Adventure;
            }
            world.Refresh(game);
            if(result!=null && onSuccess!=null) onSuccess(result);
            else Refresh(preserveScroll);
        }

        private void LayoutPortrait()
        {
            bool journalAtBottom=journalScroll!=null && journalEntrance==null &&
                (journalScrollTouched ? IsJournalAtLatest() : journalFollowLatest);
            lastWidth=Screen.width;
            lastHeight=Screen.height;
            lastSafeArea=Screen.safeArea;
            var safe=lastSafeArea;
            if(safe.width<=0 || safe.height<=0) safe=new Rect(0,0,lastWidth,lastHeight);
            scaler.matchWidthOrHeight=lastWidth>lastHeight*PortraitAspect?1:0;
            Canvas.ForceUpdateCanvases();
            lastCanvasScale=canvas.scaleFactor;
            var dimensions=safe.size;
            dimensions.x=Math.Min(safe.width,safe.height*PortraitAspect);
            if(!Application.isMobilePlatform)
                dimensions.y=Math.Min(safe.height,dimensions.x/PortraitAspect);
            portrait.sizeDelta=dimensions/lastCanvasScale;
            portrait.anchoredPosition=(safe.center-new Vector2(lastWidth*.5f,lastHeight*.5f))/lastCanvasScale;
            ResizeCampMenu();
            if(journalScroll!=null) LayoutJournalStories();
            if(journalAtBottom) journalScroll.verticalNormalizedPosition=0;
        }

        private void DrawCamp()
        {
            var chapter=game.CampChapter;
            if(campPage==CampPage.Adventure) DrawCampAdventure(chapter);
            else DrawCampPage();
            if(campPage!=CampPage.Shop && campPage!=CampPage.Talent) DrawCampHeader();
            DrawCampMenu();
            var gm=UiChild(overlay,"GM");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gm.gameObject.SetActive(campPage==CampPage.Adventure);
            gm.SetAsLastSibling();
            if(normalAdventure!=null) UiChild(gm,"Label").GetComponent<Text>().text="GM 测试";
            gm.GetComponent<Button>().onClick.AddListener(RequestGmMenu);
#else
            gm.gameObject.SetActive(false);
#endif
            campPageDialog?.Invoke();
            if(campProfile) DrawCampProfile();
            if(campRewards) DrawCampChapterRewards(chapter);
            if(!campPicker) return;
            if(!campChallenges)
            {
                DrawChapterPicker();
                return;
            }
            var panel=Modal("ChallengePicker");
            UiChild(panel,"Back").GetComponent<Button>().onClick.AddListener(CloseCampPicker);
            var rest=UiChild(panel,"Rest").GetComponent<Button>();
            rest.interactable=game.Profile.stamina<30;
            rest.onClick.AddListener(()=>Change(JourneyAction.Rest,preserveScroll:true));
            UiChild(panel,"Chapters").GetComponent<Button>().onClick.AddListener(()=>{campChallenges=false;Refresh();});
            CampChallenges(panel);
        }

        private void CampChallenges(RectTransform panel)
        {
            for(int mode=1;mode<=8;mode++)
            {
                int selected=mode;
                var tab=UiChild(panel,"Modes/Mode "+mode).GetComponent<Button>();
                tab.interactable=challengeTab!=mode;
                tab.onClick.AddListener(()=>{challengeTab=selected;Refresh();});
            }
            currentScroll=UiChild(panel,"List").GetComponent<ScrollRect>();
            StartCoroutine(RestoreScroll(currentScroll,scrollPosition));
            var list=currentScroll.content;
            if(challengeTab==7)
            {
                LabelRow(list,$"今日活跃 {game.ActivityPoints}/100");
                foreach(var reward in game.ActivityRewards)
                    ListButton(list,$"{reward.threshold} 活跃 · "+(game.ActivityClaimed(reward)?"已领取":"领取奖励"),()=>Change(JourneyAction.ClaimActivity,reward.id,preserveScroll:true),game.CanClaimActivity(reward));
                return;
            }
            var choices=game.ChallengeChoices(challengeTab).ToArray();
            if(challengeTab==1 && game.NextTowerChest is TowerChest chest)
            {
                string prize=$"{game.ItemName(chest.reward.id)} ×{chest.reward.count}";
                ListButton(list,$"{chest.name}塔顶宝箱 · {prize}",()=>Change(JourneyAction.ClaimTowerChest,chest.tower,preserveScroll:true),
                    game.CanClaimTowerChest);
            }
            if(choices.Length==0) LabelRow(list,challengeTab==6?"今日远征已完成":"已完成全部挑战");
            foreach(var choice in choices)
            {
                string requirement=game.Profile.highest<choice.unlock?$"第 {choice.unlock} 章解锁":
                    choice.ticketId!=0?$"{game.ItemName(choice.ticketId)} {game.RemainingDungeonTickets(choice)}/{choice.ticketLimit}":$"{choice.fee} 体力";
                ListButton(list,$"{choice.name} · {requirement}",()=>Change(JourneyAction.StartChallenge,choice.id,choice.mode),game.CanStartChallenge(choice));
            }
        }

        private void DrawJourney()
        {
            var run=game.Run;
            var stats=UiChild(overlay,"Stats");
            experienceCaption=UiChild(stats,"Experience/Track/Value").GetComponent<Text>();
            experienceFill=UiChild(stats,"Experience/Track/Fill").GetComponent<Image>();
            healthCaption=UiChild(stats,"Health/Track/Value").GetComponent<Text>();
            healthFill=UiChild(stats,"Health/Track/Fill").GetComponent<Image>();
            healthPercent=UiChild(stats,"Health percent").GetComponent<Text>();
            attackCaption=UiChild(stats,"Attack/Track/Value").GetComponent<Text>();
            defenceCaption=UiChild(stats,"Defence/Track/Value").GetComponent<Text>();
            ShowJournalStats(journalPreviousStats ?? ReadJournalStats());
            bool fight=game.Phase==Phase.Battle;
            var settings=UiChild(overlay,"Settings").GetComponent<Button>();
            settings.interactable=fight || !busy;
            settings.onClick.AddListener(()=>{if(fight) ToggleBattlePause(); else OpenRetreat();});
            if(fight)
            {
                battleHeader=UiChild(overlay,"Battle Header");
                roundCaption=UiChild(battleHeader,"Round").GetComponent<Text>();
                roundCaption.text=$"回合 {Math.Max(1,run.round)}/{game.RoundLimit}";
                damageCaption=UiChild(battleHeader,"Damage").GetComponent<Text>();
                var speedControl=UiChild(overlay,"Speed");
                speedControl.GetComponent<Button>().onClick.AddListener(ToggleBattleSpeed);
                speedCaption=UiChild(speedControl,"Value").GetComponent<Text>();
                speedCaption.text="x"+speed;
                battleHeader.gameObject.SetActive(!world.HasEncounterIntro);
                speedCaption.transform.parent.gameObject.SetActive(!world.HasEncounterIntro);
            }
            else
            {
                var days=UiChild(overlay,"Day");
                var challenge=game.ActiveChallenge;
                int milestone=(run.dailyLoop?challenge.rewardStage:game.Chapter.rewardStage).FirstOrDefault(d=>d>run.stage);
                string path=run.stage<7 && run.replayOpening ? "天    ⚑ 1     ? 6     ⚔ 7    •••    ◆ "+milestone
                    : $"天    ⚑ {Math.Max(1,run.stage)}    •••    ◆ {Math.Max(run.stage,milestone)}";
                if(challenge!=null && !run.dailyLoop)
                    path=$"{challenge.name} · {Math.Max(1,run.stage)} / {game.StageCount}";
                UiChild(days,"Value").GetComponent<Text>().text=path;
                days.gameObject.SetActive(true);
            }

            if(run.stage>0 || run.journal.Count>0)
            {
                DrawJourneyJournal(run);
            }
            if(journalEntrance==null) FinishJournalPresentation();
            if(run.challengeMode==0 && game.DamageStatisticsUnlocked &&
                (fight || game.Phase==Phase.Trail || game.Phase==Phase.Event))
                AddDamageStatisticsEntry(overlay,false);
            if(game.Phase==Phase.Skill)
            {
                if(game.CurrentSkillOffer!=SkillOfferOrigin.Begin) JourneyButton("下一天",null,false);
                DrawSkills();
                return;
            }
            if(game.Phase==Phase.Result){DrawResult();return;}
            if(game.HasWheel)
            {
                if(run.wheelOpened) DrawWheel();
                else
                {
                    string action=string.IsNullOrEmpty(game.WheelAction) || game.WheelAction=="离开"?"打开转盘":game.WheelAction;
                    var controls=InstantiateUi("Widgets/WheelActions",overlay);
                    BindJourneyChoice(controls,"Left",null,()=>Change(JourneyAction.SkipWheel));
                    BindJourneyChoice(controls,"Right",action,()=>Change(JourneyAction.OpenWheel));
                }
                return;
            }

            if(fight || busy)
                JourneyButton(fight?"战斗中":"移动中",null,fight,ongoing:true);
            else if(game.HasJourneyChoice)
            {
                bool shop=run.eventKind==7;
                bool fork=run.eventKind==2;
                string action=shop?$"补给 · {Adventure.JourneySupplyPrice} 金币":fork?"调查":"抛竿";
                bool available=!shop || game.CanBuyJourneySupplies;
                if(shop)
                    UiChild(overlay,"Supplies").GetComponent<Text>().text=$"旅途金币 {run.gold}"+(available?"":" · 金币不足");
                var controls=InstantiateUi("Widgets/ChoiceActions",overlay);
                BindJourneyChoice(controls,"Left",action,()=>Change(JourneyAction.ResolveEvent),available);
                BindJourneyChoice(controls,"Right",fork?"绕行":"离开",()=>Change(JourneyAction.ResolveEvent,option:1));
            }
            else if(game.Phase==Phase.Event)
            {
                bool encounter=game.IsBattlePrompt;
                bool rest=run.eventKind==8;
                JourneyButton(encounter?"战斗":rest?"休息":"继续",()=>Change(JourneyAction.ResolveEvent),encounter,rest:rest);
            }
            else if(game.CanSettleDaily)
            {
                var controls=InstantiateUi("Widgets/ExpeditionActions",overlay);
                BindJourneyChoice(controls,"Left",null,()=>Change(JourneyAction.SettleDaily));
                BindJourneyChoice(controls,"Right",null,()=>StartCoroutine(Walk()));
            }
            else JourneyButton("下一天",()=>StartCoroutine(Walk()),false);
        }

        private static Button BindButton(Graphic image,Action action,bool enabled=true)
        {
            image.raycastTarget=true;
            var button=image.GetComponent<Button>();
            button.interactable=enabled;
            button.onClick.AddListener(()=>action());
            return button;
        }

        private static void BindJourneyChoice(RectTransform controls,string side,string label,Action action,bool available=true)
        {
            var face=UiChild(controls,side+"/Face");
            if(label!=null) UiChild(face,"Label").GetComponent<Text>().text=label;
            var button=face.GetComponent<Button>();
            button.interactable=available;
            button.onClick.AddListener(()=>action());
        }

        private void JourneyButton(string label,Action action,bool battle,bool ongoing=false,bool rest=false)
        {
            var root=InstantiateUi("Widgets/JourneyAction",overlay);
            root.name=label;
            string state=ongoing?(battle?"Fighting":"Walking"):rest?"Rest":battle?"Battle":"Next";
            var face=UiChild(root,state);
            UiChild(root,"Next").gameObject.SetActive(state=="Next");
            face.gameObject.SetActive(true);
            UiChild(face,"Label").GetComponent<Text>().text=label;
            if(state=="Fighting") battleDots=UiChild(face,"Dots").GetComponent<Text>();
            if(state=="Walking") journalTravelFill=UiChild(face,"Progress/Fill").GetComponent<Image>();
            var button=UiChild(root,"Frame").GetComponent<Button>();
            button.enabled=action!=null;
            if(action!=null)
            {
                button.targetGraphic=face.GetComponent<Image>();
                button.interactable=journalEntrance==null;
                button.onClick.AddListener(()=>{if(journalEntrance==null) action();});
                journalAction=button;
            }
        }

        private void UpdateJourneyHealth(int health)
        {
            displayedHp=health;
            float fraction=Mathf.Clamp01(health/(float)Math.Max(1,displayedMaxHp));
            healthCaption.text=$"{health}/{displayedMaxHp}";
            healthPercent.text=$"{Mathf.RoundToInt(fraction*100)}%";
            healthFill.fillAmount=fraction;
        }

        private IEnumerator Walk()
        {
            if(busy || syncRequired || game.Phase!=Phase.Trail) yield break;
            busy=true;
            FollowLatestJournal();
            world.BeginWalk(game.Run.seed,game.Run.stage+1);
            if(game.UsesJourneyMovement && !world.IsSeaJourney && steps!=null) audioSource.PlayOneShot(steps);
            Refresh();
            while(world.JourneyProgress<1)
            {
                if(journalTravelFill!=null)
                    journalTravelFill.fillAmount=world.JourneyProgress;
                yield return null;
            }
            yield return ResolveForJournal(new AdventureCommand(JourneyAction.Advance));
            busy=false;
            world.Refresh(game);
            Refresh();
        }

        private IEnumerator ResolveForJournal(AdventureCommand command,Action<AdventureResult> completed=null)
        {
            FollowLatestJournal();
            var before=ReadJournalStats();
            yield return RunCommand(command,result=>
            {
                if(result!=null && game.Phase==Phase.Trail) journalPreviousStats=before;
                completed?.Invoke(result);
            });
        }

        private void ToggleBattlePause()
        {
            paused=!paused;
            if(roundCaption!=null) roundCaption.text=paused?"暂停":$"回合 {Math.Max(1,game.Run.round)}/{game.RoundLimit}";
        }

        private void ToggleBattleSpeed()
        {
            speed=speed==1?2:1;
            speedCaption.text="x"+speed;
        }

        private IEnumerator WaitForBattle(float seconds)
        {
            while(seconds>0)
            {
                yield return null;
                seconds-=BattleTimeStep;
            }
        }

        private IEnumerator PlayRound()
        {
            if(busy || syncRequired || game.Phase!=Phase.Battle) yield break;
            busy=playingBattle=true;
            float approach=world.BeginEncounterApproach();
            if(approach>0)
            {
                while(world.IsEncounterApproaching) yield return null;
                speedCaption?.transform.parent.gameObject.SetActive(true);
                yield return WaitForBattle(world.PlayFightBanner());
                world.ShowBattleBars();
                battleHeader?.gameObject.SetActive(true);
            }
            // Resolve and save once. The presentation consumes the outcome without writing to the save.
            if(game.DamageStatisticsUnlocked)
                displayedBattleStatistics=new List<BattleStatistic>(game.Run.battleStatistics);
            var beforeRound=ReadJournalStats();
            int roundLimit=game.RoundLimit;
            RoundPlayback report=null;
            yield return RunCommand(new AdventureCommand(JourneyAction.PlayRound),result=>report=result?.round);
            if(report==null)
            {
                busy=playingBattle=false;
                displayedBattleStatistics=null;
                world.Refresh(game);
                Refresh();
                yield break;
            }
            if(report.outcome==BattleRoundOutcome.Victory && game.Phase==Phase.Trail)
            {
                beforeRound.hp=game.Run.hp;
                journalPreviousStats=beforeRound;
            }
            if(roundCaption!=null) roundCaption.text=$"回合 {report.number}/{roundLimit}";
            int damageHits=0;
            foreach(var row in report.actions)
            {
                var action=row.ToAction();
                string skillName=action.SkillId==0?null:game.FindSkill(action.SkillId).name;
                float windup=world.BeginAction(action,skillName);
                if(windup>0) yield return WaitForBattle(windup);
                float flight=world.LaunchActionProjectile(action);
                if(flight>0) yield return WaitForBattle(flight);
                world.ShowAction(action);
                if(displayedBattleStatistics!=null) BattleStatistic.Record(displayedBattleStatistics,action);
                if(action.Source<0 && action.Target>=0 && action.Amount>0 && damageCaption!=null)
                    damageCaption.text=(++damageHits).ToString();
                if(action.Target==CombatAction.Hero && healthCaption!=null && targetHp!=action.TargetHp)
                {
                    previousHp=displayedHp;
                    targetHp=action.TargetHp;
                    healthChangeElapsed=0;
                }
                bool strike=(windup+flight)>0 && (action.Amount>0 || action.Absorbed>0);
                if(strike && impact!=null) audioSource.PlayOneShot(impact);
                yield return WaitForBattle(world.ActionRecovery(action,windup+flight));
                world.EndAction();
            }
            world.UpdateRage(game.Run.recharge);
            float finishDelay=report.outcome==BattleRoundOutcome.Continue?.12f:Mathf.Max(.35f,world.BattleExitRecovery);
            yield return WaitForBattle(finishDelay);
            if(report.outcome==BattleRoundOutcome.Victory && victory!=null) audioSource.PlayOneShot(victory);
            busy=playingBattle=false;
            displayedBattleStatistics=null;
            if(report.outcome!=BattleRoundOutcome.Continue) world.Refresh(game,report.outcome==BattleRoundOutcome.NextWave);
            if(game.Phase==Phase.Battle)
            {
                UpdateJournal(game.Run);
                UpdateSaveError();
            }
            else Refresh();
        }

        private RectTransform Modal(string name) => UiChild(LoadView(name,overlay),"Content");

        private void DrawResult()
        {
            var run=game.Run;
            if(!run.sweep && run.challengeMode==0)
            {
                DrawAdventureResult(run);
                return;
            }
            var panel=Modal("ChallengeResult");
            var art=UiChild(panel,"Artwork").GetComponent<RawImage>();
            art.texture=world.Texture(run.won?"Chest":"Camp");
            art.enabled=art.texture!=null;
            UiChild(panel,"Heading").GetComponent<Text>().text=run.sweep?"扫荡完成":run.won?"冒险胜利":"旅程结束";
            UiChild(panel,"Balance").GetComponent<Text>().text=$"金币 +{run.gold+run.milestoneGold}  ·  体力 {game.Profile.stamina}/30";
            UiChild(panel,"Loot").GetComponent<Text>().text=string.Join("  ·  ",run.loot.Take(5).Select(x=>$"{game.ItemName(x.id)} ×{x.count}"));
            UiChild(panel,"Return").GetComponent<Button>().onClick.AddListener(()=>Change(JourneyAction.ReturnToCamp));
        }

        private void OpenRetreat()
        {
            paused=true;
            var panel=Modal("Retreat");
            UiChild(panel,"Resume").GetComponent<Button>().onClick.AddListener(()=>{paused=false;Refresh();});
            UiChild(panel,"Retreat").GetComponent<Button>().onClick.AddListener(()=>{paused=false;Change(JourneyAction.Abandon);});
            AddSessionButton(panel,"返回登录",LeaveAdventure,(.29f,.02f,.71f,.13f));
            var gm=UiChild(panel,"GM");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gm.GetComponent<Button>().onClick.AddListener(RequestGmMenu);
#else
            gm.gameObject.SetActive(false);
#endif
        }

        private IEnumerator RestoreScroll(ScrollRect scroll,float value)
        {
            yield return null;
            if(scroll!=null) scroll.verticalNormalizedPosition=value;
        }

        private RectTransform Row(RectTransform list,int height)
        {
            var row=InstantiateUi("Widgets/ListRow",list);
            row.GetComponent<LayoutElement>().preferredHeight=height;
            return row;
        }

        private void LabelRow(RectTransform list,string label)
        {
            var row=InstantiateUi("Widgets/ListLabel",list);
            var text=UiChild(row,"Label").GetComponent<Text>();
            text.font=font;
            text.text=FormatUiText(label);
        }

        private void ListButton(RectTransform list,string label,Action action,bool enabled)
        {
            var row=InstantiateUi("Widgets/ListAction",list);
            BindUiButton(row,label,action,enabled);
        }

        internal static RectTransform NewRect(string name,RectTransform parent,(float x, float y, float right, float top) bounds)
        {
            var (x, y, right, top) = bounds;
            var rt=InstantiateUi("Widgets/Rect",parent);
            rt.name=name;
            PlaceUi(rt,x,y,right,top);
            return rt;
        }

        internal static RectTransform LoadView(string name,RectTransform parent) => InstantiateUi("Views/"+name,parent);

        internal static RectTransform UiChild(RectTransform parent,string path) =>
            parent.Find(path) as RectTransform ?? throw new InvalidOperationException($"UI Prefab {parent.name} 缺少节点 {path}");

        internal static RectTransform MountPart(RectTransform parent,string path,string name=null)
        {
            int separator=path.IndexOf('/');
            var source=UiChild(UiTemplate("Parts/"+path.Substring(0,separator)),path.Substring(separator+1));
            var instance=Instantiate(source,parent,false);
            instance.name=name??source.name;
            return instance;
        }

        internal static RectTransform MountPanel(RectTransform parent,string path,Color color)
        {
            var panel=MountPart(parent,path);
            panel.GetComponent<Image>().color=color;
            return panel;
        }

        internal static Text MountText(RectTransform parent,string path,string value=null,int? size=null,Color? color=null,
            TextAnchor? alignment=null,FontStyle? style=null,float? strokeAlpha=null,Font selectedFont=null)
        {
            var text=MountPart(parent,path).GetComponent<Text>();
            if(value!=null) text.text=FormatUiText(value);
            if(size.HasValue)
            {
                text.fontSize=size.Value;
                text.resizeTextMinSize=Math.Min(text.resizeTextMinSize,size.Value);
                text.resizeTextMaxSize=size.Value;
            }
            if(color.HasValue) text.color=color.Value;
            if(alignment.HasValue) text.alignment=alignment.Value;
            if(style.HasValue) text.fontStyle=style.Value;
            if(selectedFont!=null) text.font=selectedFont;
            if(strokeAlpha.HasValue)
            {
                var outline=text.GetComponent<GameTextOutline>();
                var ink=outline.effectColor;
                ink.a=strokeAlpha.Value;
                outline.effectColor=ink;
            }
            return text;
        }

        private Image MountSprite(RectTransform parent,string path,string sprite=null,bool sliced=false,bool viewportScale=false,Color? color=null)
        {
            var image=MountPart(parent,path).GetComponent<Image>();
            if(color.HasValue) image.color=color.Value;
            if(sprite!=null) SetUiSprite(image,sprite,sliced);
            if(viewportScale) image.pixelsPerUnitMultiplier=OutputWidth/portrait.sizeDelta.x;
            return image;
        }

        internal static RawImage MountRaw(RectTransform parent,string path,Texture texture=null)
        {
            var image=MountPart(parent,path).GetComponent<RawImage>();
            if(texture!=null)
            {
                image.texture=texture;
                image.enabled=true;
            }
            return image;
        }

        private RectTransform MountList(RectTransform parent,string path)
        {
            currentScroll=MountPart(parent,path).GetComponent<ScrollRect>();
            StartCoroutine(RestoreScroll(currentScroll,scrollPosition));
            return currentScroll.content;
        }

        private Button MountButton(RectTransform parent,string path,Action action,bool enabled=true,string label=null,string name=null,
            bool viewportScale=false,Color? color=null,Color? labelColor=null)
        {
            var node=MountPart(parent,path,name);
            var button=node.GetComponent<Button>();
            button.interactable=enabled;
            button.onClick.AddListener(()=>action());
            if(viewportScale) node.GetComponent<Image>().pixelsPerUnitMultiplier=OutputWidth/portrait.sizeDelta.x;
            if(color.HasValue) node.GetComponent<Image>().color=color.Value;
            if(label!=null || labelColor.HasValue)
            {
                var caption=UiChild(node,"Label").GetComponent<Text>();
                if(label!=null) caption.text=FormatUiText(label);
                if(labelColor.HasValue) caption.color=labelColor.Value;
            }
            return button;
        }

        private static readonly Dictionary<string,RectTransform> UiPrefabs=new Dictionary<string,RectTransform>();

        private static RectTransform UiTemplate(string path)
        {
            if(!UiPrefabs.TryGetValue(path,out var prefab) || prefab==null)
            {
                var asset=Resources.Load<GameObject>("UI/"+path);
                prefab=asset==null?null:asset.GetComponent<RectTransform>();
                if(prefab==null) throw new InvalidOperationException("找不到 UI Prefab UI/"+path);
                UiPrefabs[path]=prefab;
            }
            return prefab;
        }

        internal static RectTransform InstantiateUi(string path,RectTransform parent)
        {
            var prefab=UiTemplate(path);
            var instance=Instantiate(prefab,parent,false);
            instance.name=prefab.name;
            return instance;
        }

        private static void PlaceUi(RectTransform rt,float x,float y,float right,float top)
        {
            rt.anchorMin=new Vector2(x,y);
            rt.anchorMax=new Vector2(right,top);
            rt.offsetMin=Vector2.zero;
            rt.offsetMax=Vector2.zero;
        }

        internal static Color Tint(int rgb)=>new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255);

        internal static RectTransform Panel(RectTransform parent,Color color,(float x, float y, float right, float top) bounds)
        {
            var (x, y, right, top) = bounds;
            var rt=InstantiateUi("Widgets/Panel",parent);
            PlaceUi(rt,x,y,right,top);
            var image=rt.GetComponent<Image>();
            image.color=color;
            image.raycastTarget=false;
            return rt;
        }

        internal static RawImage ImageAt(RectTransform parent,Texture texture,(float x, float y, float right, float top) bounds)
        {
            var (x, y, right, top) = bounds;
            var rt=InstantiateUi("Widgets/Art",parent);
            PlaceUi(rt,x,y,right,top);
            var image=rt.GetComponent<RawImage>();
            image.texture=texture;
            image.raycastTarget=false;
            image.enabled=texture!=null;
            return image;
        }

        // Imported descriptions can contain escaped line breaks alongside rich-text tags.
        private static string FormatUiText(string text) =>
            text?.Replace(@"\r\n","\n").Replace(@"\n","\n").Replace(@"\r","\n")??string.Empty;

        private void BindUiButton(RectTransform node,string label,Action action,bool enabled)
        {
            var button=node.GetComponent<Button>();
            button.interactable=enabled;
            button.onClick.AddListener(()=>action());
            var text=UiChild(node,"Label").GetComponent<Text>();
            text.font=font;
            text.text=FormatUiText(label);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly int[] GmModes={0,1,2,3,4,5,6,8};
        private Adventure normalAdventure;
        private RectTransform gmMenu;
        private bool gmRequested, normalBattlePaused;
        private int gmMode, normalBattleSpeed;

        private void RequestGmMenu() => gmRequested=true;

        private void UpdateGmMenu()
        {
            if(Input.GetKeyDown(KeyCode.F10))
            {
                if(gmMenu!=null) CloseGmMenu();
                else gmRequested=!gmRequested;
            }
            if(!gmRequested || busy || game==null) return;
            gmRequested=false;
            DrawGmMenu();
        }

        private void DrawGmMenu()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            gmMenu=LoadView("GmPicker",portrait);
            var panel=UiChild(gmMenu,"Content");
            Button Control(string name) => UiChild(panel,name).GetComponent<Button>();
            Control("Close").onClick.AddListener(CloseGmMenu);
            string session=normalAdventure==null?"从当前角色创建临时测试":
                game.Run!=null?$"临时测试档 · 当前种子 {game.Run.seed}":"正在使用临时测试档";
            UiChild(panel,"Session").GetComponent<Text>().text=session;

            var search=UiChild(panel,"Search").GetComponent<InputField>();
            var seedField=UiChild(panel,"Seed").GetComponent<InputField>();
            var rows=UiChild(panel,"Levels");
            var message=UiChild(panel,"Message").GetComponent<Text>();
            int page=0;

            void DrawRows()
            {
                string query=search.text.Trim();
                bool byId=int.TryParse(query,NumberStyles.Integer,CultureInfo.InvariantCulture,out int id);
                var matches=game.GmLevels(gmMode)
                    .Where(level=>byId?level.id==id:level.name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)
                    .OrderBy(level=>level.id).ToArray();
                int pageSize=rows.childCount;
                if(pageSize==0) throw new InvalidOperationException("GM 选关 Prefab 缺少关卡行");
                int pageCount=Math.Max(1,(matches.Length+pageSize-1)/pageSize);
                page=Mathf.Clamp(page,0,pageCount-1);
                for(int index=0;index<pageSize;index++)
                {
                    var row=(RectTransform)rows.GetChild(index);
                    var button=row.GetComponent<Button>();
                    button.onClick.RemoveAllListeners();
                    int position=page*pageSize+index;
                    row.gameObject.SetActive(position<matches.Length);
                    if(position>=matches.Length) continue;
                    var level=matches[position];
                    BindUiButton(row,$"{level.id}  {level.name}",()=>
                    {
                        string seedText=seedField.text.Trim();
                        int seed=Environment.TickCount;
                        if(seedText.Length>0 && !int.TryParse(seedText,NumberStyles.Integer,CultureInfo.InvariantCulture,out seed))
                        {
                            message.text="种子须为 32 位整数。";
                            return;
                        }
                        StartGmRun(level.id,seed);
                    },true);
                }
                message.text=matches.Length==0?"该玩法下没有找到关卡。":
                    $"{(gmMode==0?"普通章节":Adventure.ChallengeName(gmMode))} · {matches.Length} 关 · 第 {page+1}/{pageCount} 页";
            }

            for(int index=0;index<GmModes.Length;index++)
            {
                int mode=GmModes[index];
                Control("Mode "+index).onClick.AddListener(()=>
                {
                    gmMode=mode;
                    page=0;
                    search.SetTextWithoutNotify("");
                    DrawRows();
                });
            }
            search.onValueChanged.AddListener(_=>{page=0;DrawRows();});
            Control("Previous").onClick.AddListener(()=>{page--;DrawRows();});
            Control("Next").onClick.AddListener(()=>{page++;DrawRows();});
            var exit=Control("Exit test");
            exit.gameObject.SetActive(normalAdventure!=null);
            exit.onClick.AddListener(ExitGmRun);
            DrawRows();
        }

        private void CloseGmMenu()
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null && selected.transform.IsChildOf(gmMenu))
                EventSystem.current.SetSelectedGameObject(null);
            gmMenu.gameObject.SetActive(false);
            Destroy(gmMenu.gameObject);
            gmMenu=null;
        }

        private void StartGmRun(int id,int seed)
        {
            var test=(normalAdventure??game).CreateGmRun(gmMode,id,seed);
            if(test==null) return;
            if(normalAdventure==null)
            {
                normalAdventure=game;
                normalBattlePaused=paused && game.Phase==Phase.Battle;
                normalBattleSpeed=speed;
            }
            CloseGmMenu();
            paused=false;
            speed=1;
            SwitchGmAdventure(test);
        }

        private void ExitGmRun()
        {
            var normal=normalAdventure;
            normalAdventure=null;
            CloseGmMenu();
            paused=normalBattlePaused;
            speed=normalBattleSpeed;
            SwitchGmAdventure(normal);
        }

        private void SwitchGmAdventure(Adventure adventure)
        {
            journalPreviousStats=null;
            campPageDialog=null;
            campMore=campPicker=campChallenges=campProfile=campRewards=false;
            campPage=CampPage.Adventure;
            audioSource.Stop();
            game=adventure;
            world.Refresh(game);
            Refresh();
            if(paused && roundCaption!=null) roundCaption.text="暂停";
        }
#endif
    }
}
