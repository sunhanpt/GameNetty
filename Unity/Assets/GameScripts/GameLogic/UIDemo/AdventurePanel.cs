using System;
using System.Collections.Generic;
using System.Text;
using ET;
using UnityEngine;
using UnityEngine.UI;

public sealed class AdventurePanel : MonoBehaviour
{
    private enum CampPage { Adventure, Equipment, Talent, Companion, Challenge }

    private Font font;
    private Text title;
    private Text status;
    private Text body;
    private Text message;
    private AdventureLandscapeView landscape;
    private RawImage emblem;
    private ScrollRect reportScroll;
    private GameObject chapterPicker;
    private Text selectedChapter;
    private Button previousChapter;
    private Button nextChapter;
    private Button primary;
    private Button secondary;
    private Button abandon;
    private Button skipReport;
    private Button refresh;
    private GameObject campTabs;
    private readonly List<Button> campButtons = new();
    private Button back;
    private AdventureAction primaryAction;
    private AdventureAction secondaryAction;
    private EntityRef<ClientSenderComponent> sender;
    private G2C_Adventure snapshot;
    private bool busy;
    private int visibleRounds;
    private float nextRoundAt;
    private int chapterIndex;
    private int equipmentIndex;
    private CampPage campPage;
    private List<AdventureGrowthOption> growthOptions = new();
    private int optionIndex;
    private float automaticActionAt = float.PositiveInfinity;

    private bool PlayingReport => snapshot?.Phase == AdventurePhase.Result && visibleRounds < snapshot.Rounds.Count;

    public static void Open(Transform parent, Font font, ClientSenderComponent sender, G2C_Adventure snapshot, Action returnToLogin)
    {
        GameObject view = new GameObject("AdventurePanel", typeof(RectTransform), typeof(Image));
        view.transform.SetParent(parent, false);
        AdventurePanel panel = view.AddComponent<AdventurePanel>();
        panel.font = font;
        panel.sender = sender;
        panel.BuildView(returnToLogin);
        panel.Show(snapshot, false);
    }

    private void BuildView(Action returnToLogin)
    {
        RectTransform rect = (RectTransform)transform;
        AdventureUi.Stretch(rect);
        GetComponent<Image>().color = AdventureUi.Background;
        RectTransform content = AdventureViewport.Create(transform);
        content.gameObject.AddComponent<Image>().color = AdventureUi.Surface;
        AdventureUi.Stack(content.gameObject, new RectOffset(20, 20, 18, 18), 9f);

        title = AdventureUi.Label(content, font, "Title", 32, TextAnchor.MiddleLeft);
        title.color = AdventureUi.Ink;
        status = AdventureUi.Label(content, font, "Status", 19, TextAnchor.MiddleLeft);
        status.color = AdventureUi.Gold;

        campTabs = CreateRow("CampTabs", content).gameObject;
        string[] labels = { "营地", "装备", "天赋", "旅伴", "挑战" };
        for (int i = 0; i < labels.Length; ++i)
        {
            CampPage page = (CampPage)i;
            Button button = CreateButton(labels[i], () =>
            {
                message.text = string.Empty;
                ShowCamp(page);
            }, campTabs.transform);
            button.GetComponentInChildren<Text>().fontSize = 16;
            campButtons.Add(button);
        }

        HorizontalLayoutGroup chapterLayout = CreateRow("ChapterPicker", content);
        chapterPicker = chapterLayout.gameObject;
        chapterLayout.childForceExpandWidth = false;
        previousChapter = CreateButton("上一项", () => MoveChoice(-1), chapterPicker.transform);
        previousChapter.GetComponent<LayoutElement>().preferredWidth = 76;
        selectedChapter = AdventureUi.Label(chapterPicker.transform, font, "SelectedChapter", 18, TextAnchor.MiddleCenter);
        selectedChapter.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        nextChapter = CreateButton("下一项", () => MoveChoice(1), chapterPicker.transform);
        nextChapter.GetComponent<LayoutElement>().preferredWidth = 76;
        landscape = AdventureLandscapeView.Create(content);
        landscape.GetComponent<LayoutElement>().preferredHeight = 188;
        emblem = new GameObject("ItemArt", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement)).GetComponent<RawImage>();
        emblem.transform.SetParent(content, false);
        emblem.raycastTarget = false;
        // Keeping a square drawing area preserves the item silhouettes on wide screens.
        LayoutElement emblemLayout = emblem.GetComponent<LayoutElement>();
        emblemLayout.preferredHeight = 142;
        emblemLayout.preferredWidth = 142;
        HorizontalLayoutGroup emblemRow = CreateRow("ItemDisplay", content);
        emblemRow.childForceExpandWidth = false;
        emblemRow.childAlignment = TextAnchor.MiddleCenter;
        emblemRow.GetComponent<LayoutElement>().preferredHeight = 142;
        emblem.transform.SetParent(emblemRow.transform, false);

