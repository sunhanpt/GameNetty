using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private const string AdventureVictoryArt="Reference/AdventureVictory/";
        private Run lastResultSound;

        private void DrawAdventureResult(Run run)
        {
            // UIGameEventFinish uses a full-screen mask and center-relative offsets on its 1080x1920 canvas.
            var result=LoadView("AdventureResult",overlay);
            result.name="UIGameEventFinish";
            result.GetComponent<Image>().raycastTarget=true;

            RectTransform rays=null;
            Image[] particles=null;
            if(run.won)
            {
                rays=MountPart(result, "Victory/DrawAdventureResult/UIFX_Victory_Ray");
                particles=new Image[9];
                particles[0]=MountSprite(rays, "Victory/DrawAdventureResult/Glow");
                particles[1]=MountSprite(rays, "Victory/DrawAdventureResult/Rays");
                particles[2]=MountSprite(rays, "Victory/DrawAdventureResult/Rays 2");
                for(int i=3;i<particles.Length;i++) particles[i]=MountSprite(rays, "Victory/DrawAdventureResult/Star");
                rays.gameObject.SetActive(false);
            }
            var light=run.won?MountSprite(result, "Victory/DrawAdventureResult/VictoryLight"):
                MountSprite(result, "Victory/DrawAdventureResult/DefeatLight");
            var band=InstantiateUi("Widgets/AdventureRewardBand",result).GetComponent<Image>();
            SetUiSprite(band,AdventureVictoryArt+(run.won?"RewardBand":"DefeatBand"));

            var spine=InstantiateUi("Widgets/AdventureVictoryAnimation",result).GetComponent<ActorAnimationGraphic>();
            spine.name=run.won?"SpineWin":"SpineLose";
            string animation=run.won?"ShengLi":"ShiBai";
            bool hasSpine=spine.LoadUiEffect("JourneyVictory",animation+"_1",animation+"_2",spine.UiEffectScale);
            if(!hasSpine) spine.gameObject.SetActive(false);

            var titleColor=Tint(run.won?0xFFED73:0xE9E8FF);
            var title=MountText(result, "Victory/DrawAdventureResult/title", value: run.won?"胜利":"战败", color: titleColor);
            var chapter=MountSprite(result, "Victory/DrawAdventureResult/chapter", sprite: "Reference/AdventureVictory/" + (run.won?"Chapter":"DefeatChapter"), sliced: true).rectTransform;
            MountText(chapter, "Victory/DrawAdventureResult/Text", value: $"章节{run.chapter}", size: VictoryFont(38));
            var daysLabel=MountText(result, "Victory/DrawAdventureResult/生存天数", color: Tint(run.won?0xFFD0D7:0xC4A9E7));
            var days=MountText(result, "Victory/DrawAdventureResult/days", value: $"{run.stage} 天");
            days.GetComponent<GameTextOutline>().effectColor=Tint(run.won?0x8B2521:0x5D7C76);
            var record=MountText(result, "Victory/DrawAdventureResult/record", value: $"最高旅程：{game.ChapterBestStage(game.Chapter)}天", color: titleColor);

            var prizes=DrawAdventureRewards(result,run);
            var back=InstantiateUi("Widgets/ResultReturn",result);
            UiChild(back,"Frame").GetComponent<Button>().onClick.AddListener(()=>Change(JourneyAction.ReturnToCamp));

            var book=MountSprite(result, "Victory/DrawAdventureResult/book");
            SetUiSprite(book,"Reference/SkillUI/Chrome/LearnedBook");
            MountText(result, "Victory/DrawAdventureResult/已学技能");
            BindButton(book,()=>DrawLearnedSkills(result));
            if(game.DamageStatisticsUnlocked) AddDamageStatisticsEntry(result,true);

            if(lastResultSound!=run)
            {
                var clip=Resources.Load<AudioClip>(run.won?"Audio/ChapterWin":"Audio/ChapterDefeat");
                if(clip!=null) audioSource.PlayOneShot(clip,run.won?.6f:.5f);
                lastResultSound=run;
            }
            StartCoroutine(AnimateAdventureResult(result,hasSpine?spine:null,rays,particles,light,band,prizes,back,
                new[]{title.rectTransform,chapter,daysLabel.rectTransform,days.rectTransform,record.rectTransform}));
        }

        private List<RectTransform> DrawAdventureRewards(RectTransform result,Run run)
        {
            var rewards=new List<Reward>();
            int gold=run.gold+run.milestoneGold;
            if(gold>0) rewards.Add(new Reward{id=1,count=gold});
            rewards.AddRange(run.loot);
            var prizes=new List<RectTransform>();
            if(rewards.Count==0)
            {
                prizes.Add(MountText(result, "Victory/DrawAdventureRewards/未获得战利品").rectTransform);
                return prizes;
            }

            var scroll=InstantiateUi("Widgets/AdventureRewardGrid",result).GetComponent<ScrollRect>();
            foreach(var reward in rewards)
            {
                var card=InstantiateUi("Widgets/AdventureRewardCard",scroll.content);
                card.name=$"Reward {reward.id}";
                int quality=reward.id switch {2 or 61=>5,1 or 24 or 60 or 72=>2,73=>3,_=>1};
                SetUiSprite(UiChild(card,"Frame").GetComponent<Image>(),AdventureVictoryArt+"Frame"+quality);
                var icon=UiChild(card,"Icon").GetComponent<Image>();
                SetUiSprite(icon,AdventureVictoryArt+"Item"+reward.id,false,true);
                var fallback=UiChild(card,"Fallback").GetComponent<Text>();
                fallback.gameObject.SetActive(icon.sprite==null);
                if(icon.sprite==null) fallback.text=FormatUiText(game.ItemName(reward.id));
                UiChild(card,"Quantity").GetComponent<Text>().text="x"+reward.count;
                prizes.Add(card);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.vertical=scroll.content.rect.height>scroll.viewport.rect.height;
            return prizes;
        }

        private IEnumerator AnimateAdventureResult(RectTransform result,ActorAnimationGraphic spine,RectTransform rays,
            Image[] particles,Image light,Image band,List<RectTransform> prizes,RectTransform back,RectTransform[] labels)
        {
            const float interval=.15f;
            float opening=spine!=null?spine.Duration("effect"):0f;
            float start=Time.unscaledTime;
            float end=start+opening;
            float rewardStart=(labels.Length+1)*interval;
            bool revealed=false;
            while(result!=null && result.gameObject.activeInHierarchy)
            {
                float time=Time.unscaledTime;
                float elapsed=time-end;
                bool intro=elapsed<0;
                if(spine!=null) spine.SetPose(intro?"effect":"loop",intro?time-start:elapsed,!intro);
                if(!intro && rays!=null)
                {
                    if(!rays.gameObject.activeSelf) rays.gameObject.SetActive(true);
                    AnimateVictoryRays(particles,elapsed);
                }
                float age=elapsed-rewardStart;
                if(age>=0 && rays!=null && light.sprite!=null)
                    light.color=new Color(1,1,1,Mathf.Lerp(1,.5f,Mathf.PingPong(age,1)));
                if(!revealed)
                {
                    for(int i=0;i<labels.Length;i++)
                    {
                        float reveal=elapsed-i*interval;
                        labels[i].gameObject.SetActive(reveal>=0);
                        labels[i].localScale=Vector3.one*Mathf.Lerp(i==0?3:2,1,VictoryEase(reveal/interval));
                    }
                    float bandProgress=VictoryEase((elapsed-labels.Length*interval)/interval);
                    band.rectTransform.sizeDelta=new Vector2(0,VictoryUnits(750)*bandProgress);
                    light.gameObject.SetActive(age>=0);
                    for(int i=0;i<prizes.Count;i++)
                        prizes[i].localScale=Vector3.one*VictoryPop(age-i*.05f,1.1f);
                    back.localScale=Vector3.one*VictoryPop(age,1.2f);
                    revealed=age>=prizes.Count*.05f+.2f;
                }
                yield return null;
            }
        }

        private static void AnimateVictoryRays(Image[] particles,float elapsed)
        {
            float alpha=Mathf.Clamp01(elapsed/.3f);
            for(int i=0;i<3;i++)
            {
                var image=particles[i];
                if(image.sprite==null) continue;
                image.color=new Color(1,.75f,.28f,alpha*(i==0?.38f:.23f));
                if(i!=0) image.rectTransform.localRotation=Quaternion.Euler(0,0,i*30+elapsed*(i==1?9:-6));
            }
            for(int i=3;i<particles.Length;i++)
            {
                var star=particles[i];
                if(star.sprite==null) continue;
                float phase=Mathf.Repeat(elapsed*.6f+i*.217f,1);
                float angle=i*2.4f;
                var delta=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)*.8f)*VictoryUnits(200+240*phase);
                star.rectTransform.anchoredPosition=delta;
                star.rectTransform.localScale=Vector3.one*(.45f+.55f*phase);
                star.color=new Color(1,.9f,.55f,alpha*.7f*Mathf.Sin(phase*Mathf.PI));
            }
        }

        private static float VictoryEase(float progress)
        {
            float remaining=1-Mathf.Clamp01(progress);
            return 1-remaining*remaining;
        }

        private static float VictoryPop(float elapsed,float peak) => elapsed<.15f
            ?peak*VictoryEase(elapsed/.15f):Mathf.Lerp(peak,1,Mathf.Clamp01((elapsed-.15f)/.05f));

        private static float VictoryUnits(float pixels) => pixels*ReferenceWidth/1080f;
        private static int VictoryFont(int pixels) => Mathf.RoundToInt(VictoryUnits(pixels));

    }
}
