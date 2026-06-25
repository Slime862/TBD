using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class HangoutHudFactory
{
    public const string RootName = "HangoutHudRoot";
    public const string CanvasName = "HangoutHudCanvas";
    private const int ReferenceSize = 320;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildRuntimeHud()
    {
        EnsureSceneReady();
    }

    public static HangoutHudController EnsureSceneReady()
    {
        Application.runInBackground = true;
        ConfigureCamera();
        EnsureEventSystem();

        var existing = Object.FindObjectOfType<HangoutHudController>();
        if (existing != null)
        {
            return existing;
        }

        return CreateHud();
    }

    public static void ConfigureCamera()
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("场景中没有 MainCamera，无法配置透明窗口背景。");
            return;
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        mainCamera.allowHDR = false;
        mainCamera.allowMSAA = false;
    }

    public static EventSystem EnsureEventSystem()
    {
        var eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem != null)
        {
            return eventSystem;
        }

        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        return eventSystemObject.GetComponent<EventSystem>();
    }

    public static HangoutHudController CreateHud()
    {
        var uiFont = CreateUiFont();
        var root = new GameObject(RootName);

        var windowController = root.AddComponent<WindowsTransparentWindow>();

        var canvasObject = new GameObject(
            CanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 1000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceSize, ReferenceSize);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var battlePanel = CreatePanel("BattlePreviewPanel", canvasRect, new Color(0f, 0f, 0f, 0f));
        Stretch(battlePanel, new Vector2(0f, 0.23f), Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -10f));

        var battleStatus = CreateText("BattleStatus", battlePanel, uiFont, 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.9f, 0.86f, 0.72f, 1f));
        battleStatus.rectTransform.anchorMin = new Vector2(0f, 1f);
        battleStatus.rectTransform.anchorMax = new Vector2(1f, 1f);
        battleStatus.rectTransform.pivot = new Vector2(0.5f, 1f);
        battleStatus.rectTransform.anchoredPosition = new Vector2(0f, -80f);
        battleStatus.rectTransform.sizeDelta = new Vector2(0f, 20f);
        battleStatus.text = "普通关自动战斗";

        var formationLabel = CreateText("FormationLabel", battlePanel, uiFont, 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.9f, 0.86f, 0.72f, 1f));
        formationLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
        formationLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        formationLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        formationLabel.rectTransform.anchoredPosition = new Vector2(0f, -104f);
        formationLabel.rectTransform.sizeDelta = new Vector2(0f, 18f);
        formationLabel.text = "我方站位 1-4     敌方站位 1-4";

        var heroBindings = new BattlePreviewController.UnitBinding[4];
        var enemyBindings = new BattlePreviewController.UnitBinding[4];
        for (var i = 0; i < 4; i++)
        {
            heroBindings[i] = CreateBattleUnit($"HeroSlot{i + 1}", battlePanel, uiFont, new Vector2(20f + i * 30f, -188f), new Color(0.36f, 0.72f, 0.88f, 1f), "我", i + 1, false);
            enemyBindings[i] = CreateBattleUnit($"EnemySlot{i + 1}", battlePanel, uiFont, new Vector2(191f + i * 30f, -188f), new Color(0.76f, 0.28f, 0.3f, 1f), "敌", i + 1, false);
        }

        var battlePreview = battlePanel.gameObject.AddComponent<BattlePreviewController>();
        battlePreview.Bind(heroBindings, enemyBindings, battleStatus);

        var detailPanel = CreatePanel("DetailPanel", canvasRect, new Color(0.07f, 0.09f, 0.11f, 0.88f));
        Stretch(detailPanel, new Vector2(0f, 0.23f), Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -10f));

        var detailTitle = CreateText("DetailTitle", detailPanel, uiFont, 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Stretch(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -42f), new Vector2(-14f, -10f));
        detailTitle.text = "挂机详情";

        var detailSummary = CreateText("DetailSummary", detailPanel, uiFont, 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.86f, 0.9f, 0.94f, 1f));
        Stretch(detailSummary.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -52f));
        detailSummary.lineSpacing = 1.25f;

        var bottomBar = CreatePanel("BottomBar", canvasRect, new Color(0.05f, 0.07f, 0.08f, 0.82f));
        bottomBar.anchorMin = new Vector2(0f, 0f);
        bottomBar.anchorMax = new Vector2(1f, 0f);
        bottomBar.pivot = new Vector2(0.5f, 0f);
        bottomBar.anchoredPosition = Vector2.zero;
        bottomBar.sizeDelta = new Vector2(0f, 72f);

        var progressText = CreateText("ProgressText", bottomBar, uiFont, 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        Stretch(progressText.rectTransform, new Vector2(0f, 0.48f), Vector2.one, new Vector2(12f, 0f), new Vector2(-74f, -6f));
        progressText.text = "挂机进程 0%";

        var progressTrack = CreatePanel("ProgressTrack", bottomBar, new Color(0.2f, 0.23f, 0.26f, 0.9f));
        Stretch(progressTrack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 14f), new Vector2(-74f, 26f));

        var progressFillRect = CreateRect("ProgressFill", progressTrack);
        Stretch(progressFillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var progressFill = progressFillRect.gameObject.AddComponent<Image>();
        progressFill.color = new Color(0.32f, 0.78f, 0.64f, 1f);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;

        var detailButton = CreateButton("DetailButton", bottomBar, uiFont, "详情");
        var buttonRect = detailButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-10f, 36f);
        buttonRect.sizeDelta = new Vector2(52f, 44f);

        var controller = root.AddComponent<HangoutHudController>();
        var detailButtonText = detailButton.GetComponentInChildren<Text>();
        controller.Bind(detailPanel.gameObject, detailButton, detailButtonText, progressText, progressFill, detailSummary);

        var visibleUiRoots = new[] { battlePanel, bottomBar, detailPanel };
        windowController.Bind(canvas, visibleUiRoots, visibleUiRoots);
        return controller;
    }

    private static BattlePreviewController.UnitBinding CreateBattleUnit(
        string name,
        Transform parent,
        Font font,
        Vector2 anchoredPosition,
        Color portraitColor,
        string sideLabel,
        int slotIndex,
        bool showSlotBackground)
    {
        var slot = CreatePanel(name, parent, showSlotBackground ? new Color(0.09f, 0.1f, 0.11f, 0.72f) : new Color(0f, 0f, 0f, 0f));
        slot.anchorMin = new Vector2(0f, 1f);
        slot.anchorMax = new Vector2(0f, 1f);
        slot.pivot = new Vector2(0.5f, 0.5f);
        slot.anchoredPosition = anchoredPosition;
        slot.sizeDelta = new Vector2(22f, 76f);

        var portrait = CreatePanel("Portrait", slot, portraitColor);
        Stretch(portrait, new Vector2(0f, 0f), Vector2.one, new Vector2(3f, 14f), new Vector2(-3f, -5f));
        portrait.GetComponent<Image>().raycastTarget = false;

        var slotText = CreateText("SlotText", slot, font, 11, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        slotText.rectTransform.anchorMin = new Vector2(0f, 1f);
        slotText.rectTransform.anchorMax = new Vector2(1f, 1f);
        slotText.rectTransform.pivot = new Vector2(0.5f, 1f);
        slotText.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        slotText.rectTransform.sizeDelta = new Vector2(0f, 18f);
        slotText.text = $"{sideLabel}{slotIndex}";

        var healthTrack = CreatePanel("HealthTrack", slot, new Color(0.18f, 0.16f, 0.14f, 0.95f));
        healthTrack.anchorMin = new Vector2(0f, 0f);
        healthTrack.anchorMax = new Vector2(1f, 0f);
        healthTrack.pivot = new Vector2(0.5f, 0f);
        healthTrack.anchoredPosition = new Vector2(0f, 6f);
        healthTrack.sizeDelta = new Vector2(-8f, 4f);
        healthTrack.GetComponent<Image>().raycastTarget = false;

        var healthFillRect = CreateRect("HealthFill", healthTrack);
        Stretch(healthFillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var healthFill = healthFillRect.gameObject.AddComponent<Image>();
        healthFill.color = new Color(0.48f, 0.84f, 0.42f, 1f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;
        healthFill.fillAmount = 1f;
        healthFill.raycastTarget = false;

        var hitText = CreateText("HitText", slot, font, 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.42f, 0.28f, 0f));
        hitText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        hitText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        hitText.rectTransform.pivot = new Vector2(0.5f, 0f);
        hitText.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        hitText.rectTransform.sizeDelta = new Vector2(44f, 22f);

        return new BattlePreviewController.UnitBinding
        {
            root = slot,
            portrait = portrait.GetComponent<Image>(),
            healthFill = healthFill,
            hitText = hitText,
        };
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        var rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return rect;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label)
    {
        var rect = CreatePanel(name, parent, new Color(0.92f, 0.86f, 0.36f, 0.95f));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();

        var text = CreateText("Label", rect, font, 14, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.09f, 0.09f, 0.07f, 1f));
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        text.text = label;

        return button;
    }

    private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        var rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Font CreateUiFont()
    {
        var font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
            16);

        if (font != null)
        {
            return font;
        }

        Debug.LogError("无法创建中文 UI 字体，使用 Unity 内置字体，中文可能显示不完整。");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