        GameObject logView = new GameObject("RoundLog", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        logView.transform.SetParent(content, false);
        logView.GetComponent<Image>().color = AdventureUi.Background;
        logView.GetComponent<LayoutElement>().minHeight = 68;
        logView.GetComponent<LayoutElement>().flexibleHeight = 1f;
        RectTransform viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(logView.transform, false);
        AdventureUi.Stretch(viewport);
        viewport.offsetMin = new Vector2(14, 10);
        viewport.offsetMax = new Vector2(-14, -10);
        body = AdventureUi.Label(viewport, font, "Content", 20, TextAnchor.UpperLeft);
        body.rectTransform.anchorMin = new Vector2(0f, 1f);
        body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.rectTransform.offsetMin = body.rectTransform.offsetMax = Vector2.zero;
        body.lineSpacing = 1.18f;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        reportScroll = logView.GetComponent<ScrollRect>();
        reportScroll.viewport = viewport;
        reportScroll.content = body.rectTransform;
        reportScroll.horizontal = false;
        reportScroll.movementType = ScrollRect.MovementType.Clamped;
        reportScroll.scrollSensitivity = 34f;

        message = AdventureUi.Label(content, font, "Feedback", 17, TextAnchor.MiddleLeft);
        message.color = AdventureUi.Warning;
        message.gameObject.AddComponent<LayoutElement>().minHeight = 27;
        primary = CreateButton("继续", () => Submit(primaryAction).Coroutine(), content);
        primary.GetComponent<LayoutElement>().preferredHeight = 52;
        AdventureUi.StyleButton(primary, primary.GetComponentInChildren<Text>(), true);
        secondary = CreateButton("备选行动", () => Submit(secondaryAction).Coroutine(), content);

        HorizontalLayoutGroup footer = CreateRow("SessionActions", content);
        refresh = CreateButton("刷新进度", () => Submit(AdventureAction.Snapshot).Coroutine(), footer.transform);
        abandon = CreateButton("放弃本局", () => Submit(AdventureAction.Abandon).Coroutine(), footer.transform);
        skipReport = CreateButton("查看完整战报", ShowFullReport, footer.transform);
        back = CreateButton("返回登录", () =>
        {
            gameObject.SetActive(false);
            returnToLogin();
            Destroy(gameObject);
        }, footer.transform);
    }

    private static HorizontalLayoutGroup CreateRow(string name, Transform parent)
    {
        GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 42;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        return layout;
    }

    private Button CreateButton(string caption, UnityEngine.Events.UnityAction onClick, Transform parent)
    {
        GameObject item = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        item.transform.SetParent(parent, false);
        item.GetComponent<LayoutElement>().preferredHeight = 44f;
        Text label = AdventureUi.Label(item.transform, font, "Label", 19, TextAnchor.MiddleCenter);
        label.text = caption;
        AdventureUi.Stretch(label.rectTransform);
        Button button = item.GetComponent<Button>();
        AdventureUi.StyleButton(button, label, false);
        button.onClick.AddListener(onClick);
        return button;
    }

