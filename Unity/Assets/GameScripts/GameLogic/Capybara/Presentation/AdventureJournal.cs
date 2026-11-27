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
    public sealed partial class AdventureScreen
    {
        private static readonly Color JournalText = Tint(0x282231);
        private static readonly Color JournalMuted = Tint(0xF1EAF8);
        private static readonly Color JournalGreen = Tint(0x28725D);
        private static readonly Color JournalLucky = Tint(0x24688D);
        private static readonly Color JournalGreat = Tint(0xA25338);
        private static readonly Color JournalCoral = Tint(0xA1364A);
        private static readonly Color JournalInsight = Tint(0x634D86);
        private const float JournalBottomTolerance = 12;
        private const float FortuneReelRollTime = 1.50f;
        private const float FortuneReelHoldTime = .30f;
        private const float FortuneReelEnd = FortuneReelRollTime + FortuneReelHoldTime;
        private Font journalFont;
        private Texture2D journalSweepTexture, journalPulseTexture, journalAnnouncementTexture;
        private readonly Dictionary<JournalSymbol, Sprite> journalSymbols = new Dictionary<JournalSymbol, Sprite>();
        private readonly List<(Text caption, LayoutElement layout, int footerHeight)> journalStories =
            new List<(Text caption, LayoutElement layout, int footerHeight)>();
        private Run journalRun;
        private int journalPresentedSerial;
        private int journalRenderedDay;
        private Phase journalRenderedPhase;
        private bool journalViewBuilt, journalFollowLatest = true;
        private bool journalScrollTouched;
        private float journalReadOffset;
        private Coroutine journalEntrance;
        private Button journalAction;
        private RectTransform journalSealLayer;
        private RectTransform journalTransferLayer;
        private JourneyStats? journalPreviousStats;
        private Image journalTravelFill;

        private enum JournalCardKind { Story, Setback, Fortune, GreatFortune, Victory, Skill }
        private enum JournalSymbol { Warning, Fortune, GreatFortune, Victory, Skill, Experience, Gold, Attack, Defence, Health, Change }

        private struct JourneyStats
        {
            public readonly int level, experience, nextExperience, maxHp, attack, defence;
            public int hp;

            public JourneyStats(Adventure adventure)
            {
                var run = adventure.Run;
                var player = adventure.Player();
                level = run.level;
                experience = run.experience;
                nextExperience = adventure.NextExperience;
                hp = run.hp;
                maxHp = player.Stat("HPMax");
                attack = player.Stat("Attack");
                defence = player.Stat("Defence");
            }
        }

        private sealed class JournalEntrance
        {
            public RectTransform row, body;
            public Image warningLine;
            public CanvasGroup group;
            public CanvasGroup badge;
            public Text rollingStory;
            public RectTransform reel;
            public Text[] reelEntries;
            public JournalDetail[] details;
            public JournalCardKind kind;
            public JournalSealEffect effect;
            public (CanvasGroup group, RectTransform label, RawImage glow, RectTransform[] glints) announcement;
            public float startTime, scrollFrom, scrollTo, scrollDuration;
            public bool Great => kind == JournalCardKind.GreatFortune;
            public bool Insight => kind == JournalCardKind.Skill;
            public bool HasSeal => Great || Insight || kind == JournalCardKind.Fortune;
            public float BadgeArrival => rollingStory != null ? FortuneReelEnd + .10f : Great ? .90f : .54f;
            public float DetailStart => HasSeal ? BadgeArrival + .10f : .14f;
            public JournalSymbol SealSymbol => Insight ? JournalSymbol.Skill : Great ? JournalSymbol.GreatFortune : JournalSymbol.Fortune;
            public Color SealLight => Insight ? Tint(0xB88AFC) : Great ? Tint(0xFF525C) : Tint(0x58BFFF);
        }

        private sealed class JournalSealEffect
        {
            public RawImage sweep, echo, pulse;
            public RectTransform layer, token;
            public CanvasGroup tokenOpacity;
        }

        private readonly struct JournalDetail
        {
            public readonly CanvasGroup group;
            public readonly string attribute, value;
            public readonly bool negative;

            public JournalDetail(CanvasGroup group, string attribute, string value, bool negative)
            {
                this.group = group;
                this.attribute = attribute;
                this.value = value;
                this.negative = negative;
            }
        }

        private sealed class JournalTransfer
        {
            public RectTransform source, target, visual;
            public Vector2 origin;
            public float start;
            public string value;
            public Color color;
            public bool started;
        }

        private void DrawJourneyJournal(Run run)
        {
            var journal=LoadView("JourneyJournal",overlay);
            journalScroll = UiChild(journal,"Scroll").GetComponent<ScrollRect>();
            var trigger = journalScroll.GetComponent<EventTrigger>();
            foreach (var gesture in new[] { EventTriggerType.BeginDrag, EventTriggerType.Scroll })
            {
                var entry = new EventTrigger.Entry { eventID = gesture };
                entry.callback.AddListener(_ => journalScrollTouched = true);
                trigger.triggers.Add(entry);
            }
            journalViewBuilt = false;
            UpdateJournal(run);
        }

        private void SaveJournalScroll()
        {
            if (journalScroll == null) return;
            if (journalEntrance != null)
            {
                StopCoroutine(journalEntrance);
                journalEntrance = null;
                FinishJournalPresentation();
            }
            if (journalScrollTouched)
            {
                journalFollowLatest = IsJournalAtLatest();
            }
            journalReadOffset = Mathf.Max(0, journalScroll.content.anchoredPosition.y);
        }

        private bool IsJournalAtLatest() => JournalExtent(journalScroll) -
            journalScroll.content.anchoredPosition.y <= JournalBottomTolerance;

        private static float JournalExtent(ScrollRect scroll) => Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);

        private static void SetJournalOffset(ScrollRect scroll, float offset, float extent) =>
            scroll.verticalNormalizedPosition = extent > 0 ? 1 - Mathf.Clamp01(offset / extent) : 0;

        private void FollowLatestJournal()
        {
            journalFollowLatest = true;
            journalScrollTouched = false;
            if (journalScroll == null) return;
            journalScroll.StopMovement();
            journalScroll.verticalNormalizedPosition = 0;
        }

        private void UpdateJournal(Run run)
        {
            if (journalScroll == null) return;
            bool sameRun = journalRun == run;
            int serial = run.journalSerial;
            if (journalViewBuilt && sameRun && journalRenderedDay == run.stage && journalRenderedPhase == run.phase)
            {
                int appended = serial - journalPresentedSerial;
                if (appended == 0 || !run.journal.Skip(Math.Max(0, run.journal.Count - appended)).Any(IsVisibleJournalMessage))
                {
                    journalPresentedSerial = serial;
                    return;
                }
            }

            if (journalViewBuilt) SaveJournalScroll();
            int previousSerial = sameRun ? journalPresentedSerial : serial;
            float offset = sameRun ? journalReadOffset : 0;
            bool follow = !sameRun || journalFollowLatest;
            bool choosingSkill = run.phase == Phase.Skill;
            bool canReveal = !choosingSkill && run.phase != Phase.Result;
            journalRun = run;
            // Hold new entries until the skill selection overlay is closed.
            journalPresentedSerial = choosingSkill ? previousSerial : serial;
            journalRenderedDay = run.stage;
            journalRenderedPhase = run.phase;
            journalViewBuilt = true;
            journalFollowLatest = follow;
            journalScrollTouched = false;

            var list = journalScroll.content;
            journalStories.Clear();
            foreach (Transform child in list)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var incoming = new List<JournalEntrance>();
            int rowDay = -1;
            for (int index = 0; index < run.journal.Count; index++)
            {
                string message = run.journal[index];
                if (!IsVisibleJournalMessage(message)) continue;
                int day = run.journalDays[index];
                bool mainEntry = day != rowDay;
                if (mainEntry) DrawJournalDay(list, day);
                rowDay = day;
                var battle = message == "战斗胜利。" ? run.battleRewards.FirstOrDefault(reward => reward.day == day) : null;
                var fortune = index < run.journalFortunes.Count ? run.journalFortunes[index] : JournalFortune.None;
                int entrySerial = serial - run.journal.Count + index + 1;
                var details = run.journalModifiers.Find(modifier => modifier.serial == entrySerial);
                bool reveal = canReveal && entrySerial > previousSerial && follow;
                var entry = DrawJournalCard(list, message, fortune, battle, details, reveal, mainEntry);
                if (reveal) incoming.Add(entry);
            }

            if (run.stage > 0 && rowDay != run.stage)
            {
                DrawJournalDay(list, run.stage);
                DrawJournalCard(list, Adventure.EventName(run.eventKind), JournalFortune.None, null);
            }

            LayoutJournalStories();
            Canvas.ForceUpdateCanvases();
            journalScroll.StopMovement();
            float extent = JournalExtent(journalScroll);
            float start = Mathf.Clamp(offset, 0, extent);
            if (incoming.Count == 0)
            {
                SetJournalOffset(journalScroll, follow && !choosingSkill ? extent : start, extent);
            }
            else
            {
                SetJournalOffset(journalScroll, start, extent);
                journalEntrance = StartCoroutine(AnimateJournal(journalScroll, incoming, start, extent));
            }
        }

        private void LayoutJournalStories()
        {
            var list = journalScroll.content;
            // Resolve the row widths before measuring wrapped text at the current canvas scale.
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            foreach (var (caption, layout, footerHeight) in journalStories)
                layout.preferredHeight = Mathf.Max(56, Mathf.Ceil(caption.preferredHeight + 34)) + footerHeight;
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        }

        private static bool IsVisibleJournalMessage(string message) =>
            !string.IsNullOrEmpty(message) &&
            !message.StartsWith("回合 ", StringComparison.Ordinal) &&
            !message.StartsWith("抵达第", StringComparison.Ordinal);

        private void DrawJournalDay(RectTransform list, int day)
        {
            var heading = Row(list, 32);
            heading.name = "Journey day " + day;
            heading.GetComponent<Image>().color = Color.clear;
            var date = MountText(heading, "Journal/DrawJournalDay/date", value: day > 0 ? $"第{day}天" : "启程", selectedFont: JournalFont());
            var line = MountPart(heading, "Journal/DrawJournalDay/line");
            line.offsetMin = new Vector2(date.preferredWidth + 15, 0);
        }

        private JournalEntrance DrawJournalCard(RectTransform list, string message, JournalFortune fortune, BattleReward battle,
            JournalModifier details = null, bool reveal = false, bool mainEntry = false)
        {
            bool oldAttributeLine = message.StartsWith(Adventure.JournalAttributePrefix, StringComparison.Ordinal);
            string attributes = details?.attributes;
            if (attributes == null && oldAttributeLine) attributes = message.Substring(Adventure.JournalAttributePrefix.Length);
            if (battle != null) attributes = $"Experience={battle.experience}|Gold={battle.gold}";
            var modifiers = JournalModifierLabels(attributes).ToList();
            string skillName = details?.skillName;
            const string learnedPrefix = "习得「", learnedSuffix = "」。";
            if (string.IsNullOrEmpty(skillName) && message.StartsWith(learnedPrefix, StringComparison.Ordinal) &&
                message.EndsWith(learnedSuffix, StringComparison.Ordinal))
                skillName = message.Substring(learnedPrefix.Length, message.Length - learnedPrefix.Length - learnedSuffix.Length);
            bool hasSkill = !string.IsNullOrEmpty(skillName);
            if (hasSkill) modifiers.Add((null, null, skillName, false));
            var kind = fortune switch
            {
                JournalFortune.Unlucky => JournalCardKind.Setback,
                JournalFortune.Lucky => JournalCardKind.Fortune,
                JournalFortune.Great => JournalCardKind.GreatFortune,
                _ when message == "战斗胜利。" => JournalCardKind.Victory,
                _ when hasSkill => JournalCardKind.Skill,
                _ => JournalCardKind.Story
            };
            var (faceColor, edgeColor, accent, badge, emblem) = kind switch
            {
                JournalCardKind.Setback => (0xF9E4E3, 0x693446, JournalCoral, "运气不佳", (JournalSymbol?)JournalSymbol.Warning),
                JournalCardKind.Fortune => (0xE3F2F8, 0x4F8BAC, JournalLucky, "小吉", JournalSymbol.Fortune),
                JournalCardKind.GreatFortune => (0xFFF0DA, 0xB56A45, JournalGreat, "大吉", JournalSymbol.GreatFortune),
                JournalCardKind.Victory => (0xE8EDF4, 0x3C5676, Tint(0x3E5E81), null, JournalSymbol.Victory),
                JournalCardKind.Skill => (0xEDE6F6, 0x5B467D, JournalInsight, "领悟", JournalSymbol.Skill),
                _ => (0xF4F0EC, 0x534957, Tint(0x665768), null, null)
            };
            int metricCount = modifiers.Count - (hasSkill ? 1 : 0);
            int metricRows = (metricCount + 1) / 2;
            int detailRows = metricRows + (hasSkill ? 1 : 0);
            bool badgeInFooter = kind == JournalCardKind.Setback || kind == JournalCardKind.Fortune || kind == JournalCardKind.GreatFortune;
            int footerRows = badgeInFooter ? Math.Max(1, detailRows) : detailRows;
            int modifierHeight = footerRows == 0 ? 0 : footerRows * 30 + 6;
            var row = Row(list, 54);
            row.name = kind switch
            {
                JournalCardKind.Setback => "Unfavorable journey entry",
                JournalCardKind.Fortune => "Lucky fortune entry",
                JournalCardKind.GreatFortune => "Great fortune entry",
                JournalCardKind.Victory => "Victory journey entry",
                JournalCardKind.Skill => "Skill journey entry",
                _ => "Journey entry"
            };
            row.GetComponent<Image>().color = Color.clear;
            var border = MountPanel(row, "Journal/DrawJournalCard/border", color: Tint(edgeColor));
            var body = MountPanel(border, "Journal/DrawJournalCard/body", color: Tint(faceColor));
            bool hasStoryIcon = emblem.HasValue && badge == null;
            string content = kind == JournalCardKind.Victory ? "战斗胜利" : hasSkill ? "习得技能" : message;
            if (modifiers.Count > 0)
            {
                if (oldAttributeLine) content = "属性变化";
                else if (message.StartsWith("好运转盘 · ", StringComparison.Ordinal)) content = "转盘结算";
            }
            if (hasStoryIcon)
            {
                var seal = MountPanel(body, "Journal/DrawJournalCard/seal", color: accent);
                JourneySymbolAt(seal, emblem.Value, Color.white, .5f, .5f, 21);
            }
            var story = InstantiateUi(hasStoryIcon ? "Widgets/JournalStoryWithIcon" :
                badge != null && !badgeInFooter ? "Widgets/JournalStoryWithBadge" : "Widgets/JournalStory", body);
            story.offsetMin += Vector2.up * modifierHeight;
            var caption = MountText(story, "Journal/DrawJournalCard/caption", value: content, selectedFont: JournalFont());
            journalStories.Add((caption, row.GetComponent<LayoutElement>(), modifierHeight));
            float contentWidth = (journalScroll.viewport.rect.width - 10) * .988f - 4;
            CanvasGroup badgeGroup = null;
            float badgeWidth = 0;
            if (badge != null)
            {
                var marker = InstantiateUi(kind == JournalCardKind.Setback ? "Widgets/JournalBadgeWide" :
                    badgeInFooter ? "Widgets/JournalBadge" : "Widgets/JournalInsightBadge", body);
                marker.GetComponent<Image>().color = accent;
                badgeWidth = marker.sizeDelta.x;
                JourneySymbolAt(marker, emblem.Value, Color.white, 0, .5f, 16, 15);
                MountText(marker, "Journal/DrawJournalCard/label", value: badge, selectedFont: JournalFont());
                if (reveal) badgeGroup = marker.GetComponent<CanvasGroup>();
            }
            if (footerRows > 0)
            {
                var footer = MountPanel(body, "Journal/DrawJournalCard/footer", color: new Color(accent.r, accent.g, accent.b, .20f));
                footer.anchoredPosition += Vector2.up * modifierHeight;
            }

            var groups = reveal && modifiers.Count > 0 ? new JournalDetail[modifiers.Count] : Array.Empty<JournalDetail>();
            float chipLeft = 0;
            float footerWidth = Mathf.Max(1, contentWidth * (1 - 2 * story.anchorMin.x) - (badgeInFooter ? badgeWidth + 8 : 0));
            float maxMetricWidth = Mathf.Max(1, (footerWidth - 8) * .5f);
            for (int i = 0; i < modifiers.Count; i++)
            {
                var (attribute, label, value, negative) = modifiers[i];
                bool secondColumn = label != null && i % 2 == 1;
                int detailRow = label == null ? metricRows : i / 2;
                float bottom = 12 + 30 * (detailRows - detailRow - 1);
                if (!secondColumn) chipLeft = 0;
                var chip = MountPanel(body, "Journal/DrawJournalCard/chip", color: Color.Lerp(accent, JournalText, .60f));
                var (symbol, glyphColor) = JournalMetricSymbol(attribute);
                JourneySymbolAt(chip, symbol, glyphColor, 0, .5f, 16, 15);
                float chipWidth;
                if (reveal) groups[i] = new JournalDetail(chip.GetComponent<CanvasGroup>(), attribute, value, negative);
                if (label == null)
                {
                    var skill = MountText(chip, "Journal/DrawJournalCard/skill", value: value, selectedFont: JournalFont());
                    chipWidth = Mathf.Min(footerWidth, skill.preferredWidth + 39);
                }
                else
                {
                    var metric = MountText(chip, "Journal/DrawJournalCard/metric", value: label, selectedFont: JournalFont());
                    var amount = MountText(chip, "Journal/DrawJournalCard/amount", value: value, color: negative ? Tint(0xFFB6AA) : Tint(0xADF1D5), selectedFont: JournalFont());
                    float labelWidth = Mathf.Ceil(metric.preferredWidth);
                    chipWidth = Mathf.Min(maxMetricWidth, labelWidth + Mathf.Ceil(amount.preferredWidth) + 45);
                    float valueStart = Mathf.Max(31, chipWidth - Mathf.Ceil(amount.preferredWidth) - 10);
                    metric.rectTransform.offsetMax = new Vector2(-Mathf.Max(9, chipWidth - valueStart + 6), 0);
                    amount.rectTransform.offsetMin = new Vector2(valueStart, 0);
                }
                float chipHeight = chip.sizeDelta.y;
                chip.offsetMin = new Vector2(chipLeft, bottom);
                chip.offsetMax = new Vector2(chipLeft + chipWidth, bottom + chipHeight);
                chipLeft += chipWidth + 8;
            }

            return new JournalEntrance
            {
                row = row, body = body, badge = badgeGroup, details = groups, kind = kind,
                rollingStory = reveal && mainEntry && fortune == JournalFortune.Great ? caption : null
            };
        }

        private JourneyStats ReadJournalStats() => new JourneyStats(game);

        private void ShowJournalStats(JourneyStats stats)
        {
            displayedMaxHp = stats.maxHp;
            targetHp = stats.hp;
            healthChangeElapsed = HealthChangeDuration;
            UpdateJourneyHealth(stats.hp);
            experienceCaption.text = "等级" + stats.level;
            experienceFill.fillAmount = stats.experience / (float)Math.Max(1, stats.nextExperience);
            attackCaption.text = stats.attack.ToString();
            defenceCaption.text = stats.defence.ToString();
        }

        private void FinishJournalPresentation()
        {
            if (journalTransferLayer != null)
            {
                journalTransferLayer.gameObject.SetActive(false);
                Destroy(journalTransferLayer.gameObject);
                journalTransferLayer = null;
            }
            if (journalSealLayer != null)
            {
                journalSealLayer.gameObject.SetActive(false);
                Destroy(journalSealLayer.gameObject);
                journalSealLayer = null;
            }
            if (journalPreviousStats.HasValue)
            {
                journalPreviousStats = null;
                if (game.Run != null && healthCaption != null) ShowJournalStats(ReadJournalStats());
            }
            if (journalAction != null) journalAction.interactable = true;
        }

        private static IEnumerable<(string attribute, string label, string value, bool negative)> JournalModifierLabels(string attributes)
        {
            if (string.IsNullOrEmpty(attributes)) yield break;
            foreach (string term in attributes.Split('|'))
            {
                var pair = term.Split('=');
                if (pair.Length != 2 || !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float amount) ||
                    float.IsNaN(amount) || float.IsInfinity(amount) || amount == 0) continue;
                string key = pair[0].Trim();
                bool percentage = key.EndsWith("%", StringComparison.Ordinal);
                string attribute = key.TrimEnd('%');
                string label = attribute switch
                {
                    "Attack" => "攻击",
                    "Defence" or "Defense" => "防御",
                    "HPMax" => "生命上限",
                    "HP" => "当前生命",
                    "Gold" => "金币",
                    "Experience" => "经验",
                    _ => key.TrimEnd('%')
                };
                string signed = (amount < 0 ? "−" : "+") + Math.Abs(amount).ToString("0.##", CultureInfo.InvariantCulture);
                yield return (attribute, label, percentage ? signed + "%" : signed, amount < 0);
            }
        }

        private static Color JournalAccent(JournalCardKind kind) => kind switch
        {
            JournalCardKind.Fortune => JournalLucky,
            JournalCardKind.GreatFortune => JournalGreat,
            _ => JournalInsight
        };

        private static (JournalSymbol symbol, Color tint) JournalMetricSymbol(string attribute) => attribute switch
        {
            "Experience" => (JournalSymbol.Experience, Tint(0xBEE0F9)),
            "Gold" => (JournalSymbol.Gold, Tint(0xFBDD9E)),
            "Attack" => (JournalSymbol.Attack, Tint(0xF2C0A7)),
            "Defence" or "Defense" => (JournalSymbol.Defence, Tint(0xAACFFC)),
            "HP" or "HPMax" => (JournalSymbol.Health, Tint(0xF8ADC1)),
            null => (JournalSymbol.Skill, Tint(0xE3C7FF)),
            _ => (JournalSymbol.Change, Tint(0xE0D4E8))
        };

        private RectTransform JourneySymbolAt(RectTransform parent, JournalSymbol symbol, Color tint,
            float x, float y, float size, float offsetX = 0)
        {
            if (!journalSymbols.TryGetValue(symbol, out var sprite))
            {
                string rows = symbol switch
                {
                    JournalSymbol.Warning      => "....#..../...###.../..##.##../..##.##../.###.###./.###.###./####.####/#########/.........",
                    JournalSymbol.Fortune      => "....#..../...###.../..#####../.#######./#########/.#######./..#####../...###.../....#....",
                    JournalSymbol.GreatFortune => "....#..../....#..../##.###.##/.#######./#########/.#######./##.###.##/....#..../....#....",
                    JournalSymbol.Victory      => ".......##/......###/.....###./....###../...###.../###.#..../#####..../.###...../..#......",
                    JournalSymbol.Skill        => "........./####.####/#..###..#/#...#...#/#...#...#/#...#...#/#...#...#/####.####/....#....",
                    JournalSymbol.Experience   => "....#..../...###.../..##.##../.##...##./....#..../...###.../..##.##../.##...##./.........",
                    JournalSymbol.Gold         => "..#####../.##...##./##..#..##/#...#...#/#...#...#/#...#...#/##..#..##/.##...##./..#####..",
                    JournalSymbol.Attack       => "......###/.....####/....####./...####../..####.../..##...../.###...../####...../##.......",
                    JournalSymbol.Defence      => ".#######./.#.....#./.#..#..#./.#..#..#./.#####.#./..#...#../..#...#../...###.../....#....",
                    JournalSymbol.Health       => ".##...##./####.####/#########/#########/.#######./..#####../...###.../....#..../.........",
                    _                          => "........./....##.../...##..../..##...../.#######./.....##../....##.../...##..../........."
                };
                const int side = 9;
                var texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[side * side];
                for (int row = 0; row < side; row++)
                for (int column = 0; column < side; column++)
                    pixels[(side - 1 - row) * side + column] = new Color32(255, 255, 255, rows[row * (side + 1) + column] == '#' ? (byte)255 : (byte)0);
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                sprite = Sprite.Create(texture, new Rect(0, 0, side, side), new Vector2(.5f, .5f));
                journalSymbols.Add(symbol, sprite);
            }
            var icon = Panel(parent, tint, (x, y, x, y));
            icon.name = "Journal icon " + symbol;
            icon.sizeDelta = Vector2.one * size;
            icon.anchoredPosition = new Vector2(offsetX, 0);
            icon.GetComponent<Image>().sprite = sprite;
            return icon;
        }

        private Font JournalFont()
        {
            if (journalFont == null)
            {
                var families = Font.GetOSInstalledFontNames();
                var supported = new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC", "SimHei" };
                var family = supported.FirstOrDefault(families.Contains);
                journalFont = family != null ? Font.CreateDynamicFontFromOSFont(family, 19) : font;
            }
            return journalFont;
        }

        private RectTransform RoundedJournalPanel(RectTransform parent, Color tint, (float left, float bottom, float right, float top) bounds)
        {
            var (left, bottom, right, top) = bounds;
            var rect = InstantiateUi("Widgets/RoundedPanel", parent);
            PlaceUi(rect, left, bottom, right, top);
            rect.GetComponent<Image>().color = tint;
            return rect;
        }

        private void OnDestroy()
        {
            if (journalSweepTexture != null) Destroy(journalSweepTexture);
            if (journalPulseTexture != null) Destroy(journalPulseTexture);
            if (journalAnnouncementTexture != null) Destroy(journalAnnouncementTexture);
            foreach (var sprite in journalSymbols.Values)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        private List<JournalTransfer> BuildJournalTransfers(List<JournalEntrance> rows)
        {
            var transfers = new List<JournalTransfer>();
            if (!journalPreviousStats.HasValue || game.Phase != Phase.Trail) return transfers;
            var previous = journalPreviousStats.Value;
            var settled = ReadJournalStats();
            var targets = new HashSet<Text>();
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                var entry = rows[i];
                for (int j = 0; j < entry.details.Length; j++)
                {
                    var detail = entry.details[j];
                    Text target = detail.attribute switch
                    {
                        "Experience" when previous.level != settled.level || previous.experience != settled.experience => experienceCaption,
                        "HP" or "HPMax" when previous.hp != settled.hp || previous.maxHp != settled.maxHp => healthCaption,
                        "Attack" when previous.attack != settled.attack => attackCaption,
                        "Defence" or "Defense" when previous.defence != settled.defence => defenceCaption,
                        _ => null
                    };
                    if (target == null || !targets.Add(target)) continue;
                    transfers.Add(new JournalTransfer
                    {
                        source = (RectTransform)detail.group.transform,
                        target = target.rectTransform,
                        start = entry.startTime + entry.DetailStart + JournalDetailDelay(j) + .18f,
                        value = detail.value,
                        color = detail.negative ? JournalCoral : JournalGreen
                    });
                }
            }
            return transfers;
        }

        private const float JournalTransferDuration = .32f;

        private const float JournalCounterDuration = .26f;

        private void AnimateJournalTransfers(ScrollRect scroll, List<JournalTransfer> transfers, float elapsed,
            JourneyStats before, JourneyStats after)
        {
            foreach (var transfer in transfers)
            {
                if (elapsed < transfer.start) continue;
                float arrival = elapsed - transfer.start - JournalTransferDuration;
                if (arrival >= 0)
                {
                    float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(arrival / JournalCounterDuration));
                    if (transfer.target == attackCaption.rectTransform)
                        attackCaption.text = Mathf.RoundToInt(Mathf.Lerp(before.attack, after.attack, blend)).ToString();
                    else if (transfer.target == defenceCaption.rectTransform)
                        defenceCaption.text = Mathf.RoundToInt(Mathf.Lerp(before.defence, after.defence, blend)).ToString();
                    else if (transfer.target == healthCaption.rectTransform)
                    {
                        displayedMaxHp = Mathf.RoundToInt(Mathf.Lerp(before.maxHp, after.maxHp, blend));
                        UpdateJourneyHealth(Mathf.RoundToInt(Mathf.Lerp(before.hp, after.hp, blend)));
                    }
                    else if (before.level == after.level)
                    {
                        float xp = Mathf.Lerp(before.experience, after.experience, blend);
                        experienceFill.fillAmount = xp / Math.Max(1, after.nextExperience);
                    }
                }
                if (!transfer.started)
                {
                    transfer.started = true;
                    var center = transfer.source.TransformPoint(transfer.source.rect.center);
                    if (!scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint(center))) continue;
                    if (journalTransferLayer == null)
                        journalTransferLayer = MountPart(overlay, "Journal/AnimateJournalTransfers/Journey reward transfers");
                    transfer.origin = journalTransferLayer.InverseTransformPoint(center);
                    transfer.visual = MountPanel(journalTransferLayer, "Journal/AnimateJournalTransfers/RoundedJournalPanel", color: transfer.color);
                    transfer.visual.name = "Journey reward transfer";
                    MountText(transfer.visual, "Journal/AnimateJournalTransfers/JournalCaption", value: transfer.value, selectedFont: JournalFont());
                }
                if (transfer.visual == null) continue;
                float progress = Mathf.Clamp01((elapsed - transfer.start) / JournalTransferDuration);
                var target = (Vector2)journalTransferLayer.InverseTransformPoint(transfer.target.TransformPoint(transfer.target.rect.center));
                var path = Vector2.Lerp(transfer.origin, target, Mathf.SmoothStep(0, 1, progress));
                path.x += 22 * Mathf.Sin(progress * Mathf.PI);
                transfer.visual.localPosition = path;
                transfer.visual.localScale = Vector3.one * Mathf.Lerp(1, .55f, progress);
                transfer.visual.gameObject.SetActive(progress < 1);
            }
        }

        private static float JournalDetailDelay(int index) => Mathf.Min(.18f, index * .06f);

        private static readonly string[] FortuneReelPreviews =
        {
            "远处的风声渐渐平息。", "路边留下了一串脚印。", "枝叶间闪过一点微光。",
            "石阶上传来轻响。", "几片落叶掠过小路。", "树影里传来一阵窸窣声。",
            "薄雾散开，前方显出一条小路。", "一只鸟从路旁飞起。", "云影越过了山坡。",
            "溪水绕过一块灰白的石头。", "远处响起一声铃。", "一枚种子落在肩头。",
            "草丛里露出半截路标。", "坡下升起一缕炊烟。", "一阵细雨掠过林梢。",
            "落日照亮了桥面。", "树根旁冒出几朵蘑菇。", "湿土上留着车辙。", "一根芦苇随风摇动。"
        };

        private void CreateFortuneReel(JournalEntrance entry)
        {
            var story = entry.rollingStory;
            var viewport = MountPart((RectTransform)story.transform.parent, "Journal/CreateFortuneReel/Fortune text reel");
            viewport.sizeDelta = new Vector2(0, story.fontSize + 9);
            entry.reel = viewport;
            entry.reelEntries = new Text[FortuneReelPreviews.Length + 1];
            for (int i = 0; i < entry.reelEntries.Length; i++)
            {
                string line = i == 0 ? story.text : FortuneReelPreviews[i - 1];
                var caption = MountText(viewport, "Journal/CreateFortuneReel/caption", value: line, size: story.fontSize, color: story.color, style: story.fontStyle, selectedFont: JournalFont());
                caption.verticalOverflow = VerticalWrapMode.Truncate;
                entry.reelEntries[i] = caption;
            }
            story.enabled = false;
            AnimateFortuneReel(entry, 0);
        }

        private static void AnimateFortuneReel(JournalEntrance entry, float time)
        {
            if (time >= FortuneReelRollTime)
            {
                entry.reel.gameObject.SetActive(false);
                entry.rollingStory.enabled = true;
                return;
            }
            float remaining = 1 - Mathf.Clamp01(time / FortuneReelRollTime);
            float step = entry.reel.rect.height + 3;
            float start = (entry.reelEntries.Length - 1) * remaining * remaining;
            for (int i = 0; i < entry.reelEntries.Length; i++)
                entry.reelEntries[i].rectTransform.anchoredPosition = Vector2.up * ((start - i) * step);
        }

        private (CanvasGroup group, RectTransform label, RawImage glow, RectTransform[] glints) CreateJournalAnnouncement(JournalEntrance entry)
        {
            if (journalAnnouncementTexture == null)
                journalAnnouncementTexture = JournalEffectTexture(96, 96, (x, y) =>
                {
                    float falloff = Mathf.Clamp01(1 - new Vector2((x - .5f) * 2, (y - .5f) * 2).magnitude);
                    return falloff * falloff;
                });

            var center = MountPart(journalSealLayer, "Journal/CreateJournalAnnouncement/Journey result announcement");
            var group = center.GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            var glow = JournalEffectImage(center, "Journey result glow", journalAnnouncementTexture, false);
            var glints = new RectTransform[entry.Great ? 8 : 5];
            Color color = Color.Lerp(entry.SealLight, Color.white, .72f);
            for (int i = 0; i < glints.Length; i++)
                glints[i] = JourneySymbolAt(center, JournalSymbol.GreatFortune, color, .5f, .5f, 7 + 3 * (i % 3));
            string text = entry.Insight ? "领悟" : entry.Great ? "大吉" : "小吉";
            var label = MountText(center, "Journal/CreateJournalAnnouncement/label", value: text, size: entry.Great ? 42 : 35, color: color).rectTransform;
            return (group, label, glow, glints);
        }

        private static void AnimateJournalAnnouncement(JournalEntrance entry, float time)
        {
            var (group, label, glow, glints) = entry.announcement;
            bool great = entry.Great;
            float progress = Mathf.InverseLerp(.07f, great ? .88f : .61f, time);
            float entrance = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .25f));
            group.alpha = entrance * (1 - Mathf.InverseLerp(.62f, 1, progress));
            label.anchoredPosition = Vector2.up * Mathf.Lerp(-15, 7, entrance);
            label.localScale = Vector3.one * (Mathf.Lerp(.72f, 1, entrance) + .13f * Mathf.Sin(progress * Mathf.PI));
            glow.rectTransform.sizeDelta = new Vector2(great ? 435 : 340, great ? 186 : 150) * Mathf.Lerp(.63f, 1.24f, progress);
            glow.rectTransform.anchoredPosition = Vector2.down * 10;
            glow.color = entry.SealLight;
            for (int i = 0; i < glints.Length; i++)
            {
                float angle = (i + .35f) * 2 * Mathf.PI / glints.Length;
                float reach = Mathf.Lerp(.36f, 1, progress) * (great ? 136 : 112);
                var drift = entry.Insight ? Vector2.up * (26 * progress) : Vector2.zero;
                glints[i].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .44f) * reach + drift;
                glints[i].localScale = Vector3.one * (.85f + .23f * Mathf.Sin(progress * 8 + i));
            }
        }

        private JournalSealEffect CreateJournalSealEffect(JournalEntrance entry)
        {
            if (journalSweepTexture == null)
            {
                journalSweepTexture = JournalEffectTexture(144, 32, (x, y) =>
                {
                    float distance = Mathf.Abs(x - (.5f + (y - .5f) * .32f));
                    float core = Mathf.Clamp01(1 - distance / .034f);
                    float feather = Mathf.Clamp01(1 - distance / .16f);
                    return Mathf.Max(core * .92f, feather * feather * .64f);
                });
                journalPulseTexture = JournalEffectTexture(64, 64, (x, y) =>
                {
                    float radius = new Vector2(x - .5f, y - .5f).magnitude;
                    return Mathf.Clamp01(1 - Mathf.Abs(radius - .38f) / .065f);
                });
            }
            var sweep = JournalEffectImage(entry.body, "Journal light sweep", journalSweepTexture, true);
            var echo = entry.Great ? JournalEffectImage(entry.body, "Journal light sweep echo", journalSweepTexture, true) : null;
            var layer = MountPart(entry.body, "Journal/CreateJournalSealEffect/Journal seal effects");
            var pulse = JournalEffectImage(layer, "Journal seal pulse", journalPulseTexture, false);
            if (journalSealLayer == null)
                journalSealLayer = MountPart(overlay, "Journal/CreateJournalSealEffect/Journey result effects");
            var token = MountPanel(journalSealLayer, "Journal/CreateJournalSealEffect/token", color: JournalAccent(entry.kind));
            token.name = "Journal flying seal";
            token.sizeDelta = Vector2.one * (entry.Great ? 40 : 32);
            JourneySymbolAt(token, entry.SealSymbol, Color.white, .5f, .5f, entry.Great ? 24 : 19);
            var opacity = token.GetComponent<CanvasGroup>();
            opacity.alpha = 0;
            opacity.blocksRaycasts = false;
            return new JournalSealEffect { sweep = sweep, echo = echo, layer = layer, pulse = pulse, token = token, tokenOpacity = opacity };
        }

        private static Texture2D JournalEffectTexture(int width, int height, Func<float, float, float> coverage)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(coverage(x / (width - 1f), y / (height - 1f)) * 255));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private RawImage JournalEffectImage(RectTransform parent, string name, Texture2D texture, bool fill)
        {
            var rect = InstantiateUi(fill ? "Widgets/JournalSweep" : "Widgets/JournalEffect", parent);
            rect.name = name;
            if (fill) rect.SetAsFirstSibling();
            var image = rect.GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.clear;
            return image;
        }

        private static void AnimateJournalSweep(RawImage image, Color color, float time, float start, float end, float intensity)
        {
            float progress = Mathf.InverseLerp(start, end, time);
            image.uvRect = new Rect(Mathf.Lerp(.91f, -.87f, progress), 0, 1, 1);
            color.a = intensity * Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI));
            image.color = color;
        }

        private void AnimateJournalSeal(JournalEntrance entry, float time)
        {
            var effect = entry.effect;
            var tint = entry.SealLight;
            AnimateJournalSweep(effect.sweep, tint, time, .18f, entry.Great ? .80f : .60f, entry.Great ? .94f : .86f);
            if (effect.echo != null)
                AnimateJournalSweep(effect.echo, Tint(0xFF9295), time, entry.BadgeArrival - .36f, entry.BadgeArrival + .16f, .80f);

            var badge = (RectTransform)entry.badge.transform;
            Vector3 target = badge.TransformPoint(badge.rect.center);
            Vector2 finish = journalSealLayer.InverseTransformPoint(target);
            float launch = entry.BadgeArrival - .25f;
            float flight = Mathf.InverseLerp(launch, entry.BadgeArrival, time);
            var start = new Vector2(0, journalSealLayer.rect.height * -.31f);
            Vector2 position = Vector2.Lerp(start, finish, Mathf.SmoothStep(0, 1, flight));
            position.x += Mathf.Sin(flight * Mathf.PI) * 21;
            if (flight > 0 && flight < 1 && journalSealLayer.GetSiblingIndex() != journalSealLayer.parent.childCount - 1)
                journalSealLayer.SetAsLastSibling();
            effect.token.localPosition = position;
            effect.token.localRotation = Quaternion.Euler(0, 0, (1 - flight) * -25);
            bool visible = journalScroll.viewport.rect.Contains(journalScroll.viewport.InverseTransformPoint(target));
            effect.tokenOpacity.alpha = visible ? Mathf.InverseLerp(0, .24f, flight) * (1 - Mathf.InverseLerp(.86f, 1, flight)) : 0;

            float pulse = Mathf.InverseLerp(entry.BadgeArrival, entry.BadgeArrival + .30f, time);
            effect.pulse.rectTransform.localPosition = effect.layer.InverseTransformPoint(target);
            effect.pulse.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(24, entry.Great ? 90 : 66, pulse);
            tint.a = Mathf.Clamp01(Mathf.Sin(pulse * Mathf.PI));
            effect.pulse.color = tint;
        }

        private static float PlanJournalScroll(ScrollRect scroll, List<JournalEntrance> rows, float offset, float extent)
        {
            float height = Mathf.Max(1, scroll.viewport.rect.height);
            float next = 0, end = .54f;
            for (int i = 0; i < rows.Count; i++)
            {
                var entry = rows[i];
                float bottom = -(entry.row.localPosition.y + entry.row.rect.yMin);
                float target = i == rows.Count - 1 ? extent : Mathf.Clamp(bottom + JournalBottomTolerance - height, offset, extent);
                float distance = target - offset;
                entry.startTime = next;
                entry.scrollFrom = offset;
                entry.scrollTo = target;
                entry.scrollDuration = distance > 0 ? .20f : 0;
                offset = target;

                float effectEnd = entry.HasSeal ? entry.BadgeArrival + .30f : .36f;
                float detailEnd = entry.details.Length == 0 ? 0 : entry.DetailStart + JournalDetailDelay(entry.details.Length - 1) + .18f;
                float revealTime = Mathf.Max(entry.scrollDuration, Mathf.Max(effectEnd, detailEnd));
                end = Mathf.Max(end, next + revealTime);
                next += entry.HasSeal ? revealTime : Mathf.Max(.10f, entry.scrollDuration);
            }
            return end;
        }

        private IEnumerator AnimateJournal(ScrollRect scroll, List<JournalEntrance> rows, float start, float extent)
        {
            float duration = PlanJournalScroll(scroll, rows, start, extent);
            var transfers = BuildJournalTransfers(rows);
            var before = journalPreviousStats.GetValueOrDefault();
            var after = transfers.Count > 0 ? ReadJournalStats() : default;
            if (journalAction != null) journalAction.interactable = false;
            foreach (var entry in rows)
            {
                entry.group = entry.body.parent.GetComponent<CanvasGroup>();
                entry.group.alpha = 0;
                if (entry.badge != null) entry.badge.alpha = 0;
                foreach (var detail in entry.details) detail.group.alpha = 0;
                if (entry.rollingStory != null) CreateFortuneReel(entry);
                if (entry.HasSeal)
                {
                    entry.effect = CreateJournalSealEffect(entry);
                    if (game.Phase == Phase.Trail || game.Phase == Phase.Event)
                        entry.announcement = CreateJournalAnnouncement(entry);
                }
                else if (entry.kind == JournalCardKind.Setback)
                {
                    var warning = MountPart(entry.body, "Journal/AnimateJournal/warning");
                    warning.name = "Journal setback flash";
                    warning.SetAsFirstSibling();
                    entry.warningLine = warning.GetComponent<Image>();
                }
            }

            foreach (var transfer in transfers)
                duration = Mathf.Max(duration, transfer.start + JournalTransferDuration + JournalCounterDuration);
            int activeRow = 0;
            for (float elapsed = 0; elapsed < duration && scroll != null && !journalScrollTouched; elapsed += Mathf.Min(Time.unscaledDeltaTime, .07f))
            {
                while (activeRow + 1 < rows.Count && elapsed >= rows[activeRow + 1].startTime) activeRow++;
                var active = rows[activeRow];
                float progress = Mathf.InverseLerp(active.startTime, active.startTime + active.scrollDuration, elapsed);
                float offset = Mathf.Lerp(active.scrollFrom, active.scrollTo, progress);
                SetJournalOffset(scroll, offset, extent);
                for (int i = 0; i <= activeRow; i++) AnimateJournalEntry(rows[i], elapsed - rows[i].startTime);
                AnimateJournalTransfers(scroll, transfers, elapsed, before, after);
                yield return null;
            }

            if (scroll != null && !journalScrollTouched) SetJournalOffset(scroll, extent, extent);
            foreach (var entry in rows)
            {
                if (entry.body == null) continue;
                ((RectTransform)entry.group.transform).anchoredPosition = Vector2.zero;
                entry.group.transform.localScale = Vector3.one;
                entry.group.alpha = 1;
                if (entry.reel != null)
                {
                    entry.rollingStory.enabled = true;
                    Destroy(entry.reel.gameObject);
                }
                if (entry.badge != null)
                {
                    entry.badge.alpha = 1;
                    entry.badge.transform.localScale = Vector3.one;
                }
                foreach (var detail in entry.details)
                {
                    detail.group.alpha = 1;
                    detail.group.transform.localScale = Vector3.one;
                }
                if (entry.warningLine != null) Destroy(entry.warningLine.gameObject);
                if (entry.effect != null)
                {
                    Destroy(entry.effect.sweep.gameObject);
                    if (entry.effect.echo != null) Destroy(entry.effect.echo.gameObject);
                    Destroy(entry.effect.layer.gameObject);
                }
            }
            journalEntrance = null;
            FinishJournalPresentation();
        }

        private void AnimateJournalEntry(JournalEntrance entry, float time)
        {
            float appear = Mathf.SmoothStep(0, 1, Mathf.Clamp01(time / .23f));
            entry.group.alpha = appear;
            var card = (RectTransform)entry.group.transform;
            float nudge = entry.kind == JournalCardKind.Setback && time < .24f
                ? Mathf.Sin(time * 55) * (1 - time / .24f) * 9 : 0;
            card.anchoredPosition = new Vector2(nudge, -12 * (1 - appear));
            bool emphasize = entry.kind == JournalCardKind.Victory || entry.Insight;
            if (emphasize) card.localScale = Vector3.one * Mathf.Lerp(.96f, 1, appear);
            if (entry.reel != null)
            {
                if (entry.reel.gameObject.activeSelf) AnimateFortuneReel(entry, time);
                float scale = Mathf.SmoothStep(1, 1.05f, Mathf.InverseLerp(.10f, 1.40f, time));
                if (time >= FortuneReelRollTime)
                    scale = time < 1.60f ? Mathf.SmoothStep(1.05f, .70f, Mathf.InverseLerp(1.50f, 1.60f, time))
                        : Mathf.SmoothStep(.70f, 1, Mathf.InverseLerp(1.60f, 1.70f, time));
                card.localScale = Vector3.one * scale;
            }
            if (entry.badge != null)
            {
                float badgeStart = entry.HasSeal ? entry.BadgeArrival : entry.DetailStart;
                float progress = Mathf.InverseLerp(badgeStart, badgeStart + .15f, time);
                entry.badge.alpha = progress;
                entry.badge.transform.localScale = Vector3.one * (Mathf.Lerp(.84f, 1, progress) + .14f * Mathf.Sin(progress * Mathf.PI));
            }
            for (int i = 0; i < entry.details.Length; i++)
            {
                var detail = entry.details[i].group;
                detail.alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - entry.DetailStart - JournalDetailDelay(i)) / .18f));
                if (entry.Insight)
                    detail.transform.localScale = Vector3.one * (Mathf.Lerp(.76f, 1, detail.alpha) + .13f * Mathf.Sin(detail.alpha * Mathf.PI));
                else if (emphasize) detail.transform.localScale = Vector3.one * Mathf.Lerp(.84f, 1, detail.alpha);
            }
            if (entry.effect != null) AnimateJournalSeal(entry, time);
            if (entry.announcement.group != null) AnimateJournalAnnouncement(entry, time);
            if (entry.warningLine != null)
            {
                float burst = Mathf.InverseLerp(.04f, .34f, time);
                entry.warningLine.rectTransform.anchorMax = new Vector2(Mathf.Lerp(.996f, .008f, burst), .05f);
                entry.warningLine.color = new Color(JournalCoral.r, JournalCoral.g, JournalCoral.b, .88f * Mathf.Sin(burst * Mathf.PI));
            }
        }
    }
}
