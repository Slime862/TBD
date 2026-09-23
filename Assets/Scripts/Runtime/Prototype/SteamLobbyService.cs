using System;
using Steamworks;
using TBD.Core;
using UnityEngine;

namespace TBD.Prototype
{
    [DefaultExecutionOrder(-900)]
    public sealed class SteamLobbyService : MonoBehaviour
    {
        private const string ProtocolKey = "tbd_protocol";
        private const string HostKey = "host_steam_id";
        private const string StateKey = "state";
        private const string LobbyState = "lobby";
        private const string BattleState = "battle";

        [SerializeField] private SteamPlatformService platform;
        private Callback<LobbyCreated_t> lobbyCreatedCallback;
        private Callback<LobbyEnter_t> lobbyEnterCallback;
        private Callback<GameLobbyJoinRequested_t> joinRequestedCallback;
        private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

        public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;

        public bool HasLobby => CurrentLobby != CSteamID.Nil;

        public bool IsLobbyOwner => HasLobby && SteamMatchmaking.GetLobbyOwner(CurrentLobby) == SteamUser.GetSteamID();

        public event Action<ulong, ulong, bool> LobbyEntered;
        public event Action LobbyMembersChanged;
        public event Action<string> LobbyError;

        public void Bind(SteamPlatformService steamPlatform)
        {
            platform = steamPlatform;
        }

        private void Start()
        {
            if (platform == null || !platform.IsInitialized)
            {
                return;
            }

            lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdated);
        }

        private void OnDestroy()
        {
            LeaveLobby();
            lobbyCreatedCallback?.Dispose();
            lobbyEnterCallback?.Dispose();
            joinRequestedCallback?.Dispose();
            lobbyChatUpdateCallback?.Dispose();
        }

        public void CreateFriendsOnlyLobby()
        {
            if (platform == null || !platform.IsInitialized)
            {
                LobbyError?.Invoke("Steam 未启动，无法创建联机房间。");
                return;
            }

            LeaveLobby();
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, PrototypeRules.PlayerCount);
        }

        public void JoinLobby(ulong lobbyId)
        {
            if (platform == null || !platform.IsInitialized || lobbyId == 0)
            {
                LobbyError?.Invoke("大厅 ID 无效或 Steam 未启动。");
                return;
            }

            LeaveLobby();
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        }

        public void InviteFriends()
        {
            if (!HasLobby)
            {
                LobbyError?.Invoke("请先创建 Steam 房间。");
                return;
            }

            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
        }

        public string GetLobbyIdText()
        {
            return HasLobby ? CurrentLobby.m_SteamID.ToString() : string.Empty;
        }

        public void SetBattleInProgress(bool inProgress)
        {
            if (!IsLobbyOwner)
            {
                return;
            }

            SteamMatchmaking.SetLobbyData(CurrentLobby, StateKey, inProgress ? BattleState : LobbyState);
            SteamMatchmaking.SetLobbyJoinable(CurrentLobby, !inProgress);
        }

        public bool IsCurrentLobbyMember(ulong steamId)
        {
            if (!HasLobby)
            {
                return false;
            }

            var count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
            for (var i = 0; i < count; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i).m_SteamID == steamId)
                {
                    return true;
                }
            }

            return false;
        }

        public void LeaveLobby()
        {
            if (!HasLobby || platform == null || !platform.IsInitialized)
            {
                CurrentLobby = CSteamID.Nil;
                return;
            }

            SteamMatchmaking.LeaveLobby(CurrentLobby);
            CurrentLobby = CSteamID.Nil;
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                LobbyError?.Invoke($"创建 Steam 房间失败：{callback.m_eResult}");
                return;
            }

            CurrentLobby = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(CurrentLobby, ProtocolKey, PrototypeRules.ProtocolVersion.ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobby, HostKey, platform.LocalSteamId.ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobby, StateKey, LobbyState);
            SteamMatchmaking.SetLobbyJoinable(CurrentLobby, true);
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            if ((EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                LobbyError?.Invoke($"加入 Steam 房间失败：{(EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse}");
                return;
            }

            CurrentLobby = new CSteamID(callback.m_ulSteamIDLobby);
            if (!ValidateCurrentLobby(out var hostSteamId, out var reason))
            {
                LeaveLobby();
                LobbyError?.Invoke(reason);
                return;
            }

            LobbyEntered?.Invoke(CurrentLobby.m_SteamID, hostSteamId, IsLobbyOwner);
        }

        private void OnJoinRequested(GameLobbyJoinRequested_t callback)
        {
            JoinLobby(callback.m_steamIDLobby.m_SteamID);
        }

        private void OnLobbyChatUpdated(LobbyChatUpdate_t callback)
        {
            if (HasLobby && callback.m_ulSteamIDLobby == CurrentLobby.m_SteamID)
            {
                LobbyMembersChanged?.Invoke();
            }
        }

        private bool ValidateCurrentLobby(out ulong hostSteamId, out string reason)
        {
            hostSteamId = 0;
            var protocol = SteamMatchmaking.GetLobbyData(CurrentLobby, ProtocolKey);
            var state = SteamMatchmaking.GetLobbyData(CurrentLobby, StateKey);
            var host = SteamMatchmaking.GetLobbyData(CurrentLobby, HostKey);
            var memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);

            if (protocol != PrototypeRules.ProtocolVersion.ToString())
            {
                reason = "房间协议不匹配，可能是其他 Spacewar 测试程序创建的大厅。";
                return false;
            }

            if (state != LobbyState)
            {
                reason = "房间正在战斗，暂时不能加入。";
                return false;
            }

            if (memberCount > PrototypeRules.PlayerCount || !ulong.TryParse(host, out hostSteamId))
            {
                reason = "房间人数已满或房主信息无效。";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
