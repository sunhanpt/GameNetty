using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private enum ShopCategory { Equipment, Gems, Value, Gifts, Recharge }
        private static readonly string[] ShopLabels={"装备商店","宝石商店","超值精选","礼包商城","充值"};
        private static readonly string[] SlotLabels={"武器","护甲","戒指","戒指","饰品","饰品"};
        private static readonly string[] SlotIcons={"weapon","cloth","ring","ring","accessory","accessory"};
        private ShopCategory shopCategory;
        private int chapterPackIndex=-1;
        private Action campPageDialog;
        private bool sortEquipmentByType;

        private void DrawEquipmentShop()
        {
            var shop=LoadView("Shop",overlay);
            shop.name="Original shop layout";
            MountSprite(shop, "CampPages/DrawEquipmentShop/BG_LOGO").color=new Color(1,1,1,.09f);
            MountSprite(shop, "CampPages/DrawEquipmentShop/bg_frame_2", viewportScale: true);
            var currencies=InstantiateUi("Widgets/ShopCurrencies",shop);
            BindCampCurrency(UiChild(currencies,"Stamina"),$"{game.Profile.stamina}/30",()=>{campProfile=true;Refresh();});
            BindCampCurrency(UiChild(currencies,"Gems"),game.ItemCount(2).ToString(),()=>SwitchShop(ShopCategory.Recharge));
            BindCampCurrency(UiChild(currencies,"Gold"),game.Profile.gold.ToString(),()=>SwitchShop(ShopCategory.Equipment));

            var list=MountList(shop, "CampPages/DrawEquipmentShop/list");
            if(shopCategory==ShopCategory.Equipment) DrawShopChests(list);
            else
            {
                string banner=shopCategory==ShopCategory.Recharge?"banner_diamond":"banner_daily_gift";
                var heading=PageRow(list,356);
                MountSprite(heading, "CampPages/DrawEquipmentShop/PageArt", sprite: "Reference/CampPages/" + (banner));
                MountText(heading, "CampPages/DrawEquipmentShop/Title", value: ShopLabels[(int)shopCategory]);
                DrawShopCatalog(list);
            }

            var categories=Enumerable.Range(0,ShopLabels.Length).Where(i=>i!=(int)ShopCategory.Gems || game.CampGemShopUnlocked).ToArray();
            var tabs=InstantiateUi("Widgets/ShopTabs",shop);
            var layout=UiChild(tabs,"Tabs "+categories.Length);
            layout.gameObject.SetActive(true);
            for(int index=0;index<categories.Length;index++)
            {
                int i=categories[index];
                var tab=(ShopCategory)i;
                bool selected=tab==shopCategory;
                var slot=UiChild(layout,"Slot "+(index+1));
                UiChild(slot,selected?"Idle":"Selected").gameObject.SetActive(false);
                var button=UiChild(slot,selected?"Selected":"Idle");
                button.name="Shop tab "+i;
                button.GetComponent<Image>().pixelsPerUnitMultiplier=OutputWidth/portrait.sizeDelta.x;
                button.GetComponent<Button>().onClick.AddListener(()=>SwitchShop(tab));
                UiChild(button,"Label").GetComponent<Text>().text=ShopLabels[i];
            }
        }

        private void SwitchShop(ShopCategory category)
        {
            if(shopCategory==category) return;
            shopCategory=category;
            campPageDialog=null;
            Refresh();
        }

        private void DrawShopChests(RectTransform list)
        {
            DrawShopChapterPacks(list);
            var title=PageRow(list,96);
            MountSprite(title, "CampPages/DrawShopChests/img_title_bg", viewportScale: true);
            MountText(title, "CampPages/DrawShopChests/装备补给箱");
            var row=PageRow(list,718);
            var cards=InstantiateUi("Widgets/ShopSupplyChests",row);
            var chests=game.CampStock.chests;
            while(cards.childCount<chests.Length)
            {
                var last=(RectTransform)cards.GetChild(cards.childCount-1);
                var previous=(RectTransform)cards.GetChild(cards.childCount-2);
                var extra=Instantiate(last,cards,false);
                extra.name=last.name;
                extra.anchorMin+=last.anchorMin-previous.anchorMin;
                extra.anchorMax+=last.anchorMax-previous.anchorMax;
            }
            for(int i=0;i<cards.childCount;i++)
            {
                var card=(RectTransform)cards.GetChild(i);
                card.gameObject.SetActive(i<chests.Length);
                if(i<chests.Length) DrawShopChest(card,chests[i],i==0);
            }
        }

        private void DrawShopChapterPacks(RectTransform list)
        {
            var packs=game.CampStock.chapterPacks.Where(pack=>pack.chapter<=game.Profile.highest).ToArray();
            if(packs.Length==0) return;
            if(chapterPackIndex<0) chapterPackIndex=packs.Length-1;
            chapterPackIndex=Mathf.Clamp(chapterPackIndex,0,packs.Length-1);
            var pack=packs[chapterPackIndex];
            var heading=PageRow(list,89);
            MountSprite(heading, "CampPages/DrawShopChapterPacks/img_title_bg", viewportScale: true);
            MountText(heading, "CampPages/DrawShopChapterPacks/章节礼包");
            var row=PageRow(list,378);
            string tint=pack.type switch { 1=>"green",2=>"blue",3=>"purple",4=>"gold",_=>"red" };
            var card=MountSprite(row, "CampPages/DrawShopChapterPacks/card", sprite: "Reference/CampPages/" + ("chatpter_pack_banner_"+tint)).rectTransform;
            MountText(card, "CampPages/DrawShopChapterPacks/Title", value: $"第 {pack.chapter} 章礼包");
            for(int i=0;i<pack.rewards.Length;i++)
            {
                var reward=pack.rewards[i];
                var slot=InstantiateUi("Widgets/ShopChapterReward",card);
                float offset=(i-(pack.rewards.Length-1)*.5f)*.156f;
                slot.anchorMin+=Vector2.right*offset;
                slot.anchorMax+=Vector2.right*offset;
                if(reward.id==1 || reward.id==2) MountSprite(slot, "CampPages/DrawShopChapterPacks/CampArt", sprite: "Reference/CampMenu/" + (reward.id==1?"icon_coin":"icon_diamond"));
                else if(reward.id==24 || reward.id==62) MountSprite(slot, "CampPages/DrawShopChapterPacks/PageArt", sprite: "Reference/CampPages/" + (reward.id==24?"icon_item_equip_qianghuashi":"icon_key_diamond"));
                else MountText(slot, "CampPages/DrawShopChapterPacks/Title 2", value: game.ItemName(reward.id));
                MountText(slot, "CampPages/DrawShopChapterPacks/Title 3", value: "×"+reward.count);
            }
            if(chapterPackIndex>0) MountButton(card, "CampPages/DrawShopChapterPacks/btn_blue", action: ()=>{chapterPackIndex--;Refresh(true);}, enabled: true, viewportScale: true);
            if(chapterPackIndex+1<packs.Length) MountButton(card, "CampPages/DrawShopChapterPacks/btn_blue 2", action: ()=>{chapterPackIndex++;Refresh(true);}, enabled: true, viewportScale: true);
            var goods=string.Join("   ",pack.rewards.Select(item=>$"{game.ItemName(item.id)} ×{item.count}"));
            MountButton(card, "CampPages/DrawShopChapterPacks/btn_yellow", action: ()=>ShowPageDialog(()=>DrawPurchasePreview(goods)), enabled: true, viewportScale: true);
        }

        private void DrawShopChest(RectTransform card,EquipmentChest chest,bool silver)
        {
            string title=silver?"冒险者补给箱":"英雄补给箱";
            MountText(card, "CampPages/DrawShopChest/Title", value: title);
            MountSprite(card, "CampPages/DrawShopChest/PageArt", sprite: "Reference/CampPages/" + (silver?"tip_frame_blue":"tip_frame_purple"), sliced: true, viewportScale: true);
            MountText(card, "CampPages/DrawShopChest/Title 2", value: silver?"包含<color=#D8D7EF>普通</color>或<color=#D3F24E>优秀</color>装备":
                "包含<color=#D3F24E>优秀</color>、<color=#91EEF6>稀有</color>或<color=#D672FE>史诗</color>装备");
            MountSprite(card, "CampPages/DrawShopChest/box_shadow");
            MountSprite(card, "CampPages/DrawShopChest/PageArt 2", sprite: "Reference/CampPages/" + (silver?"box_silver":"box_gold"));
            var info=MountSprite(card, "CampPages/DrawShopChest/icon_question_mark");
            BindButton(info,()=>ShowPageDialog(()=>DrawChestRates(title,chest)));

            var offer=game.ChestOffer(chest);
            bool available=offer.draws>0 && (offer.cost==0 || game.ItemCount(offer.currency)>=offer.cost);
            var button=MountSprite(card, "CampPages/DrawShopChest/button", sprite: "Reference/CampPages/" + (offer.currency==0?"btn_green":"btn_yellow"), sliced: true, viewportScale: true);
            button.name="Open chest "+chest.id;
            BindButton(button,()=>OpenShopChest(chest,offer),available);
            if(offer.currency==0) MountText(button.rectTransform, "CampPages/DrawShopChest/免费");
            else
            {
                string icon=offer.currency==2?"icon_diamond":silver?"icon_key_silver":"icon_key_gold";
                var plate=button.rectTransform;
                if(offer.currency==2) MountSprite(plate, "CampPages/DrawShopChest/CampArt", sprite: "Reference/CampMenu/" + (icon));
                else MountSprite(plate, "CampPages/DrawShopChest/PageArt 3", sprite: "Reference/CampPages/" + (icon));
                MountText(plate, "CampPages/DrawShopChest/Title 3", value: $"{offer.cost}   开启 ×{offer.draws}");
            }
        }

        private void OpenShopChest(EquipmentChest chest,(int currency,int cost,int draws) offer)
        {
            Change(new AdventureCommand(JourneyAction.OpenShopChest,chest.id,currency:offer.currency,cost:offer.cost,draws:offer.draws),
                true,result=>ShowPageDialog(()=>DrawChestRewards(result.rewards,chest)));
        }

        private void DrawChestRates(string name,EquipmentChest chest)
        {
            var panel=PageDialog("ChestRates",name);
            var list=MountList(panel, "CampPages/DrawChestRates/list");
            LabelRow(list,"常规掉落概率");
            foreach(var rate in chest.rates)
            {
                string label=rate.id switch { 1=>"普通装备",2=>"优秀装备",3=>"稀有装备",4=>"史诗装备",5=>"传说装备",_=>"强化材料" };
                LabelRow(list,$"{label}   {rate.count/100f:0.##}%");
            }
        }

        private void DrawShopCatalog(RectTransform list)
        {
            if(shopCategory==ShopCategory.Recharge)
            {
                var packs=game.CampStock.diamonds;
                for(int offset=0;offset<packs.Length;offset+=3)
                {
                    var row=InstantiateUi("Widgets/ShopDiamondPacks",list);
                    for(int j=0;j<row.childCount;j++)
                    {
                        var card=(RectTransform)row.GetChild(j);
                        bool available=offset+j<packs.Length;
                        card.gameObject.SetActive(available);
                        if(!available) continue;
                        int count=packs[offset+j];
                        MountSprite(card, "CampPages/DrawShopCatalog/PageArt", sprite: "Reference/CampPages/" + ("diamond_"+(offset+j+1)));
                        MountText(card, "CampPages/DrawShopCatalog/Title", value: $"{count} 钻石");
                        MountButton(card, "CampPages/DrawShopCatalog/btn_yellow", action: ()=>ShowPageDialog(()=>DrawPurchasePreview($"{count} 钻石")), enabled: true, viewportScale: true);
                    }
                }
                return;
            }
            if(shopCategory==ShopCategory.Gems)
            {
                var row=PageRow(list,300);
                MountSprite(row, "CampPages/DrawShopCatalog/equip_pack_banner", viewportScale: true);
                MountSprite(row, "CampPages/DrawShopCatalog/icon_gem");
                MountText(row, "CampPages/DrawShopCatalog/宝石补给");
                MountButton(row, "CampPages/DrawShopCatalog/btn_yellow 2", action: ()=>ShowPageDialog(()=>DrawPurchasePreview("宝石补给")), enabled: true, viewportScale: true);
                return;
            }
            foreach(var group in game.CampStock.gifts.GroupBy(gift=>gift.type))
            {
                if((shopCategory==ShopCategory.Value)!=(group.Key>3)) continue;
                var title=PageRow(list,96);
                MountSprite(title, "CampPages/DrawShopCatalog/img_title_bg", viewportScale: true);
                string titleText=group.Key switch { 1=>"日礼包",2=>"周礼包",3=>"月礼包",_=>"超值礼包" };
                MountText(title, "CampPages/DrawShopCatalog/Title 2", value: titleText);
                foreach(var gift in group)
                {
                    var row=PageRow(list,192);
                    MountSprite(row, "CampPages/DrawShopCatalog/super_value_item_bg", viewportScale: true);
                    string goods=string.Join("   ",gift.rewards.Select(item=>$"{game.ItemName(item.id)} ×{item.count}"));
                    MountText(row, "CampPages/DrawShopCatalog/Title 3", value: goods);
                    MountButton(row, "CampPages/DrawShopCatalog/btn_yellow 3", action: ()=>ShowPageDialog(()=>DrawPurchasePreview(goods)), enabled: true, viewportScale: true);
                }
            }
        }

        private void DrawPurchasePreview(string label)
        {
            var panel=PageDialog("PurchasePreview","商品预览");
            MountText(panel, "CampPages/DrawPurchasePreview/Text", value: label);
            MountText(panel, "CampPages/DrawPurchasePreview/当前离线，无法完成购买");
        }

        private void DrawEquipmentPage()
        {
            var page=LoadView("Equipment",overlay);
            page.name="Original equipment layout";
            MountSprite(page, "CampPages/DrawEquipmentPage/img_bg_bottom", viewportScale: true);
            MountSprite(page, "CampPages/DrawEquipmentPage/img_bg_top");
            MountSprite(page, "CampPages/DrawEquipmentPage/box_shadow").color=new Color(1,1,1,.65f);
            var hero=MountPart(page, "CampPages/DrawEquipmentPage/Equipment hero");
            var preview=hero.GetComponent<ActorAnimationGraphic>();
            if(preview.Load("Hero")) heroPreview=preview;

            var slots=InstantiateUi("Widgets/EquipmentSlots",page);
            for(int i=0;i<SlotLabels.Length;i++)
            {
                int slot=i;
                var item=game.Equipment(game.Profile.wornEquipment[i]);
                var card=DrawGearTile(UiChild(slots,"Slot "+(i+1)),item,"equip_type_icon_"+SlotIcons[i],()=>OpenEquipmentSlot(slot));
                card.name="Equipment slot "+(i+1);
                var forgeLevel=UiChild(card,"Forge level").GetComponent<Text>();
                forgeLevel.gameObject.SetActive(item!=null && game.Profile.forgeLevel>0);
                forgeLevel.text="+"+game.Profile.forgeLevel;
            }

            var stats=game.Player();
            var attributes=InstantiateUi("Widgets/EquipmentAttributes",page);
            UiChild(attributes,"Heart/Value").GetComponent<Text>().text=stats.Stat("HPMax").ToString();
            UiChild(attributes,"Attack/Value").GetComponent<Text>().text=stats.Stat("Attack").ToString();
            UiChild(attributes,"Defence/Value").GetComponent<Text>().text=stats.Stat("Defence").ToString();

            MountSprite(page, "CampPages/DrawEquipmentPage/icon_bg_frame", viewportScale: true);
            var shortcuts=InstantiateUi("Widgets/EquipmentShortcuts",page);
            UiChild(shortcuts,"Pets/Button").GetComponent<Button>().onClick.AddListener(()=>ShowPageDialog(()=>DrawGearUtility(0)));
            UiChild(shortcuts,"Mounts/Button").GetComponent<Button>().onClick.AddListener(()=>ShowPageDialog(()=>DrawMountCatalog()));
            UiChild(shortcuts,"Artifact/Button").GetComponent<Button>().onClick.AddListener(()=>ShowPageDialog(()=>DrawGearUtility(2)));

            MountSprite(page, "CampPages/DrawEquipmentPage/equip_title_bg", viewportScale: true);
            MountText(page, "CampPages/DrawEquipmentPage/装备背包");
            var sort=MountSprite(page, "CampPages/DrawEquipmentPage/btn_blue", viewportScale: true);
            sort.name="Sort equipment";
            BindButton(sort,()=>{sortEquipmentByType=!sortEquipmentByType;Refresh();});
            MountText(sort.rectTransform, "CampPages/DrawEquipmentPage/排序");
            var materials=MountSprite(page, "CampPages/DrawEquipmentPage/btn_yellow", viewportScale: true);
            BindButton(materials,()=>ShowPageDialog(DrawCampItems));
            MountText(materials.rectTransform, "CampPages/DrawEquipmentPage/道具");

            var list=MountList(page, "CampPages/DrawEquipmentPage/list");
            DrawEquipmentInventory(list,game.BackpackEquipment.ToArray(),null);
        }

        private RectTransform DrawGearTile(RectTransform parent,EquipmentItem item,string emptyIcon,Action clicked)
        {
            var tile=InstantiateUi("Widgets/EquipmentTile",parent);
            var frame=UiChild(tile,"Frame").GetComponent<Image>();
            if(item!=null) SetUiSprite(frame,"Reference/CampPages/item_frame_"+Mathf.Clamp(item.quality,1,7));
            frame.raycastTarget=clicked!=null;
            var button=frame.GetComponent<Button>();
            button.enabled=clicked!=null;
            if(clicked!=null) button.onClick.AddListener(()=>clicked());
            var empty=UiChild(tile,"Empty icon").GetComponent<Image>();
            empty.gameObject.SetActive(item==null && emptyIcon!=null);
            if(empty.gameObject.activeSelf) SetUiSprite(empty,"Reference/CampPages/"+emptyIcon);
            var itemImage=UiChild(tile,"Item icon").GetComponent<Image>();
            itemImage.gameObject.SetActive(item!=null);
            if(item!=null) SetUiSprite(itemImage,"Reference/CampPages/"+item.icon);
            return tile;
        }

        private void DrawEquipmentInventory(RectTransform list,EquipmentItem[] items,int? slot)
        {
            var ordered=(sortEquipmentByType?items.OrderBy(item=>item.type).ThenByDescending(item=>item.quality):
                items.OrderByDescending(item=>item.quality).ThenBy(item=>item.type)).ThenBy(item=>item.id).ToArray();
            int start=0;
            do
            {
                var row=InstantiateUi("Widgets/EquipmentInventoryRow",list);
                for(int index=0;index<row.childCount;index++)
                {
                    var item=start+index<ordered.Length?ordered[start+index]:null;
                    var tile=DrawGearTile((RectTransform)row.GetChild(index),item,null,
                        item==null?null:()=>ShowPageDialog(()=>DrawGearDetails(item,slot,false)));
                    if(item==null) continue;
                    tile.name="Backpack equipment "+item.id;
                    int amount=game.BackpackCount(item.id);
                    var quantity=UiChild(tile,"Quantity").GetComponent<Text>();
                    quantity.gameObject.SetActive(amount>1);
                    quantity.text="×"+amount;
                }
                start+=row.childCount;
            }
            while(start<ordered.Length);
        }

        private void OpenEquipmentSlot(int slot)
        {
            var item=game.Equipment(game.Profile.wornEquipment[slot]);
            if(item!=null) ShowPageDialog(()=>DrawGearDetails(item,slot,true));
            else ShowPageDialog(()=>DrawSlotCandidates(slot));
        }

        private void DrawSlotCandidates(int slot)
        {
            var panel=PageDialog("EquipmentSlot",SlotLabels[slot]);
            var candidates=game.BackpackEquipment.Where(item=>Adventure.FitsEquipmentSlot(slot,item)).ToArray();
            if(candidates.Length==0)
            {
                MountText(panel, "CampPages/DrawSlotCandidates/背包中暂无可穿戴装备");
                MountButton(panel, "CampPages/DrawSlotCandidates/btn_yellow", action: OpenCampShop, enabled: true, viewportScale: true);
            }
            else DrawEquipmentInventory(MountList(panel, "CampPages/DrawSlotCandidates/List"),candidates,slot);
        }

        private void DrawGearDetails(EquipmentItem item,int? selectedSlot,bool equipped)
        {
            var panel=PageDialog("EquipmentDetails",item.name);
            var tile=MountPart(panel, "CampPages/DrawGearDetails/Equipment details icon");
            DrawGearTile(tile,item,null,null);
            var attributes=new Attributes();
            attributes.Add(item.attributes);
            string description=$"生命 +{attributes.Stat("HPMax")}   攻击 +{attributes.Stat("Attack")}   防御 +{attributes.Stat("Defence")}";
            MountText(panel, "CampPages/DrawGearDetails/Text", value: description);
            if(equipped)
            {
                MountButton(panel, "CampPages/DrawGearDetails/btn_red", action: ()=>ApplyEquipment(selectedSlot.Value,0), enabled: true, viewportScale: true).name="Unequip gear";
                MountButton(panel, "CampPages/DrawGearDetails/btn_green", action: ()=>Change(JourneyAction.Forge), enabled: game.CanForge, label: $"强化   {game.ForgePrice} 金币", viewportScale: true);
                MountText(panel, "CampPages/DrawGearDetails/Text 2", value: $"强化石 {game.ForgeMaterialCount}/2");
            }
            else
            {
                var slots=Enumerable.Range(0,SlotLabels.Length).Where(i=>Adventure.FitsEquipmentSlot(i,item)).ToArray();
                int slot=selectedSlot??slots.Where(i=>game.Profile.wornEquipment[i]==0).DefaultIfEmpty(slots[0]).First();
                MountButton(panel, "CampPages/DrawGearDetails/btn_green 2", action: ()=>ApplyEquipment(slot,item.id), enabled: true, label: game.Profile.wornEquipment[slot]==0?"穿戴":"替换", viewportScale: true).name="Equip gear";
            }
        }

        private void ApplyEquipment(int slot,int id)
        {
            Change(JourneyAction.ChangeEquipment,id,slot,onSuccess:_=>ShowPageDialog(null));
        }

        private void DrawCampItems()
        {
            var panel=PageDialog("CampItems","道具");
            var list=MountList(panel, "CampPages/DrawCampItems/list");
            foreach(var item in game.Profile.inventory.Where(item=>game.Equipment(item.id)==null).OrderBy(item=>item.id))
                LabelRow(list,$"{game.ItemName(item.id)}   ×{item.count}");
            if(list.childCount==0) LabelRow(list,"暂无道具");
        }

        private void DrawGearUtility(int kind)
        {
            var panel=PageDialog("GearUtility",kind==0?"宠物":"神器");
            var list=MountList(panel, "CampPages/DrawGearUtility/list");
            if(kind==0)
            {
                foreach(var pet in game.Pets)
                {
                    bool active=game.Profile.petId==pet.id;
                    ListButton(list,pet.name+(active?" · 出战中":""),()=>Change(JourneyAction.EquipPet,pet.id,preserveScroll:true),
                        game.Profile.cleared.Contains(1)&&!active&&(game.ActivePet!=null||game.Profile.gold>=Adventure.PetRecruitPrice));
                }
                return;
            }
            foreach(var relic in game.Relics.Where(item=>item.kind==kind))
            {
                bool equip=game.CanEquip(relic), buy=game.CanBuyRelic(relic);
                string state=game.IsEquipped(relic)?"已装备":equip?"可装备":buy?$"{game.RelicPrice(relic)} 金币":"未解锁";
                ListButton(list,$"{relic.name} · {state}",()=>Change(equip?JourneyAction.EquipRelic:JourneyAction.BuyRelic,relic.id,kind,preserveScroll:true),equip||buy);
            }
        }

        private void ShowPageDialog(Action draw)
        {
            campPageDialog=draw;
            Refresh(true);
        }

        private RectTransform PageDialog(string prefab,string title)
        {
            var panel=Modal(prefab);
            MountSprite(panel, "CampPages/PageDialog/title_yellow", viewportScale: true);
            MountText(panel, "CampPages/PageDialog/Title", value: title);
            MountButton(panel, "CampPages/PageDialog/btn_blue", action: ()=>ShowPageDialog(null), enabled: true, viewportScale: true);
            return panel;
        }

        private RectTransform PageRow(RectTransform parent,int height)
        {
            var row=MountPart(parent, "CampPages/PageRow/Content row");
            row.GetComponent<LayoutElement>().preferredHeight=height*ReferenceWidth/(float)OutputWidth;
            return row;
        }

    }
}
