using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Diagnostics;
using Debug = UnityEngine.Debug;
#endif

public sealed class WindowsTransparentWindow : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform[] interactiveRoots = Array.Empty<RectTransform>();
    [SerializeField] private RectTransform[] draggableRoots = Array.Empty<RectTransform>();
#pragma warning disable 0414
    [SerializeField] private bool topMost = true;
    [SerializeField] private bool borderless = true;
    [SerializeField] private Vector2Int windowSize = new Vector2Int(320, 320);
#pragma warning restore 0414

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private IntPtr windowHandle;
    private bool isClickThrough;
    private GraphicRaycaster graphicRaycaster;
    private PointerEventData pointerEventData;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsPopup = 0x80000000L;
    private const long WsExLayered = 0x00080000L;
    private const long WsExTransparent = 0x00000020L;
    private const uint LwaAlpha = 0x00000002;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;

    private static readonly IntPtr HwndTopmost = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int x;
        public int y;
    }
#endif

    public void Bind(Canvas canvas, RectTransform[] roots, RectTransform[] dragRoots)
    {
        targetCanvas = canvas;
        interactiveRoots = roots ?? Array.Empty<RectTransform>();
        draggableRoots = dragRoots ?? Array.Empty<RectTransform>();

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        graphicRaycaster = targetCanvas != null ? targetCanvas.GetComponent<GraphicRaycaster>() : null;
#endif
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private void Start()
    {
        InitializeRuntimeReferences();
        StartCoroutine(InitializeWindowWhenReady());
    }

    private void Update()
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        if (!TryGetClientPointerPosition(out var screenPoint))
        {
            SetClickThrough(true);
            return;
        }

        var isOverInteractiveUi = IsPointerOverAnyUi(interactiveRoots, screenPoint);
        SetClickThrough(!isOverInteractiveUi);

        if (isOverInteractiveUi
            && Input.GetMouseButtonDown(0)
            && IsPointerOverAnyUi(draggableRoots, screenPoint)
            && !IsPointerOverClickControl(screenPoint))
        {
            BeginWindowDrag();
        }
    }

    private IEnumerator InitializeWindowWhenReady()
    {
        for (var i = 0; i < 120; i++)
        {
            windowHandle = GetActiveWindow();
            if (windowHandle == IntPtr.Zero)
            {
                windowHandle = Process.GetCurrentProcess().MainWindowHandle;
            }

            if (windowHandle != IntPtr.Zero)
            {
                break;
            }

            yield return null;
        }

        if (windowHandle == IntPtr.Zero)
        {
            Debug.LogError("无法取得 Unity Player 窗口句柄，透明穿透窗口初始化失败。", this);
            yield break;
        }

        ApplyTransparentWindow();
        SetClickThrough(true);
    }

    private void BeginWindowDrag()
    {
        SetClickThrough(false);

        if (!ReleaseCapture())
        {
            LogLastWin32Error("ReleaseCapture 失败");
            return;
        }

        SendMessage(windowHandle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
    }

    private void InitializeRuntimeReferences()
    {
        if (targetCanvas == null)
        {
            Debug.LogError("WindowsTransparentWindow 缺少 Canvas 引用，点击控件排除和窗口拖拽无法正确工作。", this);
            return;
        }

        graphicRaycaster = targetCanvas.GetComponent<GraphicRaycaster>();
        if (graphicRaycaster == null)
        {
            Debug.LogError("WindowsTransparentWindow 的 Canvas 缺少 GraphicRaycaster，点击控件排除无法正确工作。", this);
        }
    }

    private void ApplyTransparentWindow()
    {
        var margins = new Margins { cxLeftWidth = -1 };
        var dwmResult = DwmExtendFrameIntoClientArea(windowHandle, ref margins);
        if (dwmResult != 0)
        {
            Debug.LogError($"DwmExtendFrameIntoClientArea 失败，错误码：{dwmResult}", this);
        }

        var exStyle = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        if (!TrySetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(exStyle | WsExLayered), out var layerError))
        {
            Debug.LogError($"设置 WS_EX_LAYERED 失败，Win32 错误码：{layerError}", this);
        }

        if (!SetLayeredWindowAttributes(windowHandle, 0, 255, LwaAlpha))
        {
            LogLastWin32Error("SetLayeredWindowAttributes 失败");
        }

        if (borderless)
        {
            var style = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            style |= WsPopup;

            if (!TrySetWindowLongPtr(windowHandle, GwlStyle, new IntPtr(style), out var styleError))
            {
                Debug.LogError($"设置无边框窗口样式失败，Win32 错误码：{styleError}", this);
            }

            if (!SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, windowSize.x, windowSize.y, SwpNoZOrder | SwpNoActivate | SwpFrameChanged))
            {
                LogLastWin32Error("设置窗口尺寸失败");
            }
        }

        if (topMost && !SetWindowPos(windowHandle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate))
        {
            LogLastWin32Error("设置窗口置顶失败");
        }
    }

    private void SetClickThrough(bool clickThrough)
    {
        if (isClickThrough == clickThrough)
        {
            return;
        }

        var exStyle = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        exStyle = clickThrough ? exStyle | WsExTransparent : exStyle & ~WsExTransparent;

        if (!TrySetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(exStyle), out var error))
        {
            Debug.LogError($"{(clickThrough ? "启用鼠标穿透失败" : "关闭鼠标穿透失败")}，Win32 错误码：{error}", this);
            return;
        }

        isClickThrough = clickThrough;
    }

    private bool IsPointerOverAnyUi(RectTransform[] roots, Vector2 screenPoint)
    {
        if (roots == null)
        {
            return false;
        }

        var uiCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;

        foreach (var root in roots)
        {
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(root, screenPoint, uiCamera))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointerOverClickControl(Vector2 screenPoint)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null || graphicRaycaster == null)
        {
            return false;
        }

        if (pointerEventData == null)
        {
            pointerEventData = new PointerEventData(eventSystem);
        }

        pointerEventData.Reset();
        pointerEventData.position = screenPoint;
        raycastResults.Clear();
        graphicRaycaster.Raycast(pointerEventData, raycastResults);

        foreach (var result in raycastResults)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            if (result.gameObject.GetComponentInParent<Selectable>() != null)
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(result.gameObject) != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetClientPointerPosition(out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;

        if (!GetCursorPos(out var point))
        {
            LogLastWin32Error("GetCursorPos 失败");
            return false;
        }

        if (!ScreenToClient(windowHandle, ref point))
        {
            LogLastWin32Error("ScreenToClient 失败");
            return false;
        }

        screenPoint = new Vector2(point.x, Screen.height - point.y);
        return point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height;
    }

    private static void LogLastWin32Error(string message)
    {
        Debug.LogError($"{message}，Win32 错误码：{Marshal.GetLastWin32Error()}");
    }

    private static bool TrySetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value, out int error)
    {
        SetLastError(0);
        var previousValue = SetWindowLongPtr(hWnd, nIndex, value);
        error = Marshal.GetLastWin32Error();
        return previousValue != IntPtr.Zero || error == 0;
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, unchecked((int)dwNewLong.ToInt64())));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(uint dwErrCode);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ScreenToClient(IntPtr hWnd, ref NativePoint lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins pMarInset);
#endif
}
