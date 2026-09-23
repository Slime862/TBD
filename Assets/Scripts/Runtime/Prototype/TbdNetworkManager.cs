using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using TBD.Core;
using UnityEngine;

namespace TBD.Prototype
{
    public struct BattleStartedMessage : NetworkMessage
    {
        public LobbyPlayerState firstPlayer;
        public LobbyPlayerState secondPlayer;
        public int seed;
    }

    public struct BattleEventMessage : NetworkMessage
    {
        public BattleEvent battleEvent;
    }

    public struct ReturnToLobbyMessage : NetworkMessage
    {
    }

    public struct SessionInterruptedMessage : NetworkMessage
    {
        public string reason;
    }

    public sealed class TbdNetworkManager : NetworkManager
    {
        [SerializeField] private SteamPlatformService steamPlatform;
        [SerializeField] private SteamLobbyService lobbyService;
        private Coroutine battleRoutine;
        private bool battleInProgress;

        public bool IsBattleInProgress => battleInProgress;

        public bool IsHost => NetworkServer.active && NetworkClient.active;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<BattleStartedMessage> BattleStarted;
        public event Action<BattleEvent> BattleEventReceived;
        public event Action ReturnedToLobby;
        public event Action<string> SessionInterrupted;

        public void Bind(SteamPlatformService platform, SteamLobbyService lobby)
        {
            steamPlatform = platform;
            lobbyService = lobby;
        }

        public void StartSteamHost()
        {
            if (!steamPlatform.IsInitialized || !lobbyService.IsLobbyOwner)
            {
                SessionInterrupted?.Invoke("Steam 房间尚未准备好。");
                return;
            }

            StartHost();
        }

        public void JoinSteamHost(ulong hostSteamId)
        {
            if (!steamPlatform.IsInitialized || hostSteamId == 0)
            {
                SessionInterrupted?.Invoke("房主 SteamID 无效。");
                return;
            }

            StartClient(new Uri($"steam://{hostSteamId}"));
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<BattleStartedMessage>(OnBattleStarted);
            NetworkClient.RegisterHandler<BattleEventMessage>(OnBattleEvent);
            NetworkClient.RegisterHandler<ReturnToLobbyMessage>(_ => ReturnedToLobby?.Invoke());
            NetworkClient.RegisterHandler<SessionInterruptedMessage>(message => SessionInterrupted?.Invoke(message.reason));
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Connected?.Invoke();
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            battleInProgress = false;
            Disconnected?.Invoke();
        }

        public override void OnServerConnect(NetworkConnectionToClient connection)
        {
            base.OnServerConnect(connection);
            if (battleInProgress)
            {
                connection.Disconnect();
                return;
            }

            // Fizzy 的远端地址就是 SteamID，只允许当前 Steam 大厅中的成员接入。
            if (connection != NetworkServer.localConnection && ulong.TryParse(connection.address, out var remoteSteamId) &&
                !lobbyService.IsCurrentLobbyMember(remoteSteamId))
            {
                connection.Disconnect();
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient connection)
        {
            var playerObject = Instantiate(playerPrefab);
            var roomPlayer = playerObject.GetComponent<TbdRoomPlayer>();
            var playerSlot = NetworkServer.connections.Count == 1 ? 0 : 1;
            var playerSteamId = connection == NetworkServer.localConnection ? steamPlatform.LocalSteamId : ParseSteamId(connection.address);
            var playerName = ResolveDisplayName(playerSteamId, playerSlot);
            roomPlayer.Initialize(playerSlot, playerSteamId, playerName, CharacterLoadout.CreateDefault());
            NetworkServer.AddPlayerForConnection(connection, playerObject);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient connection)
        {
            var guestLeftDuringBattle = battleInProgress && connection != NetworkServer.localConnection;
            base.OnServerDisconnect(connection);
            if (guestLeftDuringBattle)
            {
                StopBattleRoutine();
                ResetRoomReadyState();
                lobbyService.SetBattleInProgress(false);
                NetworkServer.SendToAll(new SessionInterruptedMessage
                {
                    reason = "队友已离开，当前战斗已结束。",
                });
                NetworkServer.SendToAll(new ReturnToLobbyMessage());
            }
        }

        [Server]
        public bool CanStartBattle()
        {
            var players = GetServerPlayers();
            return !battleInProgress && LobbyRules.CanStart(players.ConvertAll(player => player.State).ToArray());
        }

        [Server]
        public void ServerStartBattle()
        {
            if (!CanStartBattle())
            {
                return;
            }

            var players = GetServerPlayers();
            players.Sort((left, right) => left.State.slot.CompareTo(right.State.slot));
            var seed = unchecked((int)DateTime.UtcNow.Ticks);
            var message = new BattleStartedMessage
            {
                firstPlayer = players[0].State,
                secondPlayer = players[1].State,
                seed = seed,
            };

            battleInProgress = true;
            lobbyService.SetBattleInProgress(true);
            NetworkServer.SendToAll(message);
            battleRoutine = StartCoroutine(RunAuthoritativeBattle(message));
        }

        [Server]
        public void ServerReturnToLobby()
        {
            StopBattleRoutine();
            ResetRoomReadyState();
            lobbyService.SetBattleInProgress(false);
            NetworkServer.SendToAll(new ReturnToLobbyMessage());
        }

        private IEnumerator RunAuthoritativeBattle(BattleStartedMessage message)
        {
            var simulation = new BattleSimulation(message.firstPlayer, message.secondPlayer, message.seed);
            while (simulation.TryStep(out var battleEvent))
            {
                NetworkServer.SendToAll(new BattleEventMessage { battleEvent = battleEvent });
                if (battleEvent.type == BattleEventType.BattleEnded)
                {
                    battleRoutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(0.55f);
            }
        }

        private void OnBattleStarted(BattleStartedMessage message)
        {
            battleInProgress = true;
            BattleStarted?.Invoke(message);
        }

        private void OnBattleEvent(BattleEventMessage message)
        {
            BattleEventReceived?.Invoke(message.battleEvent);
        }

        private void StopBattleRoutine()
        {
            battleInProgress = false;
            if (battleRoutine != null)
            {
                StopCoroutine(battleRoutine);
                battleRoutine = null;
            }
        }

        [Server]
        private static void ResetRoomReadyState()
        {
            foreach (var player in GetServerPlayers())
            {
                player.ServerResetReady();
            }
        }

        [Server]
        private static List<TbdRoomPlayer> GetServerPlayers()
        {
            var result = new List<TbdRoomPlayer>(PrototypeRules.PlayerCount);
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection.identity != null && connection.identity.TryGetComponent(out TbdRoomPlayer player))
                {
                    result.Add(player);
                }
            }

            return result;
        }

        private string ResolveDisplayName(ulong steamId, int playerSlot)
        {
            if (steamId == steamPlatform.LocalSteamId)
            {
                return steamPlatform.LocalDisplayName;
            }

            if (steamId != 0)
            {
                return Steamworks.SteamFriends.GetFriendPersonaName(new Steamworks.CSteamID(steamId));
            }

            return $"玩家 {playerSlot + 1}";
        }

        private static ulong ParseSteamId(string address)
        {
            return ulong.TryParse(address, out var steamId) ? steamId : 0;
        }
    }
}
