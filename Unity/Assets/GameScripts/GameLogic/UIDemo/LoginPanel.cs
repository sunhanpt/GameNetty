using System;
using System.Linq;
using System.Threading.Tasks;
using CapybaraRebuild;
using ET;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Scene = ET.Scene;
using UnityScene = UnityEngine.SceneManagement.Scene;

public class LoginPanel : MonoBehaviour
{
    public InputField account;
    public InputField password;
    public Button loginBtn;
    
    private Text feedback;
    private Text loginLabel;
    private Button demoButton;
    private bool loggingIn;
    private UnityScene loginScene;
    private UnityScene journeyScene;
    private GameObject[] hiddenRoots = Array.Empty<GameObject>();

    private void Start()
    {
        BuildView();
        loginBtn.onClick.AddListener(() => LoginAsync(false).Coroutine());
        demoButton.onClick.AddListener(() => LoginAsync(true).Coroutine());
        account.onEndEdit.AddListener(_ =>
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) LoginAsync(false).Coroutine();
        });
    }

    private void BuildView()
    {
        Image background = GetComponent<Image>();
        background.sprite = null;
        background.color = AdventureUi.Background;
        background.raycastTarget = false;

        loginLabel = loginBtn.GetComponentInChildren<Text>();
        Font font = loginLabel.font;
        RectTransform content = AdventureViewport.Create(transform);
        Image card = content.gameObject.AddComponent<Image>();
        card.color = AdventureUi.Surface;
        card.raycastTarget = false;
        VerticalLayoutGroup layout = AdventureUi.Stack(content.gameObject, new RectOffset(28, 28, 26, 26), 12f);
        layout.childAlignment = TextAnchor.MiddleCenter;

        Text title = AdventureUi.Label(content, font, "Title", 46, TextAnchor.MiddleLeft);
        title.text = "卡皮巴拉的旅程";
        title.color = AdventureUi.Gold;
        var artwork = new GameObject("Journey artwork", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
        artwork.transform.SetParent(content, false);
        var cover = artwork.GetComponent<RawImage>();
        cover.texture = Resources.Load<Texture2D>("GameIcon");
        cover.raycastTarget = false;
        var artworkLayout = artwork.GetComponent<LayoutElement>();
        artworkLayout.preferredHeight = 180;
        artworkLayout.preferredWidth = 180;
        layout.childForceExpandWidth = false;

        Text accountLabel = AdventureUi.Label(content, font, "AccountLabel", 19, TextAnchor.MiddleLeft);
        accountLabel.text = "冒险账号";
        account.transform.SetParent(content, false);
        account.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
        Image inputBackground = account.GetComponent<Image>();
        inputBackground.sprite = null;
        inputBackground.color = AdventureUi.Background;
        account.textComponent.fontSize = 22;
        account.textComponent.color = AdventureUi.Ink;
        if (account.placeholder is Text placeholder)
        {
            placeholder.fontSize = 20;
            placeholder.color = AdventureUi.Muted;
            placeholder.text = "输入账号";
        }
        password.gameObject.SetActive(false);

        loginBtn.transform.SetParent(content, false);
        loginBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        loginLabel.text = "进入营地";
        loginLabel.fontSize = 22;
        AdventureUi.StyleButton(loginBtn, loginLabel, true);
        demoButton = new GameObject("Framework demo", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement))
            .GetComponent<Button>();
        demoButton.transform.SetParent(content, false);
        demoButton.GetComponent<LayoutElement>().preferredHeight = 46;
        var demoLabel = AdventureUi.Label(demoButton.transform, font, "Label", 18, TextAnchor.MiddleCenter);
        demoLabel.text = "打开《风灯旅记》";
        AdventureUi.Stretch(demoLabel.rectTransform);
        AdventureUi.StyleButton(demoButton, demoLabel, false);
        foreach (var item in new[] { account.GetComponent<LayoutElement>(), loginBtn.GetComponent<LayoutElement>(), demoButton.GetComponent<LayoutElement>() })
            item.flexibleWidth = 1;
        feedback = AdventureUi.Label(content, font, "LoginFeedback", 18, TextAnchor.UpperLeft);
        var feedbackLayout = feedback.gameObject.AddComponent<LayoutElement>();
        feedbackLayout.preferredHeight = 50;
        feedbackLayout.flexibleWidth = 1;
        ResetFeedback();
    }

    private async ETTask LoginAsync(bool frameworkDemo)
    {
        if (loggingIn) return;
        if (Init.Root == null)
        {
            ShowError("客户端正在初始化，请稍后重试。");
            return;
        }
        if (string.IsNullOrWhiteSpace(account.text))
        {
            ShowError("请输入冒险账号。");
            account.ActivateInputField();
            return;
        }

        loggingIn = true;
        account.interactable = false;
        loginBtn.interactable = false;
        demoButton.interactable = false;
        loginLabel.text = "连接中…";
        feedback.color = AdventureUi.Muted;
        feedback.text = "正在连接服务器…";
        Scene root = Init.Root;
        try
        {
            await LoginHelper.Login(root, account.text.Trim(), string.Empty);
            if (this == null) return;
            feedback.text = "正在读取冒险进度…";
            ClientSenderComponent sender = root.GetComponent<ClientSenderComponent>();
            if (frameworkDemo)
            {
                C2G_Adventure request = C2G_Adventure.Create();
                G2C_Adventure snapshot = await sender.Call(request, false) as G2C_Adventure;
                if (snapshot == null || snapshot.Error != 0 || snapshot.PlayerId == 0)
                    throw new InvalidOperationException(snapshot?.Message ?? "无法读取冒险快照。");
                if (this == null) return;
                AdventurePanel.Open(transform.parent, feedback.font, sender, snapshot, ReturnToLogin);
                gameObject.SetActive(false);
            }
            else
            {
                var catalog = AdventureCatalog.Load();
                Task<AdventureReply> Send(AdventureCommand command, long revision, string commandId) =>
                    SendAdventure(sender, catalog.Version, command, revision, commandId);
                var snapshot = await Send(new AdventureCommand(JourneyAction.Snapshot), 0, null);
                if (snapshot.Error != 0)
                {
                    root.RemoveComponent<ClientSenderComponent>();
                    ShowError(snapshot.Message);
                    return;
                }
                if (this == null) return;
                await OpenJourney(catalog, snapshot, Send);
            }
            feedback.text = string.Empty;
        }
        catch (Exception error)
        {
            root.RemoveComponent<ClientSenderComponent>();
            if (this != null) ShowError("无法登录或读取进度。请确认服务端已启动，再试一次。");
            Debug.LogException(error);
        }
        finally
        {
            if (this != null)
            {
                loggingIn = false;
                account.interactable = true;
                loginBtn.interactable = true;
                demoButton.interactable = true;
                loginLabel.text = "进入营地";
            }
        }
    }

    private static async Task<AdventureReply> SendAdventure(ClientSenderComponent sender, string catalogVersion,
        AdventureCommand command, long revision, string commandId)
    {
        if (sender.IsDisposed) throw new InvalidOperationException("登录会话已关闭。");
        using C2G_CapybaraAdventure request = C2G_CapybaraAdventure.Create();
        request.CatalogVersion = catalogVersion;
        request.CommandId = commandId;
        request.ExpectedRevision = revision;
        request.Action = (int)command.Action;
        request.Id = command.Id;
        request.Option = command.Option;
        request.Currency = command.Currency;
        request.Cost = command.Cost;
        request.Draws = command.Draws;
        using G2C_CapybaraAdventure response = await sender.Call(request, false) as G2C_CapybaraAdventure;
        if (response == null) throw new InvalidOperationException("服务器未返回旅程快照。");
        return new AdventureReply(response.Error, response.Message, response.Revision, response.GameDayTicks,
            response.StateJson, response.ResultJson);
    }

    private async Task OpenJourney(AdventureCatalog catalog, AdventureReply snapshot,
        Func<AdventureCommand, long, string, Task<AdventureReply>> send)
    {
        loginScene = gameObject.scene;
        hiddenRoots = loginScene.GetRootGameObjects().Where(root => root.activeSelf).ToArray();
        foreach (var root in hiddenRoots) root.SetActive(false);

        try
        {
            await SceneOperation(SceneManager.LoadSceneAsync("Adventure", LoadSceneMode.Additive));
            journeyScene = SceneManager.GetSceneByName("Adventure");
            SceneManager.SetActiveScene(journeyScene);
            var screen = journeyScene.GetRootGameObjects().Select(root => root.GetComponentInChildren<AdventureScreen>())
                .FirstOrDefault(view => view != null);
            if (screen == null) throw new InvalidOperationException("Adventure scene has no AdventureScreen.");
            screen.Bind(catalog, snapshot, send, () => CloseJourney().Coroutine());
        }
        catch
        {
            await RestoreLoginScene();
            throw;
        }
    }

    private async ETTask CloseJourney()
    {
        await RestoreLoginScene();
        ReturnToLogin();
    }

    private async Task RestoreLoginScene()
    {
        SceneManager.SetActiveScene(loginScene);
        if (journeyScene.IsValid() && journeyScene.isLoaded)
            await SceneOperation(SceneManager.UnloadSceneAsync(journeyScene));
        foreach (var root in hiddenRoots)
            if (root != null) root.SetActive(true);
        hiddenRoots = Array.Empty<GameObject>();
        journeyScene = default;
    }

    private static Task SceneOperation(AsyncOperation operation)
    {
        if (operation == null) throw new InvalidOperationException("Unity 无法切换旅程场景。");
        var completed = new TaskCompletionSource<bool>();
        operation.completed += _ => completed.TrySetResult(true);
        return completed.Task;
    }

    private void ReturnToLogin()
    {
        Init.Root?.RemoveComponent<ClientSenderComponent>();
        gameObject.SetActive(true);
        ResetFeedback();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(account.gameObject);
    }

    private void ResetFeedback()
    {
        feedback.color = AdventureUi.Muted;
        feedback.text = "使用同一账号可继续上次保存的冒险。";
    }

    private void ShowError(string text)
    {
        feedback.color = AdventureUi.Warning;
        feedback.text = text;
    }
}
