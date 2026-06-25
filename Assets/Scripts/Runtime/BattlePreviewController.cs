using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattlePreviewController : MonoBehaviour
{
    [Serializable]
    public sealed class UnitBinding
    {
        public RectTransform root;
        public Image portrait;
        public Image healthFill;
        public Text hitText;
        public Vector2 basePosition;
        public Color baseColor = Color.white;
        public float hp = 1f;
        public float flashTime;
        public float hitTextTime;
    }

    [SerializeField] private UnitBinding[] heroes = Array.Empty<UnitBinding>();
    [SerializeField] private UnitBinding[] enemies = Array.Empty<UnitBinding>();
    [SerializeField] private Text statusText;
    [SerializeField] private float attackInterval = 0.9f;
    [SerializeField] private float flashDuration = 0.22f;

    private float attackTimer;
    private int attackIndex;
    private bool heroTurn = true;
    private bool hasBindings;

    public void Bind(UnitBinding[] heroBindings, UnitBinding[] enemyBindings, Text boundStatusText)
    {
        heroes = heroBindings ?? Array.Empty<UnitBinding>();
        enemies = enemyBindings ?? Array.Empty<UnitBinding>();
        statusText = boundStatusText;
        TryInitializeBindings("BattlePreviewController 绑定失败，4v4 示意战斗不会播放。");
    }

    private void Start()
    {
        if (!hasBindings && !TryInitializeBindings("BattlePreviewController 缺少有效 UI 引用，无法播放示意战斗。"))
        {
            return;
        }
    }

    private void Update()
    {
        if (!hasBindings)
        {
            return;
        }

        var deltaTime = Time.unscaledDeltaTime;
        attackTimer += deltaTime;

        if (attackTimer >= Mathf.Max(0.1f, attackInterval))
        {
            attackTimer = 0f;
            PlayNextAttack();
        }

        TickUnits(heroes, deltaTime);
        TickUnits(enemies, deltaTime);
    }

    private void PlayNextAttack()
    {
        var attackers = heroTurn ? heroes : enemies;
        var targets = heroTurn ? enemies : heroes;

        if (attackers.Length == 0 || targets.Length == 0)
        {
            Debug.LogError("BattlePreviewController 没有可用攻击方或目标方。", this);
            return;
        }

        var attacker = attackers[attackIndex % attackers.Length];
        var target = targets[(attackIndex + 1) % targets.Length];
        attackIndex++;
        heroTurn = !heroTurn;

        attacker.flashTime = flashDuration;
        target.flashTime = flashDuration;
        target.hitTextTime = 0.55f;
        target.hp = Mathf.Max(0f, target.hp - 0.18f);

        if (target.hitText != null)
        {
            target.hitText.text = "-18";
        }

        if (AllDefeated(targets))
        {
            ResetHealth(targets);
            RefreshStatus(heroTurn ? "敌方重整队列" : "英雄队重整队列");
        }
        else
        {
            RefreshStatus(heroTurn ? "敌方反击" : "英雄出手");
        }
    }

    private void TickUnits(UnitBinding[] units, float deltaTime)
    {
        foreach (var unit in units)
        {
            if (unit == null)
            {
                continue;
            }

            unit.flashTime = Mathf.Max(0f, unit.flashTime - deltaTime);
            unit.hitTextTime = Mathf.Max(0f, unit.hitTextTime - deltaTime);
            RefreshUnit(unit);
        }
    }

    private void RefreshAllUnits()
    {
        foreach (var hero in heroes)
        {
            RefreshUnit(hero);
        }

        foreach (var enemy in enemies)
        {
            RefreshUnit(enemy);
        }
    }

    private void RefreshUnit(UnitBinding unit)
    {
        if (unit == null)
        {
            return;
        }

        var flash = flashDuration <= 0f ? 0f : Mathf.Clamp01(unit.flashTime / flashDuration);

        if (unit.root != null)
        {
            var direction = unit.basePosition.x < 150f ? Vector2.right : Vector2.left;
            unit.root.anchoredPosition = unit.basePosition + direction * (flash * 8f);
        }

        if (unit.portrait != null)
        {
            unit.portrait.color = Color.Lerp(unit.baseColor, Color.white, flash);
        }

        if (unit.healthFill != null)
        {
            unit.healthFill.fillAmount = Mathf.Clamp01(unit.hp);
        }

        if (unit.hitText != null)
        {
            var color = unit.hitText.color;
            color.a = Mathf.Clamp01(unit.hitTextTime / 0.55f);
            unit.hitText.color = color;
        }
    }

    private bool ValidateBindings()
    {
        if (heroes.Length != 4 || enemies.Length != 4)
        {
            Debug.LogError($"BattlePreviewController 需要左右各 4 个单位，当前英雄 {heroes.Length} 个，敌人 {enemies.Length} 个。", this);
            return false;
        }

        return ValidateUnits(heroes, "英雄") && ValidateUnits(enemies, "敌人");
    }

    private bool ValidateUnits(UnitBinding[] units, string label)
    {
        for (var i = 0; i < units.Length; i++)
        {
            var unit = units[i];
            if (unit == null || unit.root == null || unit.portrait == null || unit.healthFill == null || unit.hitText == null)
            {
                Debug.LogError($"BattlePreviewController 第 {i + 1} 个{label}单位缺少 UI 引用。", this);
                return false;
            }
        }

        return true;
    }

    private bool TryInitializeBindings(string errorMessage)
    {
        hasBindings = ValidateBindings();
        if (!hasBindings)
        {
            Debug.LogError(errorMessage, this);
            return false;
        }

        CaptureBaseState(heroes);
        CaptureBaseState(enemies);
        RefreshAllUnits();
        RefreshStatus("普通关自动战斗");
        return true;
    }

    private void CaptureBaseState(UnitBinding[] units)
    {
        foreach (var unit in units)
        {
            unit.basePosition = unit.root.anchoredPosition;
            unit.baseColor = unit.portrait.color;
            unit.hp = 1f;
            unit.flashTime = 0f;
            unit.hitTextTime = 0f;
        }
    }

    private bool AllDefeated(UnitBinding[] units)
    {
        foreach (var unit in units)
        {
            if (unit.hp > 0f)
            {
                return false;
            }
        }

        return true;
    }

    private void ResetHealth(UnitBinding[] units)
    {
        foreach (var unit in units)
        {
            unit.hp = 1f;
        }
    }

    private void RefreshStatus(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }
}
