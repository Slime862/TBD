using System;
using System.Collections.Generic;

namespace TBD.Core
{
    public sealed class BattleUnitState
    {
        public string id;
        public string displayName;
        public BattleSide side;
        public int slot;
        public int maxHp;
        public int hp;
        public int attack;
        public int speed;
        public int shield;
        public bool isBoss;

        public bool IsAlive => hp > 0;
    }

    public sealed class BattleSimulation
    {
        private sealed class RuntimeUnit
        {
            public BattleUnitState state;
            public CharacterLoadout loadout;
            public int ownTurn;
            public readonly int[] nextSkillTurn = new int[PrototypeRules.SkillCount];
        }

        private readonly List<RuntimeUnit> heroes = new List<RuntimeUnit>();
        private readonly List<RuntimeUnit> enemies = new List<RuntimeUnit>();
        private readonly List<RuntimeUnit> turnOrder = new List<RuntimeUnit>();
        private readonly Queue<BattleEvent> pendingEvents = new Queue<BattleEvent>();
        private readonly Random random;
        private int waveIndex = -1;
        private int turnIndex;
        private int sequence;
        private bool completed;

        public BattleSimulation(LobbyPlayerState firstHero, LobbyPlayerState secondHero, int seed)
        {
            Seed = seed;
            random = new Random(seed);
            heroes.Add(CreateHero(firstHero, 0));
            heroes.Add(CreateHero(secondHero, 1));
            StartNextWave();
        }

        public int Seed { get; }

        public BattleState State => new BattleState
        {
            waveIndex = waveIndex,
            actionSequence = sequence,
            randomSeed = Seed,
        };

        public int WaveIndex => waveIndex;

        public bool IsCompleted => completed;

        public bool HeroesWon { get; private set; }

        public IReadOnlyList<BattleUnitState> Heroes => GetStates(heroes);

        public IReadOnlyList<BattleUnitState> Enemies => GetStates(enemies);

        public bool TryStep(out BattleEvent battleEvent)
        {
            if (pendingEvents.Count > 0)
            {
                battleEvent = pendingEvents.Dequeue();
                return true;
            }

            if (completed)
            {
                battleEvent = default;
                return false;
            }

            if (AllDefeated(heroes))
            {
                CompleteBattle(false);
                battleEvent = pendingEvents.Dequeue();
                return true;
            }

            if (AllDefeated(enemies))
            {
                if (waveIndex >= 2)
                {
                    CompleteBattle(true);
                }
                else
                {
                    StartNextWave();
                }

                battleEvent = pendingEvents.Dequeue();
                return true;
            }

            var actor = GetNextActor();
            if (actor == null)
            {
                throw new InvalidOperationException("战斗中没有可行动单位，但双方仍未全部阵亡。");
            }

            actor.ownTurn++;
            ResolveTurn(actor);
            battleEvent = pendingEvents.Dequeue();
            return true;
        }

        private static RuntimeUnit CreateHero(LobbyPlayerState player, int slot)
        {
            var loadout = player.loadout.Sanitized();
            return new RuntimeUnit
            {
                loadout = loadout,
                state = new BattleUnitState
                {
                    id = player.isAi ? "ai_paladin" : $"player_{slot}",
                    displayName = string.IsNullOrWhiteSpace(player.displayName) ? $"英雄{slot + 1}" : player.displayName,
                    side = BattleSide.Heroes,
                    slot = slot,
                    maxHp = player.isAi ? 120 : 105,
                    hp = player.isAi ? 120 : 105,
                    attack = player.isAi ? 13 : 18,
                    speed = player.isAi ? 9 : 12,
                },
            };
        }

        private void StartNextWave()
        {
            waveIndex++;
            enemies.Clear();

            if (waveIndex == 0)
            {
                enemies.Add(CreateEnemy("slime_a", "软泥怪", 0, 45, 7, 8));
                enemies.Add(CreateEnemy("slime_b", "软泥怪", 1, 45, 7, 7));
            }
            else if (waveIndex == 1)
            {
                enemies.Add(CreateEnemy("brute", "蛮力怪", 0, 95, 13, 7));
                enemies.Add(CreateEnemy("imp", "小恶魔", 1, 55, 9, 11));
            }
            else
            {
                enemies.Add(CreateEnemy("boss", "深渊领主", 0, 220, 16, 9, true));
            }

            RebuildTurnOrder();
            pendingEvents.Enqueue(NewEvent(BattleEventType.WaveStarted, $"第 {waveIndex + 1} 波开始"));
        }

