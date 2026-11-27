using ET;
using UnityEngine;
using UnityEngine.UI;

public sealed class AdventureLandscapeView : MonoBehaviour
{
    public const float TravelSeconds = 2.8f;
    public const float ApproachSeconds = 1.1f;

    private RawImage far;
    private RawImage middle;
    private RawImage terrain;
    private RawImage scout;
    private RawImage opponent;
    private RawImage companion;
    private Image scoutHealth;
    private Image opponentHealth;
    private string resourceKey;
    private AdventurePhase phase;
    private int scoutMaxHealth;
    private int opponentMaxHealth;
    private float scroll;
    private float phaseStartedAt;
    private float impactStart;
    private bool scoutHit;
    private bool opponentHit;
    private bool companionHit;

    public static AdventureLandscapeView Create(Transform parent)
    {
        GameObject viewport = new GameObject("AdventureLandscape", typeof(RectTransform), typeof(RectMask2D), typeof(LayoutElement));
        viewport.transform.SetParent(parent, false);
        LayoutElement layout = viewport.GetComponent<LayoutElement>();
        layout.minHeight = 68;
        layout.preferredHeight = 124;
        AdventureLandscapeView view = viewport.AddComponent<AdventureLandscapeView>();
        view.far = view.CreateLayer("Far");
        view.middle = view.CreateLayer("Middle");
        view.terrain = view.CreateLayer("Terrain");
        view.companion = view.CreateActor("Companion", 0.17f, 38f);
        view.scout = view.CreateActor("Scout", 0.29f, 70f);
        view.opponent = view.CreateActor("Opponent", 0.73f, 70f);
        view.scout.texture = Resources.Load<Texture2D>("Adventure/Scout");
        view.companion.texture = Resources.Load<Texture2D>("Adventure/Interface/CompanionActor");
        view.scoutHealth = CreateHealthBar(view.scout.rectTransform, new Color(0.41f, 0.79f, 0.53f));
        view.opponentHealth = CreateHealthBar(view.opponent.rectTransform, new Color(0.92f, 0.47f, 0.38f));
        return view;
    }

    public void ShowScene(string key, AdventurePhase nextPhase, AdventureSceneInfo scene)
    {
        bool hasMap = !string.IsNullOrWhiteSpace(key);
        if (hasMap && resourceKey != key)
        {
            resourceKey = key;
            scroll = 0f;
            LoadLayer(far);
            LoadLayer(middle);
            LoadLayer(terrain);
            opponent.texture = Resources.Load<Texture2D>($"Adventure/{key}/Opponent");
        }
        gameObject.SetActive(hasMap && far.texture != null);
        phase = nextPhase;
        phaseStartedAt = Time.unscaledTime;
        bool battle = phase == AdventurePhase.Battle || phase == AdventurePhase.Result;
        scout.enabled = scout.texture != null && phase != AdventurePhase.Camp;
        companion.enabled = scout.enabled && companion.texture != null && !string.IsNullOrWhiteSpace(scene?.CompanionName);
        opponent.enabled = opponent.texture != null && battle;
        scoutHealth.transform.parent.gameObject.SetActive(scout.enabled && battle);
        opponentHealth.transform.parent.gameObject.SetActive(opponent.enabled);
        scoutMaxHealth = Mathf.Max(1, scene?.PlayerHealth ?? 0);
        opponentMaxHealth = Mathf.Max(1, scene?.EnemyHealth ?? 0);
        SetHealth(scoutHealth, 1f);
        SetHealth(opponentHealth, 1f);
        impactStart = float.NegativeInfinity;
        Update();
    }

    public void ApplyRound(AdventureRound round, bool animate)
    {
        SetHealth(scoutHealth, (float)round.PlayerHealth / scoutMaxHealth);
        SetHealth(opponentHealth, (float)round.EnemyHealth / opponentMaxHealth);
        scoutHit = round.EnemyDamage > 0;
        opponentHit = round.PlayerDamage > 0;
        companionHit = round.CompanionDamage > 0;
        impactStart = animate ? Time.unscaledTime : float.NegativeInfinity;
    }

