using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TBD.Core;
using TBD.Persistence;
using UnityEngine;

namespace TBD.Prototype
{
    public sealed class TbdGameController : MonoBehaviour
    {
        [SerializeField] private TbdGameView view;
        [SerializeField] private SteamPlatformService steamPlatform;
        [SerializeField] private SteamLobbyService lobbyService;
        [SerializeField] private TbdNetworkManager networkManager;
        [SerializeField] private WindowsTransparentWindow transparentWindow;

        private readonly List<LobbyPlayerState> soloPlayers = new List<LobbyPlayerState>(2);
        private PlayerProfileStore profileStore;
        private CharacterLoadout localLoadout;
        private GameMode mode;
        private GameFlowState flowState;
        private bool localReady;
        private bool sentInitialLoadout;
        private bool intentionalDisconnect;
        private Coroutine soloBattleRoutine;
        private string lobbyStatus = string.Empty;

        private void Awake()
        {
            profileStore = new PlayerProfileStore();
            localLoadout = profileStore.LoadOrDefault();
            lobbyService.Bind(steamPlatform);
            networkManager.Bind(steamPlatform, lobbyService);

            lobbyService.LobbyEntered += OnLobbyEntered;
            lobbyService.LobbyMembersChanged += RefreshLobby;
            lobbyService.LobbyError += OnLobbyError;
            networkManager.Connected += OnNetworkConnected;
            networkManager.Disconnected += OnNetworkDisconnected;
            networkManager.BattleStarted += OnNetworkBattleStarted;
            networkManager.BattleEventReceived += OnBattleEvent;
            networkManager.ReturnedToLobby += OnNetworkReturnedToLobby;
            networkManager.SessionInterrupted += OnSessionInterrupted;
            TbdRoomPlayer.PlayersChanged += OnRoomPlayersChanged;
            steamPlatform.OverlayActivityChanged += transparentWindow.SetInteractionLocked;
        }

        private void Start()
        {
            view.Initialize(this, steamPlatform.IsInitialized);
            view.RenderLoadout(localLoadout);
            ShowHome(steamPlatform.IsInitialized
                ? "可创建好友房间，也可粘贴大厅 ID 加入。"
                : "Steam 未启动：单人模式仍可正常游玩。");
        }

        private void OnDestroy()
        {
            lobbyService.LobbyEntered -= OnLobbyEntered;
            lobbyService.LobbyMembersChanged -= RefreshLobby;
            lobbyService.LobbyError -= OnLobbyError;
            networkManager.Connected -= OnNetworkConnected;
            networkManager.Disconnected -= OnNetworkDisconnected;
            networkManager.BattleStarted -= OnNetworkBattleStarted;
            networkManager.BattleEventReceived -= OnBattleEvent;
            networkManager.ReturnedToLobby -= OnNetworkReturnedToLobby;
            networkManager.SessionInterrupted -= OnSessionInterrupted;
            TbdRoomPlayer.PlayersChanged -= OnRoomPlayersChanged;
            steamPlatform.OverlayActivityChanged -= transparentWindow.SetInteractionLocked;
        }

        public void StartSolo()
        {
            mode = GameMode.Solo;
            flowState = GameFlowState.Lobby;
            localReady = false;
            soloPlayers.Clear();
            soloPlayers.Add(CreateLocalPlayer(0, false));
            soloPlayers.Add(new LobbyPlayerState
            {
                slot = 1,
                displayName = "圣骑士 AI",
                loadout = PrototypeRules.CreateAiLoadout(),
                ready = true,
                isAi = true,
            });
            RefreshLobby();
        }

        public void CreateSteamLobby()
        {
            mode = GameMode.SteamCoop;
            flowState = GameFlowState.Lobby;
            localReady = false;
            sentInitialLoadout = false;
            lobbyStatus = "正在创建仅好友可见房间…";
            view.ShowState(flowState);
            view.RenderLobby(mode, new List<LobbyPlayerState>(), false, false, string.Empty, lobbyStatus);
            lobbyService.CreateFriendsOnlyLobby();
        }