        private static RuntimeUnit CreateEnemy(
            string id,
            string displayName,
            int slot,
            int hp,
            int attack,
            int speed,
            bool isBoss = false)
        {
            return new RuntimeUnit
            {
                state = new BattleUnitState
                {
                    id = id,
                    displayName = displayName,
                    side = BattleSide.Enemies,
                    slot = slot,
                    maxHp = hp,
                    hp = hp,
                    attack = attack,
                    speed = speed,
                    isBoss = isBoss,
                },
            };
        }

        private void ResolveTurn(RuntimeUnit actor)
        {
            if (actor.state.side == BattleSide.Enemies)
            {
                ResolveEnemyTurn(actor);
                return;
            }

            RuntimeUnit target;
            if (CanUse(actor, PrototypeSkill.Heal) && TryGetLowestHealthAlly(actor.state.side, 0.6f, out target))
            {
                UseHeal(actor, target);
                return;
            }

            if (CanUse(actor, PrototypeSkill.Shield) && TryGetShieldTarget(actor.state.side, out target))
            {
                UseShield(actor, target);
                return;
            }

            target = GetFirstAlive(enemies);
            if (CanUse(actor, PrototypeSkill.PowerStrike))
            {
                SetCooldown(actor, PrototypeSkill.PowerStrike, 2);
                UseDamage(actor, target, (int)Math.Round(actor.state.attack * 1.6f), "强击");
                return;
            }

            UseDamage(actor, target, actor.state.attack, "普通攻击");
        }

        private void ResolveEnemyTurn(RuntimeUnit actor)
        {
            // 敌人目标由房主种子决定；同一输入和种子在双方播放时保持完全一致。
            var target = GetRandomAlive(heroes);
            var isBossStrike = actor.state.isBoss && actor.ownTurn % 3 == 0;
            var damage = isBossStrike ? (int)Math.Round(actor.state.attack * 1.5f) : actor.state.attack;
            UseDamage(actor, target, damage, isBossStrike ? "Boss 强袭" : "怪物攻击");
        }

        private void UseDamage(RuntimeUnit actor, RuntimeUnit target, int amount, string actionName)
        {
            pendingEvents.Enqueue(NewTargetEvent(
                BattleEventType.Attack,
                actor,
                target,
                amount,
                $"{actor.state.displayName} 使用{actionName}"));

            var absorbed = Math.Min(target.state.shield, amount);
            target.state.shield -= absorbed;
            var hpDamage = amount - absorbed;
            target.state.hp = Math.Max(0, target.state.hp - hpDamage);

            pendingEvents.Enqueue(NewTargetEvent(
                BattleEventType.Damage,
                actor,
                target,
                amount,
                $"{actor.state.displayName} 使用{actionName}，对 {target.state.displayName} 造成 {amount} 点伤害"));

            if (!target.state.IsAlive)
            {
                pendingEvents.Enqueue(NewTargetEvent(
                    BattleEventType.UnitDied,
                    actor,
                    target,
                    0,
                    $"{target.state.displayName} 被击败"));
            }
        }

        private void UseHeal(RuntimeUnit actor, RuntimeUnit target)
        {
            SetCooldown(actor, PrototypeSkill.Heal, 3);
            var amount = (int)Math.Ceiling(target.state.maxHp * 0.3f);
            var previousHp = target.state.hp;
            target.state.hp = Math.Min(target.state.maxHp, target.state.hp + amount);
            amount = target.state.hp - previousHp;

            pendingEvents.Enqueue(NewTargetEvent(
                BattleEventType.Heal,
                actor,
                target,
                amount,
                $"{actor.state.displayName} 为 {target.state.displayName} 恢复 {amount} 点生命"));
        }

        private void UseShield(RuntimeUnit actor, RuntimeUnit target)
        {
            SetCooldown(actor, PrototypeSkill.Shield, 3);
            var amount = (int)Math.Ceiling(target.state.maxHp * 0.25f);
            target.state.shield = amount;

            pendingEvents.Enqueue(NewTargetEvent(
                BattleEventType.Shield,
                actor,
                target,
                amount,
                $"{actor.state.displayName} 为 {target.state.displayName} 添加 {amount} 点护盾"));
        }

        private bool CanUse(RuntimeUnit actor, PrototypeSkill skill)
        {
            return actor.loadout.Contains(skill) && actor.ownTurn >= actor.nextSkillTurn[(int)skill];
        }

