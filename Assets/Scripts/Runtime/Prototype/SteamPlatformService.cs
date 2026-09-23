using System;
using Steamworks;
using UnityEngine;

namespace TBD.Prototype
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SteamPlatformService : MonoBehaviour
    {
        private Callback<GameOverlayActivated_t> overlayCallback;
        private bool ownsSteamApi;

        public bool IsInitialized { get; private set; }

        public ulong LocalSteamId => IsInitialized ? SteamUser.GetSteamID().m_SteamID : 0;

        public string LocalDisplayName => IsInitialized ? SteamFriends.GetPersonaName() : "本地玩家";

        public event Action<bool> OverlayActivityChanged;

        private void Awake()
        {
            // Steam 失败只关闭联机入口，单人流程继续使用同一套战斗逻辑。
            try
            {
                var result = SteamAPI.InitEx(out var errorMessage);
                IsInitialized = result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
                ownsSteamApi = IsInitialized;
                if (!IsInitialized)
                {
                    Debug.LogWarning($"Steam 初始化失败，联机模式已禁用：{errorMessage}");
                    return;
                }

                overlayCallback = Callback<GameOverlayActivated_t>.Create(OnOverlayActivityChanged);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Steam 不可用，联机模式已禁用：{exception.Message}");
            }
        }

        private void Update()
        {
            if (IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        private void OnDestroy()
        {
            overlayCallback?.Dispose();
            if (ownsSteamApi)
            {
                IsInitialized = false;
                SteamAPI.Shutdown();
            }
        }

        private void OnOverlayActivityChanged(GameOverlayActivated_t callback)
        {
            OverlayActivityChanged?.Invoke(callback.m_bActive != 0);
        }
    }
}
