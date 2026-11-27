using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private const string SkillUiPath="Reference/SkillUI/";
        private readonly Dictionary<string,Sprite> uiSprites=new Dictionary<string,Sprite>();
        private ActorAnimationGraphic heroPreview;

        private void DrawSkills()
        {
            string view=game.Run.challengeOptionType switch
            {
                1=>"AngelChoice",
                2=>"DemonChoice",
                3=>"ChestChoice",
                _=>"SkillSelection"
            };
            var shade=LoadView(view,overlay);
            shade.name="UISelectSkill";
            shade.GetComponent<Image>().raycastTarget=true;
            if(game.Run.challengeOptionType!=0)
            {
                DrawChallengeChoice(shade,game.Run.challengeOptionType);
                return;
            }

            var preview=UiChild(shade,"Hero").GetComponent<ActorAnimationGraphic>();
            if(preview.Load("Hero"))
            {
                heroPreview=preview;
                preview.gameObject.SetActive(true);
            }

            string prompt=game.Run.offerPicksLeft>1?$"从下方选择 {game.Run.offerPicksLeft} 个技能":"从下方选择一个技能";
            UiChild(shade,"Prompt").GetComponent<Text>().text=prompt;
            var slots=UiChild(shade,game.Run.choices.Count>3?"Compact cards":"Standard cards");
            var cards=BindSkillCards(slots,true,out int highestQuality);
            StartCoroutine(RevealSkillChoices(shade,cards,highestQuality));

            int refreshes=game.SkillRefreshesRemaining;
            var refresh=UiChild(shade,"Refresh");
            refresh.gameObject.SetActive(refreshes>0);
            if(refreshes>0)
            {
                UiChild(refresh,"Label").GetComponent<Text>().text=$"刷新({refreshes})";
                refresh.GetComponent<Button>().onClick.AddListener(()=>Change(JourneyAction.RefreshSkills,preserveScroll:true));
            }

            UiChild(shade,"Learned").GetComponent<Button>().onClick.AddListener(()=>DrawLearnedSkills(shade));
        }

        private void DrawChallengeChoice(RectTransform screen,int option)
        {
            bool box=option==3;
            bool opened=box && game.Run.challengeBoxOpened;
            string model=option==1?"EventAngel":option==2?"EventDemon":"EventBox";
            screen.name="UIGame"+model;
            var actor=UiChild(screen,box && !opened?"Closed actor":"Actor").GetComponent<ActorAnimationGraphic>();
            string opening=box?(opened?"Open":"Appear"):option==2?"Appear":"Idle";
            string loop=opened?"Open_Idle":"Idle";
            bool animated=actor.LoadUiEffect(model,opening,loop,actor.UiEffectScale,box?"Chest_01":null);
            actor.gameObject.SetActive(animated);
            if(animated) StartCoroutine(AnimateChallengeChoice(actor));
            var choices=UiChild(screen,"Choices");
            if(box)
            {
                var open=UiChild(screen,"Open");
                open.gameObject.SetActive(!opened);
                if(!opened)
                {
                    choices.gameObject.SetActive(false);
                    open.GetComponent<Button>().onClick.AddListener(()=>Change(JourneyAction.OpenChallengeBox));
                    return;
                }
            }

            var skill=game.FindSkill(game.Run.choices[0]);
            string prompt=option==1?"选择技能，或接受治疗":option==2?$"交易将减少 {skill.demonCost}% 生命上限":"选择一个技能";
            UiChild(choices,"Prompt").GetComponent<Text>().text=prompt;
            var cards=BindSkillCards(UiChild(choices,"Cards"),option!=2,out int highest);
            if(opened && animated)
            {
                choices.gameObject.SetActive(false);
                StartCoroutine(ShowBoxChoicesAfterOpening(choices,cards,highest,actor.Duration("effect")));
            }
            else StartCoroutine(RevealSkillChoices(choices,cards,highest));
            if(option==1)
            {
                var recovery=UiChild(screen,"Recover");
                UiChild(recovery,"Label").GetComponent<Text>().text=$"恢复 {game.AngelRecoveryPercent}% 生命";
                recovery.GetComponent<Button>().onClick.AddListener(()=>Change(JourneyAction.ChallengeAlternative));
            }
            if(option==2)
            {
                UiChild(screen,"Decline").GetComponent<Button>().onClick.AddListener(()=>Change(JourneyAction.ChallengeAlternative));
                UiChild(screen,"Accept").GetComponent<Button>().onClick.AddListener(()=>Change(JourneyAction.SelectSkill,skill.id));
            }
            UiChild(choices,"Learned").GetComponent<Button>().onClick.AddListener(()=>DrawLearnedSkills(screen));
        }

        private List<CanvasGroup> BindSkillCards(RectTransform slots,bool selectable,out int highest)
        {
            var choices=game.Run.choices;
            while(slots.childCount<choices.Count) ExtendSkillSlots(slots);
            var cards=new List<CanvasGroup>(choices.Count);
            highest=0;
            for(int index=0;index<choices.Count;index++)
            {
                var skill=game.FindSkill(choices[index]);
                highest=Math.Max(highest,skill.quality);
                cards.Add(DrawSkillChoice((RectTransform)slots.GetChild(index),skill,selectable));
            }
            return cards;
        }

        private static void ExtendSkillSlots(RectTransform slots)
        {
            var last=(RectTransform)slots.GetChild(slots.childCount-1);
            var previous=(RectTransform)slots.GetChild(slots.childCount-2);
            var extra=Instantiate(last,slots,false);
            extra.name="Card "+slots.childCount;
            extra.anchorMin+=last.anchorMin-previous.anchorMin;
            extra.anchorMax+=last.anchorMax-previous.anchorMax;
        }

        private static IEnumerator ShowBoxChoicesAfterOpening(RectTransform choices,List<CanvasGroup> cards,int quality,float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            if(choices==null) yield break;
            choices.gameObject.SetActive(true);
            yield return RevealSkillChoices(choices,cards,quality);
        }

        private static IEnumerator AnimateChallengeChoice(ActorAnimationGraphic actor)
        {
            float opening=actor.Duration("effect");
            for(float t=0;actor!=null && actor.gameObject.activeInHierarchy;t+=Time.unscaledDeltaTime)
            {
                if(t<opening) actor.SetPose("effect",t,false);
                else actor.SetPose("loop",t-opening,true);
                yield return null;
            }
        }

        private CanvasGroup DrawSkillChoice(RectTransform slot,Skill skill,bool selectable)
        {
            int quality=Mathf.Clamp(skill.quality,0,3);
            var card=InstantiateUi("Widgets/SkillCard",slot);
            card.name="Skill "+skill.id;
            card.localScale=Vector3.one*.5f;
            var hit=UiChild(card,"Hit").GetComponent<Button>();
            hit.gameObject.SetActive(selectable);
            if(selectable) hit.onClick.AddListener(()=>Change(JourneyAction.SelectSkill,skill.id));
            SetSkillSprite(UiChild(card,"Background").GetComponent<Image>(),"Chrome/Row"+quality,true);
            var icon=UiChild(card,"Icon frame");
            SetSkillSprite(icon.GetComponent<Image>(),"Chrome/Frame"+quality);
            SetSkillSprite(UiChild(icon,"Icon").GetComponent<Image>(),skill.iconAtlasId+"/"+skill.icon,false,true);
            var badge=UiChild(icon,"Badge");
            bool hasBadge=skill.badgeAtlasId>0 && !string.IsNullOrEmpty(skill.badge);
            badge.gameObject.SetActive(hasBadge);
            if(hasBadge) SetSkillSprite(badge.GetComponent<Image>(),skill.badgeAtlasId+"/"+skill.badge,false,true);
            UiChild(icon,"Upgrade").gameObject.SetActive(skill.level>1);
            var tag=UiChild(card,"Quality");
            tag.gameObject.SetActive(quality<3);
            if(quality<3)
            {
                SetSkillSprite(tag.GetComponent<Image>(),"Chrome/Tag"+quality);
                var label=UiChild(tag,"Label").GetComponent<Text>();
                label.text=quality==0?"普通":quality==1?"传说":"神话";
                if(quality>0) label.color=quality==1?Tint(0xFFF17F):White;
            }
            var title=UiChild(card,"Name").GetComponent<Text>();
            title.text=skill.name;
            if(skill.level>1) title.color=Tint(0xD3F24E);
            bool upgraded=!string.IsNullOrEmpty(skill.upgradeDescription);
            UiChild(card,"Description").gameObject.SetActive(!upgraded);
            UiChild(card,"Short description").gameObject.SetActive(upgraded);
            UiChild(card,upgraded?"Short description":"Description").GetComponent<Text>().text=FormatUiText(skill.description);
            UiChild(card,"Arrow").gameObject.SetActive(upgraded);
            var upgrade=UiChild(card,"Upgrade text");
            upgrade.gameObject.SetActive(upgraded);
            if(upgraded) upgrade.GetComponent<Text>().text=FormatUiText(skill.upgradeDescription);
            return card.GetComponent<CanvasGroup>();
        }

        private static IEnumerator RevealSkillChoices(RectTransform screen,List<CanvasGroup> cards,int quality)
        {
            const float duration=.15f;
            const float overshoot=1.70158f;
            yield return new WaitForSecondsRealtime(quality>=2?.8f:quality==1?.4f:.2f);
            foreach(var card in cards)
            {
                if(screen==null || !screen.gameObject.activeInHierarchy) yield break;
                card.alpha=1;
                for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
                {
                    if(screen==null || !screen.gameObject.activeInHierarchy) yield break;
                    float t=elapsed/duration-1;
                    float eased=1+(overshoot+1)*t*t*t+overshoot*t*t;
                    card.transform.localScale=Vector3.one*(.5f+.5f*eased);
                    yield return null;
                }
                if(screen==null || !screen.gameObject.activeInHierarchy) yield break;
                card.transform.localScale=Vector3.one;
                card.blocksRaycasts=true;
                yield return new WaitForSecondsRealtime(.01f);
            }
        }

        private void DrawSkillIcon(RectTransform frame,Skill skill,bool showLevel)
        {
            var image=MountPart(frame, "SkillSelection/DrawSkillIcon/image").GetComponent<Image>();
            SetSkillSprite(image,skill.iconAtlasId+"/"+skill.icon,false,true);
            if(skill.badgeAtlasId>0 && !string.IsNullOrEmpty(skill.badge))
            {
                var badge=MountPart(frame, "SkillSelection/DrawSkillIcon/badge").GetComponent<Image>();
                SetSkillSprite(badge,skill.badgeAtlasId+"/"+skill.badge,false,true);
            }
            if(showLevel && skill.level>1)
                SetSkillSprite(MountPart(frame, "SkillSelection/DrawSkillIcon/Panel").GetComponent<Image>(),"Chrome/Upgrade");
        }

        private void DrawLearnedSkills(RectTransform selection)
        {
            var shade=LoadView("LearnedSkills",selection);
            shade.name="UILearnedSkills";
            BindButton(shade.GetComponent<Image>(),()=>CloseSkillLayer(shade));
            var learned=MountSprite(shade, "SkillSelection/DrawLearnedSkills/Chrome Panel");
            learned.raycastTarget=true;
            MountSprite(shade, "SkillSelection/DrawLearnedSkills/Chrome PanelTop");
            InstantiateUi("Widgets/LearnedSkillTitle",shade);

            var ids=game.Run.skills;
            if(ids.Count==0)
            {
                MountText(learned.rectTransform, "SkillSelection/DrawLearnedSkills/尚未学习任何技能");
                return;
            }

            var rows=MountList(shade, "SkillSelection/DrawLearnedSkills/rows");
            int start=0;
            while(start<ids.Count)
            {
                var row=InstantiateUi("Widgets/LearnedSkillRow",rows);
                for(int i=0;i<row.childCount && start<ids.Count;i++,start++)
                {
                    var skill=game.FindSkill(ids[start]);
                    var frame=row.GetChild(i).GetComponent<Image>();
                    frame.gameObject.SetActive(true);
                    SetSkillSprite(frame,"Chrome/Frame"+Mathf.Clamp(skill.quality,0,3));
                    DrawSkillIcon(frame.rectTransform,skill,true);
                    DrawLearnedStars(frame.rectTransform,skill);
                    frame.GetComponent<Button>().onClick.AddListener(()=>DrawLearnedSkillInfo(shade,skill));
                }
            }
        }

        private void DrawLearnedStars(RectTransform frame,Skill skill)
        {
            int count=game.SkillGroupMaxLevel(skill.groupId);
            float width=Mathf.Min(.16f,.9f/count);
            float x=.5f-count*width*.5f;
            for(int i=0;i<count;i++,x+=width)
            {
                string sprite=i>=skill.level?"SkillStarEmpty":skill.compose?.Length>0?"SkillStarCompose":"SkillStarFull";
                var star=Panel(frame,Color.clear,(x,-.065f,x+width,.12f)).GetComponent<Image>();
                SetSkillSprite(star,"Chrome/"+sprite,false,true);
            }
        }

        private void DrawLearnedSkillInfo(RectTransform learned,Skill skill)
        {
            var shade=LoadView("SkillDetails",learned);
            BindButton(shade.GetComponent<Image>(),()=>CloseSkillLayer(shade));
            var detail=MountSprite(shade, "SkillSelection/DrawLearnedSkillInfo/Chrome Panel");
            detail.raycastTarget=true;
            var heading=InstantiateUi("Widgets/SkillDetailsTitle",shade);
            UiChild(heading,"Label").GetComponent<Text>().text=FormatUiText(skill.name);
            string description=string.IsNullOrEmpty(skill.fullDescription)?skill.description:skill.fullDescription;
            MountText(detail.rectTransform, "SkillSelection/DrawLearnedSkillInfo/Text", value: description??"");
        }

        private void CloseSkillLayer(RectTransform layer)
        {
            layer.gameObject.SetActive(false);
            Destroy(layer.gameObject);
        }

        private void SetSkillSprite(Image image,string key,bool sliced=false,bool preserveAspect=false) =>
            SetUiSprite(image,SkillUiPath+key,sliced,preserveAspect);

        private void SetUiSprite(Image image,string resourcePath,bool sliced=false,bool preserveAspect=false)
        {
            if(!uiSprites.TryGetValue(resourcePath,out var sprite))
            {
                sprite=Resources.Load<Sprite>(resourcePath);
                if(sprite!=null) uiSprites.Add(resourcePath,sprite);
            }
            if(sprite==null) return;
            image.sprite=sprite;
            image.color=Color.white;
            image.preserveAspect=preserveAspect;
            if(sliced && sprite.border.sqrMagnitude>0)
            {
                image.type=Image.Type.Sliced;
                image.pixelsPerUnitMultiplier=1080f/ReferenceWidth;
            }
        }
    }
}