    private async ETTask Submit(AdventureAction action)
    {
        if (busy || PlayingReport) return;
        automaticActionAt = float.PositiveInfinity;
        busy = true;
        RefreshButtons();
        message.color = AdventureUi.Muted;
        message.text = "正在提交…";
        try
        {
            ClientSenderComponent connection = sender;
            if (connection == null) throw new InvalidOperationException("Adventure session is unavailable.");
            C2G_Adventure request = C2G_Adventure.Create();
            request.Action = action;
            request.ExpectedRevision = snapshot.Revision;
            if (action == AdventureAction.Begin)
            {
                request.ChapterId = CurrentChapter?.Id ?? snapshot.ChapterId;
            }
            else if (action is AdventureAction.BuyEquipment or AdventureAction.Equip or AdventureAction.Unequip or AdventureAction.UpgradeEquipment)
            {
                request.EquipmentId = CurrentEquipment?.Id ?? 0;
            }
            else if (action is AdventureAction.TrainTalent or AdventureAction.RecruitCompanion or AdventureAction.AssignCompanion or AdventureAction.DismissCompanion)
            {
                request.TargetId = CurrentGrowth?.Id ?? 0;
            }
            else if (action == AdventureAction.BeginChallenge)
            {
                request.TargetId = CurrentChallenge?.Id ?? 0;
            }
            G2C_Adventure response = await connection.Call(request, false) as G2C_Adventure;
            if (this == null) return;
            if (response == null || response.PlayerId == 0)
            {
                ShowFailure(response?.Message ?? "连接已断开，请重新登录。");
                return;
            }

            Show(response, response.Error == 0 && action == AdventureAction.Fight);
            if (response.Error != 0)
            {
                automaticActionAt = float.PositiveInfinity;
                ShowFailure(response.Message);
            }
            else
            {
                message.text = action == AdventureAction.Settle ? $"已结算 · +{response.LastSettlementCoins} 金币" : string.Empty;
            }
        }
        catch (Exception error)
        {
            if (this != null) ShowFailure("请求失败，可重试或重新登录。");
            Debug.LogException(error);
        }
        finally
        {
            if (this != null)
            {
                busy = false;
                RefreshButtons();
            }
        }
    }

    private void Show(G2C_Adventure next, bool playReport)
    {
        int selectedChapterId = CurrentChapter?.Id ?? (snapshot?.ChapterId ?? next.ChapterId);
        int equipmentId = CurrentEquipment?.Id ?? 0;
        snapshot = next;
        chapterIndex = Mathf.Max(0, next.Chapters.FindIndex(chapter => chapter.Id == selectedChapterId));
        equipmentIndex = Mathf.Max(0, next.Equipment.FindIndex(item => item.Id == equipmentId));
        visibleRounds = playReport ? 0 : next.Rounds.Count;
        nextRoundAt = Time.unscaledTime + 0.2f;
        status.text = $"金币 {next.Coins}";
        if (next.ChallengeId != 0) status.text += "    ·    单人挑战";
        automaticActionAt = float.PositiveInfinity;
        chapterPicker.SetActive(false);
        primary.gameObject.SetActive(true);
        campTabs.SetActive(next.Phase == AdventurePhase.Camp);
        secondary.gameObject.SetActive(next.Phase == AdventurePhase.Event);
        abandon.gameObject.SetActive(next.Phase == AdventurePhase.Trail || next.Phase == AdventurePhase.Event || next.Phase == AdventurePhase.Battle);
        refresh.gameObject.SetActive(next.Phase == AdventurePhase.Camp);
        ShowEmblem(null);
        if (next.Phase != AdventurePhase.Camp) landscape.ShowScene(next.Scene?.ResourceKey, next.Phase, next.Scene);
        switch (next.Phase)
        {
            case AdventurePhase.Camp:
                ShowCamp(campPage);
                break;
            case AdventurePhase.Trail:
                title.text = next.ChapterName;
                body.text = next.Scene?.TravelText ?? "你沿着小路继续前行。";
                SetPrimary(AdventureAction.Advance, "前往路边事件");
                automaticActionAt = Time.unscaledTime + AdventureLandscapeView.TravelSeconds;
                break;
            case AdventurePhase.Event:
                title.text = next.Scene?.EventTitle ?? "旅途事件";
                body.text = next.Scene?.EventText ?? "你发现了可以补充装备的物资。";
                SetPrimary(AdventureAction.TakeSupplies, next.Scene?.TakeLabel ?? "检查补给");
                secondaryAction = AdventureAction.SkipSupplies;
                secondary.GetComponentInChildren<Text>().text = next.Scene?.SkipLabel ?? "直接出发";
                break;
            case AdventurePhase.Battle:
                title.text = $"遭遇 · {next.EnemyName}";
                body.text = next.Scene?.EncounterText ?? $"{next.EnemyName}挡住了去路。";
                if (next.Scene != null) body.text += $"\n初始生命 · 你 {next.Scene.PlayerHealth} / 对手 {next.Scene.EnemyHealth}";
                if (!string.IsNullOrEmpty(next.Scene?.CompanionName)) body.text += $"\n随行旅伴 · {next.Scene.CompanionName}";
                SetPrimary(AdventureAction.Fight, "开始战斗");
                automaticActionAt = Time.unscaledTime + AdventureLandscapeView.ApproachSeconds;
                break;
            case AdventurePhase.Result:
                title.text = "战报";
                SetPrimary(AdventureAction.Settle, "结算并返回营地");
                if (visibleRounds > 0) landscape.ApplyRound(next.Rounds[visibleRounds - 1], false);
                RenderReport();
                break;
        }
        ScrollToTop();
        RefreshButtons();
    }