        private static void SetCooldown(RuntimeUnit actor, PrototypeSkill skill, int skippedOwnTurns)
        {
            actor.nextSkillTurn[(int)skill] = actor.ownTurn + skippedOwnTurns + 1;
        }

        private bool TryGetLowestHealthAlly(BattleSide side, float threshold, out RuntimeUnit target)
        {
            var units = side == BattleSide.Heroes ? heroes : enemies;
            target = null;
            var lowestRatio = float.MaxValue;

            foreach (var unit in units)
            {
                if (!unit.state.IsAlive)
                {
                    continue;
                }

                var ratio = (float)unit.state.hp / unit.state.maxHp;
                if (ratio < threshold && ratio < lowestRatio)
                {
                    target = unit;
                    lowestRatio = ratio;
                }
            }

            return target != null;
        }

        private bool TryGetShieldTarget(BattleSide side, out RuntimeUnit target)
        {
            var units = side == BattleSide.Heroes ? heroes : enemies;
            target = null;
            var lowestRatio = float.MaxValue;

            foreach (var unit in units)
            {
                if (!unit.state.IsAlive || unit.state.shield > 0)
                {
                    continue;
                }

                var ratio = (float)unit.state.hp / unit.state.maxHp;
                if (ratio < 0.8f && ratio < lowestRatio)
                {
                    target = unit;
                    lowestRatio = ratio;
                }
            }

            return target != null;
        }

        private RuntimeUnit GetNextActor()
        {
            for (var i = 0; i < turnOrder.Count; i++)
            {
                var actor = turnOrder[turnIndex % turnOrder.Count];
                turnIndex = (turnIndex + 1) % turnOrder.Count;
                if (actor.state.IsAlive)
                {
                    return actor;
                }
            }

            return null;
        }

        private void RebuildTurnOrder()
        {
            turnOrder.Clear();
            turnOrder.AddRange(heroes);
            turnOrder.AddRange(enemies);
            turnOrder.Sort((left, right) =>
            {
                var speedCompare = right.state.speed.CompareTo(left.state.speed);
                if (speedCompare != 0)
                {
                    return speedCompare;
                }

                var sideCompare = left.state.side.CompareTo(right.state.side);
                return sideCompare != 0 ? sideCompare : left.state.slot.CompareTo(right.state.slot);
            });
            turnIndex = 0;
        }

        private void CompleteBattle(bool heroesWin)
        {
            completed = true;
            HeroesWon = heroesWin;
            var battleEvent = NewEvent(BattleEventType.BattleEnded, heroesWin ? "战斗胜利" : "战斗失败");
            battleEvent.heroesWin = heroesWin;
            pendingEvents.Enqueue(battleEvent);
        }

        private BattleEvent NewEvent(BattleEventType type, string message)
        {
            return new BattleEvent
            {
                sequence = ++sequence,
                type = type,
                waveIndex = waveIndex,
                message = message,
            };
        }

        private BattleEvent NewTargetEvent(
            BattleEventType type,
            RuntimeUnit source,
            RuntimeUnit target,
            int amount,
            string message)
        {
            var battleEvent = NewEvent(type, message);
            battleEvent.sourceSide = source.state.side;
            battleEvent.sourceSlot = source.state.slot;
            battleEvent.targetSide = target.state.side;
            battleEvent.targetSlot = target.state.slot;
            battleEvent.amount = amount;
            battleEvent.targetHp = target.state.hp;
            battleEvent.targetMaxHp = target.state.maxHp;
            battleEvent.targetShield = target.state.shield;
            return battleEvent;
        }

        private static RuntimeUnit GetFirstAlive(List<RuntimeUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit.state.IsAlive)
                {
                    return unit;
                }
            }

            throw new InvalidOperationException("没有可用目标。");
        }

        private RuntimeUnit GetRandomAlive(List<RuntimeUnit> units)
        {
            var aliveCount = 0;
            foreach (var unit in units)
            {
                if (unit.state.IsAlive)
                {
                    aliveCount++;
                }
            }

            var targetIndex = random.Next(aliveCount);
            foreach (var unit in units)
            {
                if (unit.state.IsAlive && targetIndex-- == 0)
                {
                    return unit;
                }
            }

            throw new InvalidOperationException("没有可用目标。");
        }

        private static bool AllDefeated(List<RuntimeUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit.state.IsAlive)
                {
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<BattleUnitState> GetStates(List<RuntimeUnit> units)
        {
            var result = new List<BattleUnitState>(units.Count);
            foreach (var unit in units)
            {
                result.Add(unit.state);
            }

            return result;
        }
    }
}
