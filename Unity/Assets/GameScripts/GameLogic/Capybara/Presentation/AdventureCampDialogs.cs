using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private static readonly Color CampDialogBase = Tint(0x202535);
        private static readonly Color CampDialogCard = Tint(0x30394D);
        private static readonly Color CampDialogMuted = Tint(0xBBC5DA);
        private static readonly Color MountAccent = Tint(0x96D7D2);
        private static readonly Color RewardAccent = Tint(0xF6D694);

        private RectTransform CampCollectionDialog(string heading, string name, System.Action close = null)
        {
            close ??= () => ShowPageDialog(null);
            var layer = LoadView(name, overlay);
            var backdrop = UiChild(layer, "Backdrop");
            var dismiss = BindButton(backdrop.GetComponent<Image>(), close);
            dismiss.transition = Selectable.Transition.None;
            var panel = UiChild(layer, "Content");
            var header = UiChild(panel, "Header");
            MountText(header, "CampDialogs/CampCollectionDialog/JournalCaption", value: heading, selectedFont: JournalFont());
            MountButton(header, "CampDialogs/CampCollectionDialog/关闭", action: close, name: "Close " + name);
            return panel;
        }

        private void DrawMountCatalog(int selectedId = 0, float scrollPosition = 1)
        {
            var panel = CampCollectionDialog("坐骑", "MountCatalog");
            var mounts = game.Relics.Where(relic => relic.kind == 1).ToArray();
            if (mounts.Length == 0)
            {
                MountText(panel, "CampDialogs/DrawMountCatalog/暂无坐骑", selectedFont: JournalFont());
                return;
            }

            var preview = MountPart(panel, "CampDialogs/DrawMountCatalog/preview");
            var name = MountText(preview, "CampDialogs/DrawMountCatalog/JournalCaption", selectedFont: JournalFont());
            var status = MountText(preview, "CampDialogs/DrawMountCatalog/JournalCaption 2", selectedFont: JournalFont());
            MountPart(preview, "CampDialogs/DrawMountCatalog/Panel");
            var properties = MountText(preview, "CampDialogs/DrawMountCatalog/JournalCaption 3", selectedFont: JournalFont());
            properties.name = "Mount bonuses";
            properties.verticalOverflow = VerticalWrapMode.Overflow;
            var owned = mounts.Count(mount => game.Owns(mount) || game.IsEquipped(mount));
            MountText(panel, "CampDialogs/DrawMountCatalog/JournalCaption 4", value: $"坐骑图鉴  {owned}/{mounts.Length}", selectedFont: JournalFont());

            var scroll = MountPart(panel, "CampDialogs/DrawMountCatalog/scroll").GetComponent<ScrollRect>();
            scroll.name = "Mount collection";
            var tiles = new Dictionary<int, Image>();
            var selected = mounts.FirstOrDefault(mount => mount.id == selectedId)
                ?? mounts.FirstOrDefault(game.IsEquipped) ?? mounts[0];
            var ride = MountButton(panel, "CampDialogs/DrawMountCatalog/CampDialogButton", action: () =>
            {
                float position = scroll.verticalNormalizedPosition;
                int mountedId = selected.id;
                Change(game.CanEquip(selected) ? JourneyAction.EquipRelic : JourneyAction.BuyRelic, mountedId, 1,
                    onSuccess: _ => ShowPageDialog(() => DrawMountCatalog(mountedId, position)));
            }, name: "Ride selected mount");
            var rideLabel = ride.GetComponentInChildren<Text>();
            rideLabel.font = JournalFont();

            void Select(Relic mount)
            {
                tiles[selected.id].color = CampDialogCard;
                selected = mount;
                tiles[mount.id].color = Tint(0x3F626F);
                name.text = game.ItemName(mount.token);
                status.text = MountStatus(mount);
                var bonuses = JournalModifierLabels(mount.attributes)
                    .Select(bonus => MountAttributeLabel(bonus.attribute, bonus.label) + "  " + bonus.value);
                properties.text = string.Join("\n", bonuses);
                bool equipped = game.IsEquipped(mount);
                bool canRide = game.CanEquip(mount);
                bool canUnlock = !equipped && game.CanBuyRelic(mount);
                ride.interactable = canRide || canUnlock;
                ride.targetGraphic.color = ride.interactable ? MountAccent : CampDialogCard;
                rideLabel.color = ride.interactable ? CampDialogBase : CampDialogMuted;
                rideLabel.text = equipped ? "骑乘中" : canRide ? "骑乘" : canUnlock ? $"解锁并骑乘 · {game.RelicPrice(mount)} 金币" :
                    game.Profile.cleared.Contains(1) ? $"需要 {game.RelicPrice(mount)} 金币" : "通关第 1 章后可解锁";
            }

            const int columns = 3;
            for (int start = 0; start < mounts.Length; start += columns)
            {
                var row = InstantiateUi("Widgets/MountCatalogRow", scroll.content);
                for (int column = 0; column < columns && start + column < mounts.Length; column++)
                {
                    var mount = mounts[start + column];
                    var tile = (RectTransform)row.GetChild(column);
                    tile.gameObject.SetActive(true);
                    tile.name = "Mount " + mount.id;
                    var image = tile.GetComponent<Image>();
                    tiles.Add(mount.id, image);
                    tile.GetComponent<Button>().onClick.AddListener(() => Select(mount));
                    BindCampDialogText(tile, "Name", game.ItemName(mount.token));
                    BindCampDialogText(tile, "Status", MountStatus(mount)).color = game.IsEquipped(mount) ? MountAccent : CampDialogMuted;
                }
            }
            Select(selected);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = scrollPosition;
        }

        private string MountStatus(Relic mount) => game.IsEquipped(mount) ? "骑乘中" :
            game.Owns(mount) ? "已拥有" : game.ItemCount(mount.token) > 0 ? "可骑乘" : "未解锁";

        private static string MountAttributeLabel(string key, string fallback) => key switch
        {
            "CritRate" => "暴击率",
            "DamageReduction" => "伤害减免",
            "GlobalAttack" => "全局攻击",
            "GlobalDefence" => "全局防御",
            "GlobalHPMax" => "全局生命",
            "NormalCombo" => "普攻连击率",
            "PetDamageAdd" => "宠物伤害",
            "Revenge" => "反击率",
            "Speed" => "速度",
            "TargetArtifactPowerReduction" => "目标神器能量削减",
            _ => fallback
        };

        private void DrawChestRewards(Reward[] rewards, EquipmentChest chest)
        {
            int rows = (rewards.Length + 1) / 2;
            float halfHeight = Mathf.Min(.33f, .175f + (rows - 1) * .042f);
            var panel = CampCollectionDialog("获得奖励", "ChestRewards");
            var pad = panel.anchorMin.y + .3f - halfHeight;
            panel.anchorMin = new Vector2(panel.anchorMin.x, pad);
            panel.anchorMax = new Vector2(panel.anchorMax.x, panel.anchorMax.y - .3f + halfHeight);
            var scroll = InstantiateUi(rows == 1 ? "Widgets/ChestRewardScrollSingle" : "Widgets/ChestRewardScroll", panel).GetComponent<ScrollRect>();
            var reveal = new List<CanvasGroup>();
            bool hasEquipment = rewards.Any(reward => game.Equipment(reward.id) != null);
            for (int start = 0; start < rewards.Length; start += 2)
            {
                var row = InstantiateUi("Widgets/ChestRewardRow", scroll.content);
                for (int column = 0; column < 2 && start + column < rewards.Length; column++)
                    reveal.Add(BindChestRewardTile((RectTransform)row.GetChild(column), rewards[start + column]));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 1;

            var offer = game.ChestOffer(chest);
            var actions = InstantiateUi("Widgets/ChestRewardActions", panel);
            if (offer.draws > 0)
            {
                bool available = offer.cost == 0 || game.ItemCount(offer.currency) >= offer.cost;
                string price = offer.cost == 0 ? "免费" : $"{game.ItemName(offer.currency)} {offer.cost}";
                var repeat = UiChild(actions, hasEquipment ? "Repeat compact" : "Repeat wide").GetComponent<Button>();
                repeat.gameObject.SetActive(true);
                repeat.name = "Open chest again";
                repeat.interactable = available;
                repeat.onClick.AddListener(() => OpenShopChest(chest, offer));
                BindCampDialogText(repeat.transform, "Label", $"再开 ×{offer.draws}\n{price}");
            }
            if (hasEquipment)
            {
                var equipment = UiChild(actions, offer.draws > 0 ? "Equipment compact" : "Equipment wide").GetComponent<Button>();
                equipment.gameObject.SetActive(true);
                equipment.name = "Open equipment from rewards";
                equipment.onClick.AddListener(() => SelectCampPage(CampPage.Equipment, null));
                UiChild((RectTransform)equipment.transform, "Label").GetComponent<Text>().font = JournalFont();
            }
            StartCoroutine(RevealChestRewards(panel, reveal));
        }

        private Text BindCampDialogText(Transform parent, string node, string value)
        {
            var text = UiChild((RectTransform)parent, node).GetComponent<Text>();
            text.text = FormatUiText(value);
            text.font = JournalFont();
            return text;
        }

        private CanvasGroup BindChestRewardTile(RectTransform tile, Reward reward)
        {
            var item = game.Equipment(reward.id);
            tile.name = "Reward " + reward.id;
            tile.gameObject.SetActive(true);
            var (quality, accent) = item?.quality switch
            {
                1 => ("普通", Tint(0xD8DFED)),
                2 => ("优秀", Tint(0xB6E0A6)),
                3 => ("稀有", Tint(0xA0DFF8)),
                4 => ("史诗", Tint(0xD9B5FF)),
                5 => ("传说", RewardAccent),
                6 => ("神话", Tint(0xFFAAA6)),
                _ => (reward.id == 1 || reward.id == 2 ? "货币" : "道具", RewardAccent)
            };
            UiChild(tile, "Tag").GetComponent<Image>().color = accent;
            BindCampDialogText(tile, "Tag/Label", quality);
            BindCampDialogText(tile, "Quantity", $"×{reward.count}").color = accent;
            BindCampDialogText(tile, "Name", item?.name ?? game.ItemName(reward.id));
            return tile.GetComponent<CanvasGroup>();
        }

        private static IEnumerator RevealChestRewards(RectTransform popup, List<CanvasGroup> tiles)
        {
            const float fade = .20f;
            const float interval = .055f;
            float time = 0;
            float duration = fade + Mathf.Min(tiles.Count - 1, 8) * interval;
            while (popup != null && popup.gameObject.activeInHierarchy)
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    float progress = Mathf.SmoothStep(0, 1, (time - Mathf.Min(i, 8) * interval) / fade);
                    tiles[i].alpha = progress;
                    tiles[i].transform.localScale = Vector3.one * Mathf.Lerp(.96f, 1, progress);
                }
                if (time >= duration) yield break;
                yield return null;
                time += Time.unscaledDeltaTime;
            }
        }
    }
}