    private AdventureChapterOption CurrentChapter => snapshot != null && chapterIndex < snapshot.Chapters.Count
        ? snapshot.Chapters[chapterIndex]
        : null;

    private AdventureEquipmentOption CurrentEquipment => snapshot != null && equipmentIndex < snapshot.Equipment.Count
        ? snapshot.Equipment[equipmentIndex]
        : null;

    private AdventureGrowthOption CurrentGrowth => optionIndex < growthOptions.Count ? growthOptions[optionIndex] : null;

    private AdventureChallengeOption CurrentChallenge => optionIndex < snapshot.Challenges.Count ? snapshot.Challenges[optionIndex] : null;

    private int ChoiceCount => campPage switch
    {
        CampPage.Adventure => snapshot.Chapters.Count,
        CampPage.Equipment => snapshot.Equipment.Count,
        CampPage.Challenge => snapshot.Challenges.Count,
        _ => growthOptions.Count
    };

    private void MoveChoice(int offset)
    {
        if (busy || snapshot.Phase != AdventurePhase.Camp) return;
        int count = ChoiceCount;
        if (count < 2) return;
        if (campPage == CampPage.Adventure) chapterIndex = (chapterIndex + offset + count) % count;
        else if (campPage == CampPage.Equipment) equipmentIndex = (equipmentIndex + offset + count) % count;
        else optionIndex = (optionIndex + offset + count) % count;
        ShowCamp(campPage);
    }

    private void ShowCamp(CampPage page)
    {
        if (campPage != page) optionIndex = 0;
        campPage = page;
        if (page is CampPage.Talent or CampPage.Companion)
        {
            AdventureGrowthKind kind = page == CampPage.Talent ? AdventureGrowthKind.Talent : AdventureGrowthKind.Companion;
            growthOptions = snapshot.Growth.FindAll(item => item.Kind == kind);
        }
        int count = ChoiceCount;
        optionIndex = Mathf.Clamp(optionIndex, 0, Mathf.Max(0, count - 1));
        chapterPicker.SetActive(count > 1);
        primary.gameObject.SetActive(page == CampPage.Adventure || count > 0);
        secondary.gameObject.SetActive(false);
        ShowEmblem(null);
        switch (page)
        {
            case CampPage.Adventure:
                title.text = "营地";
                SetPrimary(AdventureAction.Begin, "开启冒险");
                ShowChapter();
                break;
            case CampPage.Equipment:
                title.text = "背包与装备";
                if (CurrentEquipment != null) ShowEquipment();
                break;
            case CampPage.Talent:
            case CampPage.Companion:
                title.text = page == CampPage.Talent ? "天赋" : "旅伴";
                if (CurrentGrowth != null) ShowGrowth(CurrentGrowth);
                break;
            case CampPage.Challenge:
                title.text = "单人挑战";
                if (CurrentChallenge != null) ShowChallenge(CurrentChallenge);
                break;
        }
        if (page != CampPage.Adventure && count == 0)
        {
            landscape.ShowScene(null, AdventurePhase.Camp, null);
            body.text = "当前没有可用项目。";
        }
        for (int i = 0; i < campButtons.Count; ++i)
        {
            Button tab = campButtons[i];
            AdventureUi.StyleButton(tab, tab.GetComponentInChildren<Text>(), i == (int)campPage);
        }
        ScrollToTop();
        RefreshButtons();
    }