        public void JoinLobbyFromInput()
        {
            if (!ulong.TryParse(view.LobbyIdInput, out var lobbyId))
            {
                view.SetHomeStatus("请输入有效的数字大厅 ID。");
                return;
            }

            mode = GameMode.SteamCoop;
            intentionalDisconnect = false;
            sentInitialLoadout = false;
            view.SetHomeStatus("正在加入 Steam 房间…");
            lobbyService.JoinLobby(lobbyId);
        }

        public void ChangePortrait(int direction)
        {
            localLoadout.portraitId = Wrap(localLoadout.portraitId + direction, PrototypeRules.PortraitCount);
            CommitLoadoutChange();
        }

        public void ChangeColor()
        {
            localLoadout.colorId = (localLoadout.colorId + 1) % PrototypeRules.ColorCount;
            CommitLoadoutChange();
        }

        public void ChangeSkill(int slot)
        {
            if (slot == 0)
            {
                localLoadout.skillA = NextDifferentSkill(localLoadout.skillA, localLoadout.skillB);
            }
            else
            {
                localLoadout.skillB = NextDifferentSkill(localLoadout.skillB, localLoadout.skillA);
            }

            CommitLoadoutChange();
        }

        public void ToggleReady()
        {
            localReady = !localReady;
            if (mode == GameMode.Solo)
            {
                var player = soloPlayers[0];
                player.ready = localReady;
                soloPlayers[0] = player;
                RefreshLobby();
                return;
            }

            FindLocalRoomPlayer()?.CmdSetReady(localReady);
        }

        public void StartBattle()
        {
            if (mode == GameMode.Solo)
            {
                if (!localReady || soloBattleRoutine != null)
                {
                    return;
                }

                soloBattleRoutine = StartCoroutine(RunSoloBattle());
                return;
            }

            if (networkManager.IsHost)
            {
                networkManager.ServerStartBattle();
            }
        }

        public void InviteFriends()
        {
            lobbyService.InviteFriends();
        }

        public void CopyLobbyId()
        {
            GUIUtility.systemCopyBuffer = lobbyService.GetLobbyIdText();
            lobbyStatus = "大厅 ID 已复制。";
            RefreshLobby();
        }

        public void LeaveToHome()
        {
            intentionalDisconnect = true;
            if (networkManager.mode == NetworkManagerMode.Host)
            {
                networkManager.StopHost();
            }
            else if (networkManager.mode == NetworkManagerMode.ClientOnly)
            {
                networkManager.StopClient();
            }

            lobbyService.LeaveLobby();
            ShowHome(steamPlatform.IsInitialized ? "已离开房间。" : "Steam 未启动：单人模式仍可正常游玩。");
        }

        public void ReturnToLobby()
        {
            if (mode == GameMode.Solo)
            {
                localReady = false;
                var player = soloPlayers[0];
                player.ready = false;
                soloPlayers[0] = player;
                flowState = GameFlowState.Lobby;
                RefreshLobby();
                return;
            }

            var localPlayer = FindLocalRoomPlayer();
            if (localPlayer != null)
            {
                localPlayer.CmdRequestReturnToLobby();
            }
        }

        private void CommitLoadoutChange()
        {
            localLoadout = localLoadout.Sanitized();
            localReady = false;
            profileStore.Save(localLoadout);
            view.RenderLoadout(localLoadout);

            if (mode == GameMode.Solo && soloPlayers.Count == 2)
            {
                soloPlayers[0] = CreateLocalPlayer(0, false);
                RefreshLobby();
            }
            else if (mode == GameMode.SteamCoop)
            {
                FindLocalRoomPlayer()?.CmdSubmitLoadout(localLoadout);
            }
        }

