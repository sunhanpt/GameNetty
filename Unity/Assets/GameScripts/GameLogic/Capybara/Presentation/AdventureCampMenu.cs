using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private enum CampPage { Shop, Equipment, Adventure, Talent, Chests }

        private const string CampArtPath="Reference/CampMenu/";
        private static readonly (string label,string icon)[] CampTabs=
        {
            ("商店","tab_icon_shop"),
            ("装备","tab_icon_equip"),
            ("冒险","tab_icon_battle"),
            ("天赋","tab_icon_talent"),
            ("宝箱","tab_icon_chest")
        };

        private CampPage campPage=CampPage.Adventure;
        private RectTransform campMenu;
        private RectTransform campUnlockNotice;
        private bool campMore, campRewards, campProfile;

        private void DrawCampHeader()
        {
            var header=MountPart(overlay, "CampMenu/DrawCampHeader/Camp resources");
            MountSprite(header, "CampMenu/DrawCampHeader/bg_frame", viewportScale: true);
            MountSprite(header, "CampMenu/DrawCampHeader/info_bg", viewportScale: true);
            MountSprite(header, "CampMenu/DrawCampHeader/name_bg", viewportScale: true);
            var avatar=MountSprite(header, "CampMenu/DrawCampHeader/frame_Capybara");
            MountSprite(header, "CampMenu/DrawCampHeader/Frame_Normal");
            BindButton(avatar,()=>{campProfile=true;Refresh();});
            var name=MountText(header, "CampMenu/DrawCampHeader/冒险家");
            if(accountFont!=null) name.font=accountFont;
            string rank=game.CurrentTalent?.name ?? game.TalentStages.LastOrDefault()?.name ?? "学徒-1阶";
            MountText(header, "CampMenu/DrawCampHeader/CampLabel", value: rank);
            MountSprite(header, "CampMenu/DrawCampHeader/icon_combat");
            MountText(header, "CampMenu/DrawCampHeader/—");
            var currencies=InstantiateUi("Widgets/CampCurrencies",header);
            BindCampCurrency(UiChild(currencies,"Stamina"),$"{game.Profile.stamina}/30",()=>{campProfile=true;Refresh();});
            BindCampCurrency(UiChild(currencies,"Gems"),game.ItemCount(2).ToString(),OpenCampShop);
            BindCampCurrency(UiChild(currencies,"Gold"),game.Profile.gold.ToString(),OpenCampShop);
        }

        private void DrawCampProfile()
        {
            var panel=Modal("PlayerProfile");
            var stats=game.Player();
            MountText(panel, "CampMenu/DrawCampProfile/冒险家");
            MountText(panel, "CampMenu/DrawCampProfile/Text", value: $"生命 {stats.Stat("HPMax")}   攻击 {stats.Stat("Attack")}   防御 {stats.Stat("Defence")}");
            MountButton(panel, "CampMenu/DrawCampProfile/休整", action: ()=>{campProfile=false;Change(JourneyAction.Rest);}, enabled: game.Profile.stamina<30);
            MountButton(panel, "CampMenu/DrawCampProfile/关闭", action: ()=>{campProfile=false;Refresh();}, enabled: true);
            AddSessionButton(panel,"返回登录",LeaveAdventure,(.3f,.08f,.7f,.2f));
        }

        private void BindCampCurrency(RectTransform currency,string value,System.Action action)
        {
            var plate=UiChild(currency,"Plate");
            plate.GetComponent<Image>().pixelsPerUnitMultiplier=OutputWidth/portrait.sizeDelta.x;
            plate.GetComponent<Button>().onClick.AddListener(()=>action());
            UiChild(currency,"Currency icon").GetComponent<Button>().onClick.AddListener(()=>action());
            UiChild(currency,"Value").GetComponent<Text>().text=value;
        }

        private void OpenCampShop()=>SelectCampPage(CampPage.Shop,CampLockMessage(CampPage.Shop));

        private void DrawCampAdventure(Chapter chapter)
        {
            MountText(overlay, "CampMenu/DrawCampAdventure/CampLabel", value: $"{chapter.id}.{chapter.name}");
            MountText(overlay, "CampMenu/DrawCampAdventure/CampLabel 2", value: $"最高生存天数:<color=#ffec43>{game.ChapterBestStage(chapter)}</color>天");

            var menu=MountSprite(overlay, "CampMenu/DrawCampAdventure/menu", sprite: "Reference/CampMenu/" + (campMore?"uimain_icon_more":"uimain_icon_more2"));
            menu.name="Main shortcuts";
            BindButton(menu,()=>{campMore=!campMore;Refresh();});
            if(campMore)
            {
                var shortcuts=InstantiateUi("Widgets/CampShortcuts",overlay);
                BindCampShortcut(UiChild(shortcuts,"Open chapter picker"),false);
                BindCampShortcut(UiChild(shortcuts,"Open challenge picker"),true);
            }

            bool cleared=game.Profile.cleared.Contains(chapter.id);
            var reward=MountPart(overlay, "CampMenu/DrawCampAdventure/reward");
            reward.name="Chapter rewards";
            BindButton(reward.GetComponent<Image>(),()=>{campRewards=true;campMore=false;Refresh();});
            MountSprite(reward, "CampMenu/DrawCampAdventure/img_hangup_bg", viewportScale: true);
            MountSprite(reward, "CampMenu/DrawCampAdventure/uimain_chapter_box");
            MountText(overlay, "CampMenu/DrawCampAdventure/CampLabel 3", value: cleared?"已通关":"通关章节");

            var start=MountSprite(overlay, "CampMenu/DrawCampAdventure/uimain_btn_fight", viewportScale: true);
            start.name="Start adventure";
            BindButton(start,()=>Change(JourneyAction.Start,chapter.id),game.CanStartChapter(chapter));
            MountText(start.rectTransform, "CampMenu/DrawCampAdventure/开始");
            MountSprite(overlay, "CampMenu/DrawCampAdventure/icon_energy");
            MountText(overlay, "CampMenu/DrawCampAdventure/CampLabel 4", value: $"x{chapter.stamina}");
        }

        private void BindCampShortcut(RectTransform button,bool challenges)
        {
            button.GetComponent<Image>().pixelsPerUnitMultiplier=OutputWidth/portrait.sizeDelta.x;
            button.GetComponent<Button>().onClick.AddListener(()=>
            {
                campMore=false;
                campPicker=true;
                campChallenges=challenges;
                previewChapterId=game.CampChapter.id;
                chapterPickerDetail=ChapterPickerDetail.None;
                Refresh();
            });
        }

        private void DrawCampChapterRewards(Chapter chapter)
        {
            var page=LoadView("ChapterRewards",overlay);
            page.GetComponent<Image>().raycastTarget=true;
            MountSprite(page, "CampMenu/DrawCampChapterRewards/backCommonImg", viewportScale: true);
            MountSprite(page, "CampMenu/DrawCampChapterRewards/title_yellow", viewportScale: true);
            MountText(page, "CampMenu/DrawCampChapterRewards/通关章节");
            MountSprite(page, "CampMenu/DrawCampChapterRewards/chapter_flag");
            MountText(page, "CampMenu/DrawCampChapterRewards/CampLabel", value: $"{chapter.id}.{chapter.name}");
            MountText(page, "CampMenu/DrawCampChapterRewards/CampLabel 2", value: $"最高生存天数 {game.ChapterBestStage(chapter)}/{chapter.totalStage}");
            MountSprite(page, "CampMenu/DrawCampChapterRewards/chapter_reward_bg", viewportScale: true);
            var rewards=MountList(page, "CampMenu/DrawCampChapterRewards/rewards");
            foreach(int stage in chapter.rewardStage)
            {
                var items=game.ChapterRewards(chapter,stage);
                var summary=string.Join("  ",items.Select(item=>$"{game.ItemName(item.id)} ×{item.count}"));
                if(summary.Length>0)
                    LabelRow(rewards,(stage==chapter.totalStage?"通关奖励":$"第 {stage} 天")+"\n"+summary);
            }
            MountSprite(page, "CampMenu/DrawCampChapterRewards/bottom_bg_frame", viewportScale: true);
            var back=MountSprite(page, "CampMenu/DrawCampChapterRewards/btn_gray", viewportScale: true);
            back.name="Close chapter rewards";
            BindButton(back,()=>{campRewards=false;Refresh();});
            MountSprite(page, "CampMenu/DrawCampChapterRewards/btn_back");
        }

        private void ResizeCampMenu()
        {
            if(campMenu==null) return;
            var menuSize=UiTemplate("Widgets/CampNavigation").sizeDelta;
            menuSize.y*=portrait.sizeDelta.x/OutputWidth;
            campMenu.sizeDelta=menuSize;
        }

        private void DrawCampMenu()
        {
            campMenu=InstantiateUi("Widgets/CampNavigation",overlay);
            ResizeCampMenu();

            var chat=UiChild(campMenu,"Chat");
            chat.gameObject.SetActive(campPage==CampPage.Adventure);
            UiChild(chat,"Background").GetComponent<Image>().pixelsPerUnitMultiplier=OutputWidth/portrait.sizeDelta.x;

            var bar=UiChild(campMenu,"Selections/"+campPage);
            bar.gameObject.SetActive(true);
            for(int i=0;i<CampTabs.Length;i++)
            {
                var page=(CampPage)i;
                var (label,icon)=CampTabs[i];
                bool selected=page==campPage;
                string lockMessage=CampLockMessage(page);
                var tab=InstantiateUi(selected?"Widgets/CampTabActive":"Widgets/CampTab",UiChild(bar,page.ToString()));
                tab.name=label;
                var background=UiChild(tab,"Background");
                background.GetComponent<Image>().pixelsPerUnitMultiplier=OutputWidth/portrait.sizeDelta.x;
                background.GetComponent<Button>().onClick.AddListener(()=>SelectCampPage(page,lockMessage));

                var iconImage=UiChild(tab,"Icon").GetComponent<Image>();
                iconImage.gameObject.SetActive(lockMessage==null);
                SetUiSprite(iconImage,CampArtPath+icon);
                UiChild(tab,"Lock").gameObject.SetActive(lockMessage!=null);
                UiChild(tab,"Badge").gameObject.SetActive(lockMessage==null && CampBadgeVisible(page));
                if(selected)
                {
                    UiChild(tab,"Label").GetComponent<Text>().text=label;
                    UiChild(tab,"Left arrow").gameObject.SetActive(page!=CampPage.Shop);
                    UiChild(tab,"Right arrow").gameObject.SetActive(page!=CampPage.Chests);
                }
            }
        }

        private string CampLockMessage(CampPage page)
        {
            switch(page)
            {
                case CampPage.Shop:
                    return game.CampShopUnlocked?null:"到达第 1 章第 39 天后解锁";
                case CampPage.Talent:
                    return game.CampTalentUnlocked?null:"到达第 1 章第 1 天后解锁";
                case CampPage.Chests:
                    return game.CampChestsUnlocked?null:"到达第 3 章第 30 天后解锁";
                default:
                    return null;
            }
        }

        private bool CampBadgeVisible(CampPage page)
        {
            switch(page)
            {
                case CampPage.Shop: return game.CampStock?.chests.Any(chest=>game.ChestOffer(chest).currency==0)==true;
                case CampPage.Equipment: return game.CanForge || game.BackpackEquipment.Any();
                case CampPage.Talent: return game.CanTrainTalent;
                case CampPage.Chests: return game.HasCampChests;
                default: return false;
            }
        }

        private void SelectCampPage(CampPage page,string lockMessage)
        {
            if(lockMessage!=null)
            {
                if(campUnlockNotice!=null) Destroy(campUnlockNotice.gameObject);
                campUnlockNotice=LoadView("CampUnlockNotice",overlay);
                MountText(campUnlockNotice, "CampMenu/SelectCampPage/Text", value: lockMessage);
                Destroy(campUnlockNotice.gameObject,2f);
                return;
            }
            if(campPage==page) return;
            campMore=false;
            campPageDialog=null;
            if(page==CampPage.Shop)
            {
                shopCategory=ShopCategory.Equipment;
                chapterPackIndex=-1;
            }
            campPage=page;
            Refresh();
        }

        private void DrawCampPage()
        {
            if(campPage==CampPage.Shop) { DrawEquipmentShop(); return; }
            if(campPage==CampPage.Equipment) { DrawEquipmentPage(); return; }
            if(campPage==CampPage.Talent) { DrawTalentPage(); return; }
            var panel=LoadView("ChestInventory",overlay);
            MountText(panel, "CampMenu/DrawCampPage/Title", value: CampTabs[(int)campPage].label);
            var list=MountList(panel, "CampMenu/DrawCampPage/list");
            switch(campPage)
            {
                case CampPage.Chests:
                    if(!game.HasCampChests) LabelRow(list,"暂无宝箱");
                    foreach(var item in game.Profile.inventory.Where(item=>Adventure.IsCampChest(item.id) && item.count>0))
                        LabelRow(list,$"{game.ItemName(item.id)}   ×{item.count}");
                    break;
            }
        }
    }
}