    private void ShowGrowth(AdventureGrowthOption item)
    {
        selectedChapter.text = item.Name;
        landscape.ShowScene(null, AdventurePhase.Camp, null);
        ShowEmblem(item.Kind == AdventureGrowthKind.Talent ? "Talent" : "Companion");
        if (item.Kind == AdventureGrowthKind.Talent)
        {
            body.text = $"{item.Name}\n等级 {item.Rank}/{item.MaxRank}\n每级 · 攻击 +{item.AttackPerRank}  生命 +{item.HealthPerRank}";
            primary.gameObject.SetActive(item.Rank < item.MaxRank);
            if (primary.gameObject.activeSelf) SetPrimary(AdventureAction.TrainTalent, $"训练 · {item.Cost} 金币");
            else body.text += "\n已达到最高等级。";
            return;
        }

        string statusText = item.Rank == 0 ? "尚未招募" : item.Assigned ? "已随行" : "已招募 · 未随行";
        body.text = $"{item.Name}\n{statusText}\n随行时生命 +{item.HealthPerRank}，每两回合协助攻击一次，造成 {item.AttackPerRank} 伤害。";
        if (item.Rank == 0) SetPrimary(AdventureAction.RecruitCompanion, $"招募 · {item.Cost} 金币");
        else if (item.Assigned) SetPrimary(AdventureAction.DismissCompanion, "停止随行");
        else SetPrimary(AdventureAction.AssignCompanion, "安排随行");
    }

    private void ShowChallenge(AdventureChallengeOption option)
    {
        selectedChapter.text = option.Name;
        landscape.ShowScene(option.ResourceKey, AdventurePhase.Camp, null);
        body.text = $"{option.Name}\n胜利奖励 · {option.RewardCoins} 金币\n今日剩余 · {option.AttemptsLeft}/{option.DailyAttempts}\n每天 UTC 00:00 刷新。";
        if (!option.Unlocked) body.text += "\n完成一次普通冒险结算后解锁。";
        if (option.Unlocked && option.AttemptsLeft == 0) body.text += "\n今日次数已用完，次日刷新后可再次挑战。";
        primary.gameObject.SetActive(option.Unlocked && option.AttemptsLeft > 0);
        if (primary.gameObject.activeSelf) SetPrimary(AdventureAction.BeginChallenge, "开始挑战");
    }

    private void ShowEquipment()
    {
        AdventureEquipmentOption item = CurrentEquipment;
        selectedChapter.text = item.Name;
        landscape.ShowScene(null, AdventurePhase.Camp, null);
        ShowEmblem(item.Slot == AdventureEquipmentSlot.Weapon ? "Weapon" : "Armor");
        string slot = item.Slot == AdventureEquipmentSlot.Weapon ? "武器" : "护具";
        string ownership = item.Rank == 0 ? "未拥有" : $"强化 {item.Rank}/{item.MaxRank} · {(item.Equipped ? "已穿戴" : "未穿戴")}";
        body.text = $"{item.Name} · {slot}\n{ownership}\n\n攻击 +{item.Attack}  生命 +{item.Health}";

        bool canUpgrade = item.Rank > 0 && item.Rank < item.MaxRank;
        secondary.gameObject.SetActive(canUpgrade);
        if (canUpgrade)
        {
            secondaryAction = AdventureAction.UpgradeEquipment;
            secondary.GetComponentInChildren<Text>().text = $"强化 · {item.UpgradeCost} 金币";
            body.text += $"\n下一级 · 攻击 +{item.NextAttack}  生命 +{item.NextHealth}";
        }

        if (item.Rank == 0) SetPrimary(AdventureAction.BuyEquipment, $"购买 · {item.Price} 金币");
        else if (item.Equipped) SetPrimary(AdventureAction.Unequip, "卸下");
        else SetPrimary(AdventureAction.Equip, "穿戴");
    }

