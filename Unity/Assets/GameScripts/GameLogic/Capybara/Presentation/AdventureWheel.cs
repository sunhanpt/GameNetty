using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private void DrawWheel()
        {
            var wheel=game.Wheel;
            var shade=LoadView("FortuneWheel",overlay);
            var design=UiChild(shade,"Design");
            FitWheel(design);
            var disk=UiChild(design,"Disk");
            disk.localRotation=Quaternion.Euler(0,0,wheel.offsetAngle);
            int total=wheel.rewards.Sum(r=>r.showWeight);
            float leading=90;
            foreach(var reward in wheel.rewards)
            {
                float span=reward.showWeight*360f/total;
                var sector=InstantiateUi("Widgets/WheelSector",disk).GetComponent<Image>();
                SetUiSprite(sector,"Reference/FortuneWheel/wheel_area"+Mathf.Clamp(reward.color,1,4));
                sector.fillAmount=span/360f;
                sector.rectTransform.localRotation=Quaternion.Euler(0,0,leading-90);
                leading-=span;
            }
            for(int i=0;i<wheel.rewards.Length;i++)
            {
                var reward=wheel.rewards[i];
                float degrees=wheel.SectorCenter(i);
                float boundary=degrees+reward.showWeight*180f/total;
                PlaceWheelPart(disk,"Widgets/WheelLine",boundary);
                var award=PlaceWheelPart(disk,"Widgets/WheelRewardIcon",degrees).GetComponent<Image>();
                SetUiSprite(award,"Reference/FortuneWheel/"+reward.icon,false,true);
                var label=PlaceWheelPart(disk,"Widgets/WheelRewardLabel",degrees);
                bool large=reward.textStyle==2;
                UiChild(label,"Normal").gameObject.SetActive(!large);
                UiChild(label,"Large").gameObject.SetActive(large);
                var caption=UiChild(label,large?"Large":"Normal").GetComponent<Text>();
                string value=reward.kind==2?"技能":reward.target=="HPMax"?reward.label.Replace("生命上限","生命"):reward.label;
                caption.text=value;
                if(ColorUtility.TryParseHtmlString(reward.textColor,out var configured)) caption.color=configured;
            }
            UiChild(disk,"Nails").SetAsLastSibling();
            var pointer=UiChild(design,"Pointer");
            var start=UiChild(design,"Start").GetComponent<Button>();
            var startLabel=UiChild(design,"Start label").GetComponent<Text>();
            var loading=UiChild(design,"Progress").GetComponent<Text>();
            start.onClick.AddListener(()=>StartCoroutine(SpinWheel(shade,disk,pointer,startLabel,loading,wheel)));
        }

        private void FitWheel(RectTransform design)
        {
            var size=design.sizeDelta;
            float scale=Mathf.Min(portrait.rect.width/size.x,portrait.rect.height/size.y);
            design.localScale=Vector3.one*scale;
        }

        private static RectTransform PlaceWheelPart(RectTransform disk,string path,float degrees)
        {
            var rect=InstantiateUi(path,disk);
            float radians=degrees*Mathf.Deg2Rad;
            rect.anchoredPosition=new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*rect.anchoredPosition.magnitude;
            rect.localRotation=Quaternion.Euler(0,0,degrees-90);
            return rect;
        }

        private IEnumerator SpinWheel(RectTransform shade,RectTransform disk,RectTransform pointer,Text startLabel,Text loading,
            WheelDefinition wheel)
        {
            if(busy || syncRequired || !game.HasWheel) yield break;
            busy=true;
            var startButton=shade.GetComponentInChildren<Button>();
            if(startButton!=null) startButton.interactable=false;
            int selected=-1;
            yield return RunCommand(new AdventureCommand(JourneyAction.SpinWheel),result=>selected=result?.wheelIndex??-1);
            if(selected<0)
            {
                busy=false;
                world.Refresh(game);
                Refresh();
                yield break;
            }
            startLabel.text="旋转中";
            startLabel.color=Tint(0x743C15);
            float elapsed=0;
            float start=wheel.offsetAngle;
            float end=wheel.StopAngle(selected);
            const float spinSeconds=7.2f;
            while(elapsed<spinSeconds)
            {
                elapsed+=Time.unscaledDeltaTime;
                float progress=Mathf.Clamp01(elapsed/spinSeconds);
                float angle=Mathf.Lerp(start,end,1-Mathf.Pow(1-progress,3.3f));
                disk.localRotation=Quaternion.Euler(0,0,angle);
                pointer.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(Mathf.Repeat(angle,45)/45*Mathf.PI)*20*(1-progress));
                loading.text=new string('·',1+(int)(elapsed*5)%3);
                yield return null;
            }
            disk.localRotation=Quaternion.Euler(0,0,end);
            pointer.localRotation=Quaternion.identity;
            if(victory!=null) audioSource.PlayOneShot(victory);
            DrawWheelResult(shade,wheel.rewards[selected]);
            yield return new WaitForSecondsRealtime(1.05f);
            yield return ResolveForJournal(new AdventureCommand(JourneyAction.ResolveWheel));
            busy=false;
            world.Refresh(game);
            Refresh();
        }

        private void DrawWheelResult(RectTransform shade,WheelReward reward)
        {
            var panel=LoadView("FortuneResult",shade);
            var design=UiChild(panel,"Design");
            FitWheel(design);
            bool setback=reward.value<0;
            UiChild(design,"Fortune ribbon").gameObject.SetActive(!setback);
            UiChild(design,"Setback ribbon").gameObject.SetActive(setback);
            SetUiSprite(UiChild(design,"Reward icon").GetComponent<Image>(),"Reference/FortuneWheel/"+reward.icon,false,true);
            var result=UiChild(design,"Result").GetComponent<Text>();
            result.text=reward.result;
            if(setback) result.color=Tint(0xFF614E);
        }
    }
}
