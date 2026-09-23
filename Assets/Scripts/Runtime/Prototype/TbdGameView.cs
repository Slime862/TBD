using System;
using System.Collections.Generic;
using System.Linq;
using TBD.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TBD.Prototype
{
    public sealed class TbdGameView : MonoBehaviour
    {
        [Header("页面")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject battlePanel;
        [SerializeField] private GameObject resultPanel;

        [Header("首页")]
        [SerializeField] private Button soloButton;
        [SerializeField] private Button createSteamButton;
        [SerializeField] private InputField lobbyIdInput;
        [SerializeField] private Button joinLobbyButton;
        [SerializeField] private Text homeStatusText;

        [Header("房间")]
        [SerializeField] private Image localPortrait;
        [SerializeField] private Text localConfigText;
        [SerializeField] private Text firstSlotText;
        [SerializeField] private Image firstSlotPortrait;
        [SerializeField] private Text secondSlotText;
        [SerializeField] private Image secondSlotPortrait;
        [SerializeField] private Text lobbyIdText;
        [SerializeField] private Text lobbyStatusText;
        [SerializeField] private Button previousPortraitButton;
        [SerializeField] private Button nextPortraitButton;
        [SerializeField] private Button nextColorButton;
        [SerializeField] private Button nextSkillAButton;
        [SerializeField] private Button nextSkillBButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startBattleButton;
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button copyLobbyIdButton;
        [SerializeField] private Button leaveButton;

        [Header("战斗")]
        [SerializeField] private Text waveText;
        [SerializeField] private Text battleLogText;
        [SerializeField] private Text[] heroUnitTexts = Array.Empty<Text>();
        [SerializeField] private Image[] heroUnitPortraits = Array.Empty<Image>();
        [SerializeField] private Image[] heroHealthFills = Array.Empty<Image>();
        [SerializeField] private Text[] enemyUnitTexts = Array.Empty<Text>();
        [SerializeField] private Image[] enemyUnitImages = Array.Empty<Image>();
        [SerializeField] private Image[] enemyHealthFills = Array.Empty<Image>();

        [Header("结果")]
        [SerializeField] private Text resultText;
        [SerializeField] private Button returnToLobbyButton;

        [Header("资源")]
        [SerializeField] private Sprite[] portraits = Array.Empty<Sprite>();

        private static readonly Color[] LoadoutColors =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(0.62f, 0.86f, 1f, 1f),
            new Color(0.68f, 1f, 0.7f, 1f),
            new Color(1f, 0.72f, 0.78f, 1f),
        };

        private readonly int[] heroHp = new int[2];
        private readonly int[] heroMaxHp = new int[2];
        private readonly int[] heroShield = new int[2];
        private readonly string[] heroNames = new string[2];
        private readonly int[] enemyHp = new int[2];
        private readonly int[] enemyMaxHp = new int[2];
        private readonly int[] enemyShield = new int[2];
        private readonly string[] enemyNames = new string[2];

        public string LobbyIdInput => lobbyIdInput.text.Trim();

        private void Awake()
        {
            // 构建场景只保存内置字体引用，运行时切换到系统中文字体以复用现有窗口方案。
            var uiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
                16);
            if (uiFont == null)
            {
                return;
            }

            foreach (var label in GetComponentsInChildren<Text>(true))
            {
                label.font = uiFont;
            }
        }

        public void Initialize(TbdGameController controller, bool steamAvailable)
        {
            soloButton.onClick.AddListener(controller.StartSolo);
            createSteamButton.onClick.AddListener(controller.CreateSteamLobby);
            joinLobbyButton.onClick.AddListener(controller.JoinLobbyFromInput);
            previousPortraitButton.onClick.AddListener(() => controller.ChangePortrait(-1));
            nextPortraitButton.onClick.AddListener(() => controller.ChangePortrait(1));
            nextColorButton.onClick.AddListener(controller.ChangeColor);
            nextSkillAButton.onClick.AddListener(() => controller.ChangeSkill(0));
            nextSkillBButton.onClick.AddListener(() => controller.ChangeSkill(1));
            readyButton.onClick.AddListener(controller.ToggleReady);
            startBattleButton.onClick.AddListener(controller.StartBattle);
            inviteButton.onClick.AddListener(controller.InviteFriends);
            copyLobbyIdButton.onClick.AddListener(controller.CopyLobbyId);
            leaveButton.onClick.AddListener(controller.LeaveToHome);
            returnToLobbyButton.onClick.AddListener(controller.ReturnToLobby);
            createSteamButton.interactable = steamAvailable;
            joinLobbyButton.interactable = steamAvailable;
        }

        public void ShowState(GameFlowState state)
        {
            homePanel.SetActive(state == GameFlowState.Home);
            lobbyPanel.SetActive(state == GameFlowState.Lobby);
            battlePanel.SetActive(state == GameFlowState.Battle);
            resultPanel.SetActive(state == GameFlowState.Result);
        }

        public void SetHomeStatus(string message)
        {
            homeStatusText.text = message;
        }

        public void RenderLoadout(CharacterLoadout loadout)
        {
            loadout = loadout.Sanitized();
            ApplyPortrait(localPortrait, loadout);
            localConfigText.text = $"外观 {loadout.portraitId + 1}  ·  颜色 {loadout.colorId + 1}\n技能：{SkillName(loadout.skillA)} + {SkillName(loadout.skillB)}";
        }

        public void RenderLobby(
            GameMode mode,
            IReadOnlyList<LobbyPlayerState> players,
            bool localReady,
            bool canStart,
            string lobbyId,
            string status)
        {
            var ordered = players.OrderBy(player => player.slot).ToArray();
            RenderSlot(0, ordered.Length > 0 ? ordered[0] : (LobbyPlayerState?)null, firstSlotText, firstSlotPortrait);
            RenderSlot(1, ordered.Length > 1 ? ordered[1] : (LobbyPlayerState?)null, secondSlotText, secondSlotPortrait);
            readyButton.GetComponentInChildren<Text>().text = localReady ? "取消准备" : "准备";
            startBattleButton.interactable = canStart;
            inviteButton.gameObject.SetActive(mode == GameMode.SteamCoop);
            copyLobbyIdButton.gameObject.SetActive(mode == GameMode.SteamCoop);
            lobbyIdText.gameObject.SetActive(mode == GameMode.SteamCoop);
            lobbyIdText.text = string.IsNullOrEmpty(lobbyId) ? "Steam 房间创建中…" : $"大厅 ID  {lobbyId}";
            lobbyStatusText.text = status;
        }

        public void BeginBattle(LobbyPlayerState first, LobbyPlayerState second)
        {
            ShowState(GameFlowState.Battle);
            var players = new[] { first, second };
            for (var i = 0; i < players.Length; i++)
            {
                heroNames[i] = players[i].displayName;
                heroMaxHp[i] = players[i].isAi ? 120 : 105;
                heroHp[i] = heroMaxHp[i];
                heroShield[i] = 0;
                ApplyPortrait(heroUnitPortraits[i], players[i].loadout);
                RenderHero(i);
            }

            battleLogText.text = "准备战斗…";
        }

        public void ApplyBattleEvent(BattleEvent battleEvent)
        {
            battleLogText.text = battleEvent.message;
            if (battleEvent.type == BattleEventType.WaveStarted)
            {
                SetupWave(battleEvent.waveIndex);
                return;
            }

            if (battleEvent.type == BattleEventType.Damage || battleEvent.type == BattleEventType.Heal ||
                battleEvent.type == BattleEventType.Shield || battleEvent.type == BattleEventType.UnitDied)
            {
                var slot = battleEvent.targetSlot;
                if (battleEvent.targetSide == BattleSide.Heroes && slot >= 0 && slot < heroUnitTexts.Length)
                {
                    heroHp[slot] = battleEvent.targetHp;
                    heroShield[slot] = battleEvent.targetShield;
                    RenderHero(slot);
                }
                else if (slot >= 0 && slot < enemyUnitTexts.Length)
                {
                    enemyHp[slot] = battleEvent.targetHp;
                    enemyShield[slot] = battleEvent.targetShield;
                    RenderEnemy(slot);
                }
            }
        }

        public void ShowResult(bool heroesWon)
        {
            ShowState(GameFlowState.Result);
            resultText.text = heroesWon ? "胜 利" : "失 败";
        }

        public void SetReturnButtonInteractable(bool interactable)
        {
            returnToLobbyButton.interactable = interactable;
        }

        private void SetupWave(int waveIndex)
        {
            waveText.text = waveIndex < 2 ? $"第 {waveIndex + 1} 波" : "BOSS 战";
            if (waveIndex == 0)
            {
                SetEnemy(0, "软泥怪", 45);
                SetEnemy(1, "软泥怪", 45);
            }
            else if (waveIndex == 1)
            {
                SetEnemy(0, "蛮力怪", 95);
                SetEnemy(1, "小恶魔", 55);
            }
            else
            {
                SetEnemy(0, "深渊领主", 220);
                SetEnemy(1, string.Empty, 0);
            }
        }

        private void SetEnemy(int slot, string unitName, int maxHp)
        {
            enemyNames[slot] = unitName;
            enemyMaxHp[slot] = maxHp;
            enemyHp[slot] = maxHp;
            enemyShield[slot] = 0;
            RenderEnemy(slot);
        }

        private void RenderHero(int slot)
        {
            heroUnitTexts[slot].text = $"{heroNames[slot]}\nHP {heroHp[slot]}/{heroMaxHp[slot]}{ShieldSuffix(heroShield[slot])}";
            heroHealthFills[slot].fillAmount = heroMaxHp[slot] > 0 ? (float)heroHp[slot] / heroMaxHp[slot] : 0f;
        }

        private void RenderEnemy(int slot)
        {
            var active = enemyMaxHp[slot] > 0;
            enemyUnitTexts[slot].gameObject.SetActive(active);
            enemyUnitImages[slot].gameObject.SetActive(active);
            enemyHealthFills[slot].transform.parent.gameObject.SetActive(active);
            enemyUnitTexts[slot].text = $"{enemyNames[slot]}\nHP {enemyHp[slot]}/{enemyMaxHp[slot]}{ShieldSuffix(enemyShield[slot])}";
            enemyHealthFills[slot].fillAmount = active ? (float)enemyHp[slot] / enemyMaxHp[slot] : 0f;
        }

        private void RenderSlot(int slot, LobbyPlayerState? state, Text label, Image portrait)
        {
            if (!state.HasValue)
            {
                label.text = $"槽位 {slot + 1}\n等待玩家…";
                portrait.enabled = false;
                return;
            }

            var player = state.Value;
            portrait.enabled = true;
            ApplyPortrait(portrait, player.loadout);
            label.text = $"{player.displayName}\n{SkillName(player.loadout.skillA)} + {SkillName(player.loadout.skillB)}\n{(player.ready ? "已准备" : "未准备")}";
        }

        private void ApplyPortrait(Image image, CharacterLoadout loadout)
        {
            image.sprite = portraits[loadout.portraitId];
            image.color = LoadoutColors[loadout.colorId];
            image.preserveAspect = true;
        }

        private static string SkillName(PrototypeSkill skill)
        {
            return skill == PrototypeSkill.PowerStrike ? "强击" : skill == PrototypeSkill.Heal ? "治疗" : "护盾";
        }

        private static string ShieldSuffix(int shield)
        {
            return shield > 0 ? $"  盾 {shield}" : string.Empty;
        }
    }
}