    private void ShowChapter()
    {
        AdventureChapterOption chapter = CurrentChapter;
        string chapterName = chapter?.Name ?? snapshot.ChapterName;
        selectedChapter.text = chapterName;
        landscape.ShowScene(chapter?.ResourceKey ?? snapshot.Scene?.ResourceKey, AdventurePhase.Camp, snapshot.Scene);
        body.text = $"可出发前往{chapterName}。";
        if (chapter != null) body.text += $"\n胜利奖励 · {chapter.RewardCoins} 金币";
        if (snapshot.LastSettledRunId != 0)
        {
            string result = snapshot.LastSettlementWon ? "胜利" : "战败";
            body.text += $"\n\n上次结算 · {result} · +{snapshot.LastSettlementCoins} 金币";
        }
    }

    private void Update()
    {
        if (!busy && Time.unscaledTime >= automaticActionAt)
        {
            Submit(primaryAction).Coroutine();
            return;
        }

        if (!PlayingReport || Time.unscaledTime < nextRoundAt) return;
        ++visibleRounds;
        landscape.ApplyRound(snapshot.Rounds[visibleRounds - 1], true);
        nextRoundAt = Time.unscaledTime + 0.25f;
        RenderReport();
        if (!PlayingReport) ScrollToEnd();
        RefreshButtons();
    }

    private void RenderReport()
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < visibleRounds; ++i)
        {
            AdventureRound round = snapshot.Rounds[i];
            text.AppendLine($"第 {round.Number} 回合 · 攻击 {round.PlayerDamage - round.CompanionDamage} / 受击 {round.EnemyDamage}");
            if (round.CompanionDamage > 0) text.AppendLine($"{snapshot.Scene?.CompanionName ?? "旅伴"}助战，造成 {round.CompanionDamage} 伤害");
            text.AppendLine($"剩余生命 · 你 {round.PlayerHealth} / 对手 {round.EnemyHealth}");
            text.AppendLine();
        }
        if (!PlayingReport)
        {
            text.Append(snapshot.Won ? $"\n胜利 · 待领取 {snapshot.RewardCoins} 金币" : "\n战败 · 本局无奖励");
        }
        body.text = text.ToString();
    }

    private void ShowFullReport()
    {
        if (!PlayingReport) return;
        visibleRounds = snapshot.Rounds.Count;
        landscape.ApplyRound(snapshot.Rounds[visibleRounds - 1], false);
        RenderReport();
        ScrollToEnd();
        RefreshButtons();
    }

    private void ShowEmblem(string resourceName)
    {
        emblem.texture = resourceName == null ? null : Resources.Load<Texture2D>($"Adventure/Interface/{resourceName}");
        emblem.transform.parent.gameObject.SetActive(emblem.texture != null);
    }

    private void ShowFailure(string text)
    {
        message.color = AdventureUi.Warning;
        message.text = string.IsNullOrWhiteSpace(text) ? "操作未完成，请重试。" : text;
    }

    private void ScrollToTop()
    {
        reportScroll.StopMovement();
        body.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void ScrollToEnd()
    {
        Canvas.ForceUpdateCanvases();
        reportScroll.StopMovement();
        reportScroll.verticalNormalizedPosition = 0f;
    }

    private void SetPrimary(AdventureAction action, string caption)
    {
        primaryAction = action;
        primary.GetComponentInChildren<Text>().text = caption;
    }

    private void RefreshButtons()
    {
        primary.interactable = secondary.interactable = abandon.interactable = !busy && !PlayingReport;
        previousChapter.interactable = nextChapter.interactable = !busy;
        foreach (Button tab in campButtons) tab.interactable = !busy;
        refresh.interactable = !busy;
        skipReport.gameObject.SetActive(PlayingReport);
        back.interactable = !busy;
    }
}
