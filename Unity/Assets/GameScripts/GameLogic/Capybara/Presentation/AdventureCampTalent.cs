using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    public sealed partial class AdventureScreen
    {
        private const string TalentArtPath = "Reference/CampTalent/";
        private const float TalentCardTop = 1228;
        private const float TalentProgressY = 1024;
        private int talentRankSelection;
        private float talentExperienceMarker;

        private void DrawTalentPage()
        {
            var page = LoadView("Talent", overlay);
            page.name = "Original talent layout";
            MountSprite(page, "CampTalent/DrawTalentPage/talent_bg");
            MountSprite(page, "CampTalent/DrawTalentPage/role_glow");
            DrawTalentHero(page);
            var stage = game.CurrentTalent ?? game.TalentStages.LastOrDefault();
            if (stage != null)
            {
                MountText(page, "CampTalent/DrawTalentPage/CampLabel", value: stage.name);
                DrawTalentProgress(page);
                var increments = new Attributes();
                increments.Add(stage.attributes);
                MountSprite(page, "CampTalent/DrawTalentPage/atrribute_grid_di", viewportScale: true);
                var attributes = InstantiateUi("Widgets/TalentAttributes", page);
                for (int i = 0; i < Adventure.TalentAttributes.Length; i++)
                    BindTalentAttribute(UiChild(attributes, "Attribute " + (i + 1)), stage, increments, i);
                var preview = MountSprite(page, "CampTalent/DrawTalentPage/Icon_btn_preview");
                preview.name = "Talent evolution preview";
                BindButton(preview, () =>
                {
                    talentRankSelection = Math.Max(0, game.TalentRanks.ToList().FindIndex(rank => stage.id >= rank.first && stage.id <= rank.last));
                    ShowPageDialog(DrawTalentPreview);
                });
                MountText(page, "CampTalent/DrawTalentPage/预览");
            }

            MountSprite(page, "CampTalent/DrawTalentPage/bg_frame_2", viewportScale: true);
            var currencies = InstantiateUi("Widgets/TalentCurrencies", page);
            BindCampCurrency(UiChild(currencies, "Gold"), game.Profile.gold.ToString(), OpenCampShop);
            BindCampCurrency(UiChild(currencies, "Gems"), game.ItemCount(2).ToString(), OpenCampShop);
        }

        private void DrawTalentHero(RectTransform parent)
        {
            var preview = InstantiateUi("Widgets/TalentHero", parent).GetComponent<ActorAnimationGraphic>();
            if (preview.Load("Hero")) heroPreview = preview;
        }

        private static float TalentCardX(int attribute) => 61 + 336 * attribute;

        private void BindTalentAttribute(RectTransform card, Talent stage, Attributes increments, int attribute)
        {
            int level = game.TalentAttributeLevel(attribute);
            bool max = level >= stage.levels;
            string stat = Adventure.TalentAttributes[attribute];
            var train = UiChild(card, "Train").GetComponent<Button>();
            train.name = "Train talent " + stat;
            train.onClick.AddListener(() => TrainCampTalent(attribute));
            var background = train.GetComponent<Image>();
            if (max)
            {
                SetUiSprite(background, TalentArtPath + "attribute_max_bg", true);
                SetUiSprite(UiChild(card, "Icon plate").GetComponent<Image>(), TalentArtPath + "atrribute_icon_bg2");
            }
            background.pixelsPerUnitMultiplier = OutputWidth / portrait.sizeDelta.x;
            UiChild(card, "Level").GetComponent<Text>().text = max ? "满级" : $"等级{level}";
            UiChild(card, "Value").GetComponent<Text>().text = $"+{increments.Get(stat)*level:0.##}";
            UiChild(card, "Stat").GetComponent<Text>().color = Tint(max ? 0x4A2875 : 0x514478);
            UiChild(card, "Max").gameObject.SetActive(max);
            UiChild(card, "Price").gameObject.SetActive(!max);
            var price = UiChild(card, "Price/Cost").GetComponent<Text>();
            price.text = stage.cost.ToString();
            price.color = game.CanTrainTalent ? Color.white : Tint(0xFF6551);
        }

        private void DrawTalentProgress(RectTransform page)
        {
            int experience = game.TalentExperience;
            var milestones = game.TalentMilestones;
            if (milestones.Count == 0) return;
            var progress = InstantiateUi("Widgets/TalentProgress", page);
            float startX = UiChild(progress, "Start").anchorMin.x * OutputWidth;
            float middleX = UiChild(progress, "Middle").anchorMin.x * OutputWidth;
            float endX = UiChild(progress, "End").anchorMin.x * OutputWidth;
            int next = 0;
            while (next < milestones.Count - 1 && milestones[next].level <= experience) next++;
            int first = next - next % 2;
            int start = first == 0 ? 0 : milestones[first - 1].level;
            var left = milestones[first];
            var right = milestones[Math.Min(first + 1, milestones.Count - 1)];
            float marker = first == milestones.Count - 1 ? Mathf.Lerp(startX, endX, Mathf.InverseLerp(start, left.level, experience)) :
                experience <= left.level ? Mathf.Lerp(startX, middleX, Mathf.InverseLerp(start, left.level, experience)) :
                Mathf.Lerp(middleX, endX, Mathf.InverseLerp(left.level, right.level, experience));
            talentExperienceMarker = marker;
            MountSprite(progress, "CampTalent/DrawTalentProgress/progress_bg", viewportScale: true);
            var fill = InstantiateUi("Widgets/TalentProgressFill", progress);
            fill.anchorMax = new Vector2(marker / OutputWidth, fill.anchorMax.y);
            fill.GetComponent<Image>().pixelsPerUnitMultiplier = OutputWidth / portrait.sizeDelta.x;
            DrawTalentMilestone(progress, left, first == milestones.Count - 1 ? endX : middleX);
            if (left != right) DrawTalentMilestone(progress, right, endX);
            var cursor = InstantiateUi("Widgets/TalentProgressMarker", progress);
            cursor.anchoredPosition += Vector2.right * TalentHorizontalOffset(progress, marker);
            UiChild(cursor, "Value").GetComponent<Text>().text = experience.ToString();
        }

        private static float TalentHorizontalOffset(RectTransform parent, float x) => (x / OutputWidth - .5f) * parent.rect.width;

        private void DrawTalentMilestone(RectTransform page, TalentMilestone milestone, float x)
        {
            var stage = game.TalentStages.FirstOrDefault(talent => talent.id == milestone.evolution);
            var item = Adventure.TalentItems(milestone).FirstOrDefault();
            bool large = stage?.type == 3;
            var point = InstantiateUi("Widgets/TalentMilestone", page);
            point.anchoredPosition += Vector2.right * TalentHorizontalOffset(page, x);
            var big = UiChild(point, "Large node");
            var small = UiChild(point, "Small node");
            big.gameObject.SetActive(large);
            small.gameObject.SetActive(stage != null && !large);
            UiChild(point, "Talent milestone tick").gameObject.SetActive(stage == null);
            if (stage != null)
                (large ? big : small).GetComponent<Button>().onClick.AddListener(() => ShowPageDialog(() => DrawTalentTip(milestone, x)));
            UiChild(point, "Title").GetComponent<Text>().text = FormatUiText(stage?.name ?? milestone.level.ToString());
            bool coins = item?.id == 1;
            bool hasReward = item != null || !string.IsNullOrEmpty(milestone.icon);
            UiChild(point, "Coin reward").gameObject.SetActive(coins);
            UiChild(point, "Item reward").gameObject.SetActive(hasReward && !coins);
            if (!hasReward) return;

            var reward = UiChild(point, coins ? "Coin reward" : "Item reward");
            var plate = UiChild(reward, "Plate").GetComponent<Button>();
            plate.name = "Talent milestone " + milestone.level;
            plate.onClick.AddListener(() => ShowPageDialog(() => DrawTalentTip(milestone, x)));
            BindTalentRewardIcon(UiChild(reward, "Art"), milestone, item);
            var quantity = UiChild(reward, "Quantity").GetComponent<Text>();
            quantity.text = item?.count.ToString() ?? string.Empty;
            UiChild(reward, "Claimed").gameObject.SetActive(game.TalentExperience >= milestone.level);
        }

        private void BindTalentRewardIcon(RectTransform art, TalentMilestone milestone, Reward item)
        {
            bool coins = item?.id == 1;
            UiChild(art, "Coins").gameObject.SetActive(coins);
            var image = UiChild(art, "Item").GetComponent<Image>();
            if (!coins && !string.IsNullOrEmpty(milestone.icon)) SetUiSprite(image, TalentArtPath + milestone.icon);
            var fallback = UiChild(art, "Fallback").GetComponent<Text>();
            fallback.text = !coins && image.sprite == null && item != null ? FormatUiText(game.ItemName(item.id)) : string.Empty;
        }

        private void TrainCampTalent(int attribute)
        {
            var previousStage = game.CurrentTalent;
            if (previousStage == null || game.TalentAttributeLevel(attribute) >= previousStage.levels)
            {
                ShowTalentNotice("已达到最高等级");
                return;
            }
            if (!game.CanTrainTalent)
            {
                ShowTalentNotice("金币不足");
                return;
            }
            int before = game.TalentExperience;
            Change(JourneyAction.TrainTalent, attribute, preserveScroll: true, onSuccess: _ =>
            {
                int after = game.TalentExperience;
                var milestone = game.TalentMilestones.FirstOrDefault(item => item.level > before && item.level <= after);
                Refresh(true);
                StartCoroutine(PlayTalentUpgrade(overlay, attribute, milestone, previousStage));
            });
        }

        private void ShowTalentNotice(string message)
        {
            var notice = LoadView("TalentNotice",overlay);
            MountText(notice, "CampTalent/ShowTalentNotice/Title", value: message);
            Destroy(notice.gameObject, 1.45f);
        }

        private IEnumerator PlayTalentUpgrade(RectTransform parent, int attribute, TalentMilestone milestone, Talent previousStage)
        {
            var origin = new Vector2(TalentCardX(attribute) + 143, TalentCardTop + 45);
            var badge = InstantiateUi("Widgets/TalentUpgradeBadge", parent);
            badge.anchoredPosition += Vector2.right * TalentHorizontalOffset(parent, origin.x);
            var initialPosition = badge.anchoredPosition;
            var light = MountSprite(parent, "CampTalent/PlayTalentUpgrade/talent_light");
            // Place the glow above the page art and keep the input shield above the nav.
            light.transform.SetSiblingIndex(Mathf.Min(1, parent.childCount - 1));
            float destination = talentExperienceMarker;
            var shield = milestone == null ? null : LoadView("InputShield", parent);
            for (float t = 0; t < .5f && badge != null; t += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, t / .5f);
                var offset = new Vector2(destination - origin.x, origin.y - TalentProgressY - 108) * progress;
                badge.anchoredPosition = initialPosition + offset * (parent.rect.width / OutputWidth);
                light.color = new Color(1, .85f, .52f, Mathf.Sin(progress * Mathf.PI) * .65f);
                yield return null;
            }
            if (badge == null) yield break;
            Destroy(badge.gameObject);
            Destroy(light.gameObject);
            if (shield != null) Destroy(shield.gameObject);
            if (milestone != null && campPage == CampPage.Talent)
                ShowPageDialog(() => DrawTalentResult(milestone, previousStage));
        }

        private void DrawTalentTip(TalentMilestone milestone, float x, float bottom = 790, bool overPreview = false)
        {
            var mask = LoadView("TalentTip", overlay);
            BindButton(mask.GetComponent<Image>(), () =>
            {
                if (overPreview) Destroy(mask.gameObject);
                else ShowPageDialog(null);
            });
            var stage = game.TalentStages.FirstOrDefault(talent => talent.id == milestone.evolution);
            string title = stage?.name ?? milestone.name;
            string description = stage?.description ?? milestone.description;
            var reward = Adventure.TalentItems(milestone).FirstOrDefault();
            if (reward != null && stage == null)
            {
                title = game.ItemName(reward.id);
                description = $"天赋进度达到 {milestone.level} 时获得 ×{reward.count}";
            }
            float cx = Mathf.Clamp(x, 174, 906);
            var tip = InstantiateUi("Widgets/TalentTooltip", mask);
            tip.anchoredPosition += new Vector2((cx - OutputWidth * .5f) * mask.rect.width / OutputWidth,
                (790 - bottom) * mask.rect.height / OutputHeight);
            UiChild(tip, "Panel").GetComponent<Image>().pixelsPerUnitMultiplier = OutputWidth / portrait.sizeDelta.x;
            UiChild(tip, "Header").GetComponent<Image>().pixelsPerUnitMultiplier = OutputWidth / portrait.sizeDelta.x;
            UiChild(tip, "Title").GetComponent<Text>().text = FormatUiText(title);
            UiChild(tip, "Description").GetComponent<Text>().text = FormatUiText(description);
        }

        private void DrawTalentResult(TalentMilestone milestone, Talent previousStage)
        {
            var result = LoadView("TalentResult", overlay);
            result.name = "Talent milestone result";
            BindButton(result.GetComponent<Image>(), () => ShowPageDialog(null));
            var stage = game.TalentStages.FirstOrDefault(talent => talent.id == milestone.evolution);
            var titles = InstantiateUi("Widgets/TalentResultTitles", result);
            UiChild(titles, "Reward").gameObject.SetActive(stage == null);
            UiChild(titles, "Evolution").gameObject.SetActive(stage != null);
            UiChild(titles, (stage == null ? "Reward" : "Evolution") + "/Ribbon").GetComponent<Image>().pixelsPerUnitMultiplier =
                OutputWidth / portrait.sizeDelta.x;
            if (stage != null)
            {
                MountSprite(result, "CampTalent/DrawTalentResult/talent_light");
                DrawTalentHero(result);
                MountText(result, "CampTalent/DrawTalentResult/CampLabel", value: previousStage.name);
                MountSprite(result, "CampTalent/DrawTalentResult/talent_img_arrow");
                MountText(result, "CampTalent/DrawTalentResult/CampLabel 2", value: stage.name);
                var bonuses = new Attributes();
                bonuses.Add(previousStage.completion);
                var stats = game.Player();
                var comparison = InstantiateUi("Widgets/TalentComparison", result);
                for (int row = 0; row < Adventure.TalentAttributes.Length; row++)
                {
                    int index = (row + 1) % Adventure.TalentAttributes.Length;
                    string key = Adventure.TalentAttributes[index];
                    int gain = Mathf.RoundToInt(bonuses.Get(key));
                    var change = UiChild(comparison, "Change " + (row + 1));
                    UiChild(change, "Plate").GetComponent<Image>().pixelsPerUnitMultiplier = OutputWidth / portrait.sizeDelta.x;
                    UiChild(change, "Before").GetComponent<Text>().text = (stats.Stat(key) - gain).ToString();
                    UiChild(change, "After").GetComponent<Text>().text = stats.Stat(key).ToString();
                    UiChild(change, "Gain").GetComponent<Text>().text = $"+{gain}";
                }
            }
            else
            {
                var item = Adventure.TalentItems(milestone).FirstOrDefault();
                MountSprite(result, "CampTalent/DrawTalentResult/talent_light 2");
                BindTalentRewardIcon(InstantiateUi("Widgets/TalentResultReward", result), milestone, item);
                string name = item == null ? milestone.name : $"{game.ItemName(item.id)} ×{item.count}";
                MountText(result, "CampTalent/DrawTalentResult/CampLabel 3", value: name);
                MountSprite(result, "CampTalent/DrawTalentResult/talent_progress");
                MountText(result, "CampTalent/DrawTalentResult/CampLabel 4", value: milestone.description);
            }
            MountText(result, "CampTalent/DrawTalentResult/点击关闭");
        }

        private void DrawTalentPreview()
        {
            var mask = LoadView("TalentPreview", overlay);
            BindButton(mask.GetComponent<Image>(), () => ShowPageDialog(null));
            MountSprite(mask, "CampTalent/DrawTalentPreview/popup_bg_purple", viewportScale: true).raycastTarget = true;
            MountSprite(mask, "CampTalent/DrawTalentPreview/popup_title_bg", viewportScale: true);
            MountText(mask, "CampTalent/DrawTalentPreview/进化之路");
            MountSprite(mask, "CampTalent/DrawTalentPreview/left_di", viewportScale: true);
            for (int i = 0; i < game.TalentRanks.Count; i++)
            {
                int index = i;
                var rank = game.TalentRanks[i];
                var tab = InstantiateUi("Widgets/TalentRankTab", mask);
                tab.anchoredPosition += Vector2.down * (1 - UiChild(tab, "Next row").anchorMin.y) * mask.rect.height * i;
                var button = UiChild(tab, "Select").GetComponent<Image>();
                if (i != talentRankSelection) button.color = Color.clear;
                button.pixelsPerUnitMultiplier = OutputWidth / portrait.sizeDelta.x;
                button.GetComponent<Button>().onClick.AddListener(() => { talentRankSelection = index; Refresh(true); });
                UiChild(tab, "Rank").GetComponent<Text>().text = FormatUiText(rank.name);
            }
            if (game.TalentRanks.Count > 0)
            {
                var rank = game.TalentRanks[talentRankSelection];
                var scroll = MountPart(mask, "CampTalent/DrawTalentPreview/scroll").GetComponent<ScrollRect>();
                scroll.content.GetComponent<VerticalLayoutGroup>().spacing = 0;
                int first = game.TalentStages.First(talent => talent.id == rank.first).experience;
                int last = game.TalentStages.FirstOrDefault(talent => talent.id == rank.last + 1)?.experience ?? int.MaxValue;
                int previous = first;
                PageRow(scroll.content, 106);
                foreach (var milestone in game.TalentMilestones.Where(item => item.level >= first && item.level < last))
                {
                    DrawTalentPreviewMilestone(PageRow(scroll.content, 260), milestone, previous);
                    previous = milestone.level;
                }
            }
            var close = MountSprite(mask, "CampTalent/DrawTalentPreview/btn_close_dark");
            close.name = "Close talent preview";
            BindButton(close, () => ShowPageDialog(null));
        }

        private void DrawTalentPreviewMilestone(RectTransform row, TalentMilestone milestone, int previous)
        {
            int experience = game.TalentExperience;
            bool received = experience >= milestone.level;
            bool active = experience >= previous && !received;
            float progress = received ? 1 : Mathf.InverseLerp(previous, milestone.level, experience);
            MountSprite(row, "CampTalent/DrawTalentPreviewMilestone/progress_bg_h", viewportScale: true);
            if (progress > 0)
            {
                var fill = InstantiateUi("Widgets/TalentPreviewFill", row);
                fill.anchorMin = new Vector2(fill.anchorMin.x, 1 - progress);
                fill.GetComponent<Image>().pixelsPerUnitMultiplier = OutputWidth / portrait.sizeDelta.x;
            }
            MountSprite(row, "CampTalent/DrawTalentPreviewMilestone/left_split", viewportScale: true);
            MountText(row, "CampTalent/DrawTalentPreviewMilestone/Title", value: milestone.level.ToString(), color: Tint(received ? 0xD5F44E : active ? 0xFFF380 : 0x8C86A0));

            Action<RectTransform> openTooltip = anchor =>
            {
                Vector2 pixel = RectTransformUtility.WorldToScreenPoint(null, anchor.position);
                float bottom = Mathf.Clamp((1 - pixel.y / Screen.height) * OutputHeight - 66, 324, 1620);
                DrawTalentTip(milestone, 730, bottom, true);
            };
            var stage = game.TalentStages.FirstOrDefault(talent => talent.id == milestone.evolution);
            if (stage != null)
            {
                var node = InstantiateUi(stage.type == 3 ? "Widgets/TalentPreviewLargeNode" : "Widgets/TalentPreviewSmallNode", row);
                node.GetComponent<Button>().onClick.AddListener(() => openTooltip(node));
                MountText(row, "CampTalent/DrawTalentPreviewMilestone/Title 2", value: stage.name);
                return;
            }

            var item = Adventure.TalentItems(milestone).FirstOrDefault();
            var plate = MountSprite(row, "CampTalent/DrawTalentPreviewMilestone/plate", sprite: "Reference/CampTalent/" + (item?.id == 1 ? "reward2_bg_blue" : "reward2_bg_yellow"), sliced: true, viewportScale: true);
            BindButton(plate, () => openTooltip(plate.rectTransform));
            if (item?.id == 1) MountSprite(row, "CampTalent/DrawTalentPreviewMilestone/icon_coin");
            else if (!string.IsNullOrEmpty(milestone.icon))
                MountSprite(row, "CampTalent/DrawTalentPreviewMilestone/TalentArt", sprite: "Reference/CampTalent/" + (milestone.icon));
            if (item != null) MountText(row, "CampTalent/DrawTalentPreviewMilestone/Title 3", value: item.count.ToString());
            if (received) MountSprite(row, "CampTalent/DrawTalentPreviewMilestone/checkmark_di");
        }

    }
}
