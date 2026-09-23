using System;
using System.Collections.Generic;
using Mirror;
using TBD.Core;
using UnityEngine;

namespace TBD.Prototype
{
    public sealed class TbdRoomPlayer : NetworkBehaviour
    {
        private static readonly List<TbdRoomPlayer> ActivePlayers = new List<TbdRoomPlayer>();

        [SyncVar(hook = nameof(OnStateChanged))] private int slot;
        [SyncVar(hook = nameof(OnStateChanged))] private ulong steamId;
        [SyncVar(hook = nameof(OnStateChanged))] private string displayName;
        [SyncVar(hook = nameof(OnStateChanged))] private int portraitId;
        [SyncVar(hook = nameof(OnStateChanged))] private int colorId;
        [SyncVar(hook = nameof(OnStateChanged))] private PrototypeSkill skillA;
        [SyncVar(hook = nameof(OnStateChanged))] private PrototypeSkill skillB;
        [SyncVar(hook = nameof(OnStateChanged))] private bool ready;

        public static IReadOnlyList<TbdRoomPlayer> Players => ActivePlayers;

        public static event Action PlayersChanged;

        public LobbyPlayerState State => new LobbyPlayerState
        {
            slot = slot,
            steamId = steamId,
            displayName = displayName,
            loadout = new CharacterLoadout
            {
                portraitId = portraitId,
                colorId = colorId,
                skillA = skillA,
                skillB = skillB,
            }.Sanitized(),
            ready = ready,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActivePlayers.Clear();
            PlayersChanged = null;
        }

        public override void OnStartClient()
        {
            ActivePlayers.Add(this);
            PlayersChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            ActivePlayers.Remove(this);
            PlayersChanged?.Invoke();
        }

        [Server]
        public void Initialize(int playerSlot, ulong playerSteamId, string playerName, CharacterLoadout initialLoadout)
        {
            slot = playerSlot;
            steamId = playerSteamId;
            displayName = playerName;
            ApplyLoadout(initialLoadout);
        }

        [Command]
        public void CmdSubmitLoadout(CharacterLoadout loadout)
        {
            ApplyLoadout(loadout);
            ready = false;
        }

        [Command]
        public void CmdSetReady(bool value)
        {
            ready = value;
        }

        [Command]
        public void CmdRequestReturnToLobby()
        {
            if (NetworkManager.singleton is TbdNetworkManager manager)
            {
                manager.ServerReturnToLobby();
            }
        }

        [Server]
        public void ServerResetReady()
        {
            ready = false;
        }

        [Server]
        private void ApplyLoadout(CharacterLoadout loadout)
        {
            var sanitized = loadout.Sanitized();
            portraitId = sanitized.portraitId;
            colorId = sanitized.colorId;
            skillA = sanitized.skillA;
            skillB = sanitized.skillB;
        }

        private void OnStateChanged(int oldValue, int newValue) => PlayersChanged?.Invoke();

        private void OnStateChanged(ulong oldValue, ulong newValue) => PlayersChanged?.Invoke();

        private void OnStateChanged(string oldValue, string newValue) => PlayersChanged?.Invoke();

        private void OnStateChanged(PrototypeSkill oldValue, PrototypeSkill newValue) => PlayersChanged?.Invoke();

        private void OnStateChanged(bool oldValue, bool newValue) => PlayersChanged?.Invoke();
    }
}
