using System;
using System.IO;
using Mirror;
using Mirror.FizzySteam;
using TBD.Prototype;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TbdPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string RootName = "TbdPrototypeRoot";
    private const string PlayerPrefabPath = "Assets/Generated/TbdRoomPlayer.prefab";
    private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.11f, 0.94f);
    private static readonly Color AccentColor = new Color(0.29f, 0.69f, 0.94f, 1f);
    private static readonly Color TextColor = new Color(0.9f, 0.94f, 1f, 1f);

    [MenuItem("TBD/Prototype/Rebuild Steam Prototype")]
    public static void RebuildPrototype()
    {
        HangoutHudSceneBuilder.ApplyPlayerSettings();
        HangoutHudFactory.ConfigureCamera();
        EnsureEventSystem();

        DestroyIfPresent(RootName);
        DestroyIfPresent(HangoutHudFactory.RootName);

        var playerPrefab = CreateRoomPlayerPrefab();
        var root = new GameObject(RootName);
        var platform = root.AddComponent<SteamPlatformService>();
        var lobby = root.AddComponent<SteamLobbyService>();
        var transport = root.AddComponent<FizzySteamworks>();
        transport.UseNextGenSteamNetworking = true;
        transport.AllowSteamRelay = true;

        var networkManager = root.AddComponent<TbdNetworkManager>();
        networkManager.transport = transport;
        networkManager.playerPrefab = playerPrefab;
        networkManager.maxConnections = 2;
        networkManager.autoCreatePlayer = true;
        networkManager.dontDestroyOnLoad = false;

        var transparentWindow = root.AddComponent<WindowsTransparentWindow>();
        var controller = root.AddComponent<TbdGameController>();
        var view = CreateInterface(root.transform, out var canvas, out var interactiveRoot);

        Assign(lobby, "platform", platform);
        Assign(networkManager, "steamPlatform", platform);
        Assign(networkManager, "lobbyService", lobby);
        Assign(controller, "view", view);
        Assign(controller, "steamPlatform", platform);
        Assign(controller, "lobbyService", lobby);
        Assign(controller, "networkManager", networkManager);
        Assign(controller, "transparentWindow", transparentWindow);
        transparentWindow.Bind(canvas, new[] { interactiveRoot }, new[] { interactiveRoot });

        Undo.RegisterCreatedObjectUndo(root, "Create TBD Steam Prototype");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("已生成 TBD 双人 Steam 联机原型场景和房间玩家 Prefab。");
    }

    public static void BuildPrototypeForAutomation()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RebuildPrototype();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
    }

    private static TbdGameView CreateInterface(Transform root, out Canvas canvas, out RectTransform interactiveRoot)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasObject = new GameObject("PrototypeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 1000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(320f, 320f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        interactiveRoot = CreateRect("Window", canvasObject.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
        var background = interactiveRoot.gameObject.AddComponent<Image>();
        background.color = PanelColor;

        var view = interactiveRoot.gameObject.AddComponent<TbdGameView>();
        var home = CreatePage("HomePanel", interactiveRoot);
        var lobby = CreatePage("LobbyPanel", interactiveRoot);
        var battle = CreatePage("BattlePanel", interactiveRoot);
        var result = CreatePage("ResultPanel", interactiveRoot);

        BuildHome(home, font, view);
        BuildLobby(lobby, font, view);
        BuildBattle(battle, font, view);
        BuildResult(result, font, view);
        SetObject(view, "homePanel", home.gameObject);
        SetObject(view, "lobbyPanel", lobby.gameObject);
        SetObject(view, "battlePanel", battle.gameObject);
        SetObject(view, "resultPanel", result.gameObject);
        SetSpriteArray(view, "portraits", new[]
        {
            "Assets/Arts/圣骑士perfect.png",
            "Assets/Arts/恶魔猎手.jpg",
            "Assets/Arts/精灵.jpg",
            "Assets/Arts/魔法使.jpg",
            "Assets/Arts/小丑.jpg",
        });
        return view;
    }

    private static void BuildHome(RectTransform page, Font font, TbdGameView view)
    {
        CreateText("Title", page, font, "T B D", 27, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(20f, 236f), new Vector2(280f, 48f));
        CreateText("Subtitle", page, font, "双人自动战斗原型", 13, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(20f, 215f), new Vector2(280f, 24f));
        SetObject(view, "soloButton", CreateButton("SoloButton", page, font, "单人游戏", new Vector2(55f, 160f), new Vector2(196f, 36f)));
        SetObject(view, "createSteamButton", CreateButton("CreateSteamButton", page, font, "创建 Steam 房间", new Vector2(55f, 113f), new Vector2(196f, 36f)));
        SetObject(view, "lobbyIdInput", CreateInput("LobbyIdInput", page, font, "粘贴大厅 ID", new Vector2(35f, 70f), new Vector2(160f, 31f)));
        SetObject(view, "joinLobbyButton", CreateButton("JoinLobbyButton", page, font, "加入", new Vector2(202f, 70f), new Vector2(70f, 31f)));
        SetObject(view, "homeStatusText", CreateText("Status", page, font, string.Empty, 11, FontStyle.Normal, TextAnchor.UpperCenter, new Vector2(24f, 12f), new Vector2(272f, 45f)));
    }

    private static void BuildLobby(RectTransform page, Font font, TbdGameView view)
    {
        CreateText("Title", page, font, "房 间 / 角 色", 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(35f, 272f), new Vector2(250f, 24f));
        var firstPortrait = CreateImage("FirstPortrait", page, new Vector2(24f, 198f), new Vector2(54f, 54f), new Color(1f, 1f, 1f, 1f));
        var secondPortrait = CreateImage("SecondPortrait", page, new Vector2(167f, 198f), new Vector2(54f, 54f), new Color(1f, 1f, 1f, 1f));
        SetObject(view, "firstSlotPortrait", firstPortrait);
        SetObject(view, "secondSlotPortrait", secondPortrait);
        SetObject(view, "firstSlotText", CreateText("FirstSlot", page, font, "槽位 1", 10, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(82f, 194f), new Vector2(76f, 61f)));
        SetObject(view, "secondSlotText", CreateText("SecondSlot", page, font, "槽位 2", 10, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(225f, 194f), new Vector2(72f, 61f)));

        var localPortrait = CreateImage("LocalPortrait", page, new Vector2(24f, 119f), new Vector2(58f, 58f), Color.white);
        SetObject(view, "localPortrait", localPortrait);
        SetObject(view, "localConfigText", CreateText("LocalConfig", page, font, string.Empty, 10, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(89f, 122f), new Vector2(199f, 51f)));
        SetObject(view, "previousPortraitButton", CreateButton("PreviousPortrait", page, font, "◀", new Vector2(23f, 92f), new Vector2(29f, 24f)));
        SetObject(view, "nextPortraitButton", CreateButton("NextPortrait", page, font, "▶", new Vector2(54f, 92f), new Vector2(29f, 24f)));
        SetObject(view, "nextColorButton", CreateButton("NextColor", page, font, "换色", new Vector2(88f, 92f), new Vector2(48f, 24f)));
        SetObject(view, "nextSkillAButton", CreateButton("NextSkillA", page, font, "技能 A", new Vector2(139f, 92f), new Vector2(58f, 24f)));
        SetObject(view, "nextSkillBButton", CreateButton("NextSkillB", page, font, "技能 B", new Vector2(200f, 92f), new Vector2(58f, 24f)));

        SetObject(view, "readyButton", CreateButton("Ready", page, font, "准备", new Vector2(22f, 52f), new Vector2(84f, 31f)));
        SetObject(view, "startBattleButton", CreateButton("StartBattle", page, font, "开始战斗", new Vector2(111f, 52f), new Vector2(104f, 31f)));
        SetObject(view, "inviteButton", CreateButton("Invite", page, font, "邀请", new Vector2(220f, 52f), new Vector2(66f, 31f)));
        SetObject(view, "copyLobbyIdButton", CreateButton("CopyLobbyId", page, font, "复制 ID", new Vector2(22f, 20f), new Vector2(70f, 25f)));
        SetObject(view, "leaveButton", CreateButton("Leave", page, font, "离开", new Vector2(222f, 20f), new Vector2(64f, 25f)));
        SetObject(view, "lobbyIdText", CreateText("LobbyId", page, font, string.Empty, 9, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(94f, 20f), new Vector2(126f, 25f)));
        SetObject(view, "lobbyStatusText", CreateText("LobbyStatus", page, font, string.Empty, 9, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(20f, 0f), new Vector2(270f, 19f)));
    }

    private static void BuildBattle(RectTransform page, Font font, TbdGameView view)
    {
        SetObject(view, "waveText", CreateText("Wave", page, font, "第 1 波", 18, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(40f, 271f), new Vector2(240f, 24f)));
        var heroPortraits = new Image[2];
        var heroTexts = new Text[2];
        var heroHealthFills = new Image[2];
        var enemyImages = new Image[2];
        var enemyTexts = new Text[2];
        var enemyHealthFills = new Image[2];
        for (var i = 0; i < 2; i++)
        {
            var x = 25f + i * 145f;
            heroPortraits[i] = CreateImage($"HeroPortrait{i}", page, new Vector2(x, 169f), new Vector2(60f, 60f), Color.white);
            heroTexts[i] = CreateText($"HeroText{i}", page, font, "英雄", 10, FontStyle.Normal, TextAnchor.UpperCenter, new Vector2(x - 8f, 136f), new Vector2(78f, 31f));
            heroHealthFills[i] = CreateHealthBar($"HeroHealth{i}", page, new Vector2(x, 128f), new Vector2(60f, 6f), new Color(0.28f, 0.82f, 0.45f, 1f));
            var enemyImage = CreateImage($"EnemyShape{i}", page, new Vector2(x + 62f, 169f), new Vector2(54f, 54f), i == 0 ? new Color(0.82f, 0.32f, 0.36f, 1f) : new Color(0.65f, 0.26f, 0.72f, 1f));
            enemyImage.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.78f, 0.42f, 0.95f);
            enemyImages[i] = enemyImage;
            enemyTexts[i] = CreateText($"EnemyText{i}", page, font, "怪物", 10, FontStyle.Normal, TextAnchor.UpperCenter, new Vector2(x + 50f, 136f), new Vector2(78f, 31f));
            enemyHealthFills[i] = CreateHealthBar($"EnemyHealth{i}", page, new Vector2(x + 59f, 128f), new Vector2(60f, 6f), new Color(0.94f, 0.3f, 0.3f, 1f));
        }

        SetObjectArray(view, "heroUnitPortraits", heroPortraits);
        SetObjectArray(view, "heroUnitTexts", heroTexts);
        SetObjectArray(view, "heroHealthFills", heroHealthFills);
        SetObjectArray(view, "enemyUnitImages", enemyImages);
        SetObjectArray(view, "enemyUnitTexts", enemyTexts);
        SetObjectArray(view, "enemyHealthFills", enemyHealthFills);
        SetObject(view, "battleLogText", CreateText("BattleLog", page, font, "准备战斗…", 12, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(20f, 61f), new Vector2(270f, 54f)));
        CreateText("Hint", page, font, "房主权威模拟 · 可靠有序事件同步", 9, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(20f, 22f), new Vector2(270f, 24f));
    }

    private static void BuildResult(RectTransform page, Font font, TbdGameView view)
    {
        SetObject(view, "resultText", CreateText("Result", page, font, "胜 利", 40, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(30f, 150f), new Vector2(250f, 76f)));
        SetObject(view, "returnToLobbyButton", CreateButton("ReturnToLobby", page, font, "返回房间", new Vector2(86f, 90f), new Vector2(138f, 38f)));
    }

    private static GameObject CreateRoomPlayerPrefab()
    {
        var temporary = new GameObject("TbdRoomPlayer", typeof(NetworkIdentity), typeof(TbdRoomPlayer));
        var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PlayerPrefabPath);
        UnityEngine.Object.DestroyImmediate(temporary);
        return prefab;
    }

    private static RectTransform CreatePage(string name, Transform parent)
    {
        return CreateRect(name, parent, Vector2.zero, Vector2.one);
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Text CreateText(string name, Transform parent, Font font, string value, int size, FontStyle style, TextAnchor alignment, Vector2 position, Vector2 dimensions)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = rect.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = TextColor;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Vector2 position, Vector2 dimensions, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var image = rect.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateHealthBar(string name, Transform parent, Vector2 position, Vector2 dimensions, Color color)
    {
        var background = CreateImage(name, parent, position, dimensions, new Color(0.03f, 0.04f, 0.06f, 0.95f));
        var fillRect = CreateRect("Fill", background.transform, Vector2.zero, Vector2.one);
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.color = color;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;
        return fill;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 position, Vector2 dimensions)
    {
        var image = CreateImage(name, parent, position, dimensions, AccentColor);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var text = CreateText("Label", image.transform, font, label, 11, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, dimensions);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }

    private static InputField CreateInput(string name, Transform parent, Font font, string placeholderValue, Vector2 position, Vector2 dimensions)
    {
        var image = CreateImage(name, parent, position, dimensions, new Color(0.12f, 0.16f, 0.22f, 1f));
        image.raycastTarget = true;
        var input = image.gameObject.AddComponent<InputField>();
        var text = CreateText("Text", image.transform, font, string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(7f, 0f), new Vector2(dimensions.x - 14f, dimensions.y));
        var placeholder = CreateText("Placeholder", image.transform, font, placeholderValue, 11, FontStyle.Italic, TextAnchor.MiddleLeft, new Vector2(7f, 0f), new Vector2(dimensions.x - 14f, dimensions.y));
        placeholder.color = new Color(0.62f, 0.68f, 0.76f, 1f);
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = InputField.ContentType.IntegerNumber;
        return input;
    }

    private static void Assign(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SetObject(target, propertyName, value);
    }

    private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray<T>(UnityEngine.Object target, string propertyName, T[] values) where T : UnityEngine.Object
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSpriteArray(UnityEngine.Object target, string propertyName, string[] paths)
    {
        var values = new Sprite[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
            values[i] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
        }

        SetObjectArray(target, propertyName, values);
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private static void DestroyIfPresent(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }
    }
}

public sealed class TbdSteamAppIdPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        var source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "steam_appid.txt"));
        var buildDirectory = Path.GetDirectoryName(report.summary.outputPath);
        if (string.IsNullOrEmpty(buildDirectory))
        {
            throw new BuildFailedException("无法确定 Windows 构建输出目录。");
        }

        File.Copy(source, Path.Combine(buildDirectory, "steam_appid.txt"), true);
    }
}