    private void Update()
    {
        float phaseTime = Time.unscaledTime - phaseStartedAt;
        bool approaching = phase == AdventurePhase.Battle && phaseTime < ApproachSeconds;
        bool walking = phase == AdventurePhase.Trail || approaching;
        if (walking) scroll = (scroll + Time.unscaledDeltaTime * (approaching ? 0.17f : 0.055f)) % 4f;
        Rect viewport = ((RectTransform)transform).rect;
        if (viewport.width > 0f && viewport.height > 0f && far.texture != null)
        {
            float width = viewport.width * far.texture.height / (viewport.height * far.texture.width);
            float center = (1f - width) * 0.5f;
            far.uvRect = new Rect(center + scroll * 0.25f, 0, width, 1);
            middle.uvRect = new Rect(center + scroll * 0.5f, 0, width, 1);
            terrain.uvRect = new Rect(center + scroll, 0, width, 1);
        }
        float impact = Mathf.Clamp01(1f - (Time.unscaledTime - impactStart) / 0.22f);
        float baseline = (phase is AdventurePhase.Battle or AdventurePhase.Result)
            ? Mathf.Lerp(0f, 22f, Mathf.SmoothStep(0f, 1f, phase == AdventurePhase.Result ? 1f : phaseTime / ApproachSeconds))
            : 0f;
        float bob = walking ? Mathf.Abs(Mathf.Sin(phaseTime * (approaching ? 20f : 12f))) * (approaching ? 6f : 3f) : 0f;
        scout.rectTransform.anchoredPosition = new Vector2(baseline + impact * 12f, bob);
        companion.rectTransform.anchoredPosition = new Vector2(baseline + (companionHit ? impact * 18f : 0f), 20f + bob * 0.7f);
        opponent.rectTransform.anchoredPosition = new Vector2(opponentHit ? impact * 7f : 0f, 0f);
        scout.color = ActorColor(scoutHealth.fillAmount, scoutHit ? impact : 0f);
        opponent.color = ActorColor(opponentHealth.fillAmount, opponentHit ? impact : 0f);
    }

    private static Color ActorColor(float health, float impact)
    {
        Color baseColor = new Color(1f, 1f, 1f, health > 0f ? 1f : 0.36f);
        return Color.Lerp(baseColor, new Color(1f, 0.53f, 0.43f, baseColor.a), impact);
    }

    private static void SetHealth(Image fill, float health)
    {
        fill.fillAmount = Mathf.Clamp01(health);
        fill.rectTransform.anchorMax = new Vector2(fill.fillAmount, 1f);
    }

    private void LoadLayer(RawImage layer)
    {
        Texture2D texture = Resources.Load<Texture2D>($"Adventure/{resourceKey}/{layer.name}");
        layer.texture = texture;
        layer.enabled = texture != null;
        if (texture != null) texture.wrapMode = TextureWrapMode.Repeat;
    }

    private RawImage CreateLayer(string name)
    {
        RawImage image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(transform, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        return image;
    }

    private RawImage CreateActor(string name, float x, float size)
    {
        RawImage image = CreateLayer(name);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(x, 0.17f);
        image.rectTransform.pivot = new Vector2(0.5f, 0);
        image.rectTransform.sizeDelta = new Vector2(size, size);
        image.rectTransform.anchoredPosition = Vector2.zero;
        return image;
    }

    private static Image CreateHealthBar(RectTransform actor, Color color)
    {
        Image background = new GameObject("HealthTrack", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(actor, false);
        background.rectTransform.anchorMin = new Vector2(0.14f, 1f);
        background.rectTransform.anchorMax = new Vector2(0.86f, 1f);
        background.rectTransform.offsetMin = new Vector2(0f, 0f);
        background.rectTransform.offsetMax = new Vector2(0f, 4f);
        background.color = new Color(0.1f, 0.14f, 0.17f, 0.9f);
        background.raycastTarget = false;

        Image fill = new GameObject("Health", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(background.transform, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        fill.color = color;
        fill.raycastTarget = false;
        return fill;
    }
}
