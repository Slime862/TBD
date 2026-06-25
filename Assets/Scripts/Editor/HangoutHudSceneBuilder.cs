using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HangoutHudSceneBuilder
{
    [MenuItem("Tools/Hangout Game/Apply All Setup")]
    [MenuItem("TBD/Hangout Game/Apply All Setup")]
    public static void ApplyAllSetup()
    {
        ApplyPlayerSettings();
        RebuildBaseUi();
    }

    [MenuItem("Tools/Hangout Game/Rebuild Base UI")]
    [MenuItem("TBD/Hangout Game/Rebuild Base UI")]
    public static void RebuildBaseUi()
    {
        var existingRoot = GameObject.Find(HangoutHudFactory.RootName);
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot);
        }

        HangoutHudFactory.ConfigureCamera();
        HangoutHudFactory.EnsureEventSystem();

        var controller = HangoutHudFactory.CreateHud();
        Undo.RegisterCreatedObjectUndo(controller.gameObject, "Create Hangout HUD");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("已生成基础挂机窗口 UI。");
    }

    [MenuItem("Tools/Hangout Game/Apply Player Settings")]
    [MenuItem("TBD/Hangout Game/Apply Player Settings")]
    public static void ApplyPlayerSettings()
    {
        PlayerSettings.defaultScreenWidth = 320;
        PlayerSettings.defaultScreenHeight = 320;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.allowFullscreenSwitch = false;
        PlayerSettings.useFlipModelSwapchain = false;

        AssetDatabase.SaveAssets();
        Debug.Log("已应用挂机窗口 PlayerSettings。");
    }
}