        private IEnumerator RunSoloBattle()
        {
            flowState = GameFlowState.Battle;
            view.BeginBattle(soloPlayers[0], soloPlayers[1]);
            var simulation = new BattleSimulation(soloPlayers[0], soloPlayers[1], Random.Range(int.MinValue, int.MaxValue));
            while (simulation.TryStep(out var battleEvent))
            {
                OnBattleEvent(battleEvent);
                if (battleEvent.type == BattleEventType.BattleEnded)
                {
                    soloBattleRoutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(0.55f);
            }
        }

        private void OnLobbyEntered(ulong lobbyId, ulong hostSteamId, bool isOwner)
        {
            flowState = GameFlowState.Lobby;
            intentionalDisconnect = false;
            lobbyStatus = isOwner ? "房间已创建，等待好友加入。" : "已加入房间，正在连接房主…";
            RefreshLobby();
            if (isOwner)
            {
                networkManager.StartSteamHost();
            }
            else
            {
                networkManager.JoinSteamHost(hostSteamId);
            }
        }

        private void OnNetworkConnected()
        {
            flowState = GameFlowState.Lobby;
            lobbyStatus = "连接成功。选择角色并准备。";
            RefreshLobby();
        }

        private void OnNetworkDisconnected()
        {
            sentInitialLoadout = false;
            if (!intentionalDisconnect && mode == GameMode.SteamCoop)
            {
                lobbyService.LeaveLobby();
                ShowHome("与房主的连接已断开，已返回首页。");
            }
        }

        private void OnRoomPlayersChanged()
        {
            var localPlayer = FindLocalRoomPlayer();
            if (localPlayer != null)
            {
                localReady = localPlayer.State.ready;
                if (!sentInitialLoadout)
                {
                    sentInitialLoadout = true;
                    localPlayer.CmdSubmitLoadout(localLoadout);
                }
            }

            RefreshLobby();
        }

        private void OnNetworkBattleStarted(BattleStartedMessage message)
        {
            flowState = GameFlowState.Battle;
            view.BeginBattle(message.firstPlayer, message.secondPlayer);
        }

        private void OnBattleEvent(BattleEvent battleEvent)
        {
            view.ApplyBattleEvent(battleEvent);
            if (battleEvent.type == BattleEventType.BattleEnded)
            {
                flowState = GameFlowState.Result;
                view.ShowResult(battleEvent.heroesWin);
                view.SetReturnButtonInteractable(mode == GameMode.Solo || networkManager.IsHost || FindLocalRoomPlayer() != null);
            }
        }

        private void OnNetworkReturnedToLobby()
        {
            localReady = false;
            flowState = GameFlowState.Lobby;
            lobbyStatus = "已返回房间，可重新配置后再战。";
            RefreshLobby();
        }

        private void OnSessionInterrupted(string reason)
        {
            lobbyStatus = reason;
            if (networkManager.IsHost)
            {
                flowState = GameFlowState.Lobby;
                RefreshLobby();
            }
            else
            {
                view.SetHomeStatus(reason);
            }
        }

        private void OnLobbyError(string reason)
        {
            ShowHome(reason);
        }

        private void RefreshLobby()
        {
            if (flowState != GameFlowState.Lobby)
            {
                return;
            }

            IReadOnlyList<LobbyPlayerState> players;
            bool canStart;
            if (mode == GameMode.Solo)
            {
                players = soloPlayers;
                canStart = localReady;
            }
            else
            {
                players = TbdRoomPlayer.Players.Select(player => player.State).OrderBy(player => player.slot).ToArray();
                canStart = networkManager.IsHost && LobbyRules.CanStart(players.ToArray());
            }

            view.ShowState(GameFlowState.Lobby);
            view.RenderLoadout(localLoadout);
            view.RenderLobby(mode, players, localReady, canStart, lobbyService.GetLobbyIdText(), lobbyStatus);
        }

        private LobbyPlayerState CreateLocalPlayer(int slot, bool ready)
        {
            return new LobbyPlayerState
            {
                slot = slot,
                steamId = steamPlatform.LocalSteamId,
                displayName = steamPlatform.IsInitialized ? steamPlatform.LocalDisplayName : "本地玩家",
                loadout = localLoadout,
                ready = ready,
            };
        }

        private TbdRoomPlayer FindLocalRoomPlayer()
        {
            return TbdRoomPlayer.Players.FirstOrDefault(player => player.isLocalPlayer);
        }

        private void ShowHome(string status)
        {
            flowState = GameFlowState.Home;
            localReady = false;
            view.ShowState(flowState);
            view.SetHomeStatus(status);
        }

        private static int Wrap(int value, int count)
        {
            return (value % count + count) % count;
        }

        private static PrototypeSkill NextDifferentSkill(PrototypeSkill current, PrototypeSkill other)
        {
            var next = (PrototypeSkill)(((int)current + 1) % PrototypeRules.SkillCount);
            return next == other ? (PrototypeSkill)(((int)next + 1) % PrototypeRules.SkillCount) : next;
        }
    }
}
