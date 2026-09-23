using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TBD.Core;
using TBD.Persistence;

namespace TBD.Tests.EditMode
{
    public sealed class PrototypeCoreTests
    {
        [Test]
        public void CharacterLoadout_SanitizesBoundsAndDuplicateSkills()
        {
            var loadout = new CharacterLoadout
            {
                portraitId = 99,
                colorId = -4,
                skillA = PrototypeSkill.Shield,
                skillB = PrototypeSkill.Shield,
            };

            var sanitized = loadout.Sanitized();

            Assert.That(sanitized.portraitId, Is.EqualTo(PrototypeRules.PortraitCount - 1));
            Assert.That(sanitized.colorId, Is.Zero);
            Assert.That(sanitized.skillA, Is.Not.EqualTo(sanitized.skillB));
        }

        [Test]
        public void PlayerProfileStore_RoundTripsSanitizedLoadout()
        {
            var directory = Path.Combine(Path.GetTempPath(), "tbd-profile-tests", System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PlayerProfileStore(directory);
                var expected = new CharacterLoadout
                {
                    portraitId = 3,
                    colorId = 2,
                    skillA = PrototypeSkill.Heal,
                    skillB = PrototypeSkill.Shield,
                };

                store.Save(expected);

                Assert.That(store.LoadOrDefault(), Is.EqualTo(expected));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void BattleSimulation_IsDeterministicAndCompletesThreeWaves()
        {
            var firstRun = RunBattle(321);
            var secondRun = RunBattle(321);

            Assert.That(firstRun, Is.EqualTo(secondRun));
            Assert.That(firstRun.FindAll(line => line.Contains("WaveStarted")).Count, Is.EqualTo(3));
            Assert.That(firstRun[firstRun.Count - 1], Does.Contain("BattleEnded"));
            Assert.That(firstRun[firstRun.Count - 1], Does.Contain("True"));
        }

        [Test]
        public void BattleSimulation_UsesConfiguredSupportSkills()
        {
            var events = RunBattle(777);

            Assert.That(events.Exists(line => line.Contains("Heal")), Is.True);
            Assert.That(events.Exists(line => line.Contains("Shield")), Is.True);
            Assert.That(events.Exists(line => line.Contains("Damage")), Is.True);
        }

        [Test]
        public void LobbyRules_LoadoutChangeClearsReadyAndBothPlayersGateStart()
        {
            var first = new LobbyPlayerState { ready = true, loadout = CharacterLoadout.CreateDefault() };
            var second = new LobbyPlayerState { ready = true, loadout = CharacterLoadout.CreateDefault() };
            Assert.That(LobbyRules.CanStart(new[] { first, second }), Is.True);

            first = LobbyRules.ApplyLoadout(first, new CharacterLoadout
            {
                portraitId = 2,
                colorId = 1,
                skillA = PrototypeSkill.Heal,
                skillB = PrototypeSkill.Shield,
            });

            Assert.That(first.ready, Is.False);
            Assert.That(LobbyRules.CanStart(new[] { first, second }), Is.False);
        }

        private static List<string> RunBattle(int seed)
        {
            var player = new LobbyPlayerState
            {
                slot = 0,
                displayName = "Player",
                loadout = CharacterLoadout.CreateDefault(),
            };
            var companion = new LobbyPlayerState
            {
                slot = 1,
                displayName = "Paladin",
                loadout = PrototypeRules.CreateAiLoadout(),
                isAi = true,
            };
            var simulation = new BattleSimulation(player, companion, seed);
            var result = new List<string>();
            var guard = 0;

            while (simulation.TryStep(out var battleEvent))
            {
                result.Add($"{battleEvent.sequence}:{battleEvent.type}:{battleEvent.waveIndex}:{battleEvent.sourceSide}:{battleEvent.sourceSlot}:{battleEvent.targetSide}:{battleEvent.targetSlot}:{battleEvent.amount}:{battleEvent.targetHp}:{battleEvent.targetShield}:{battleEvent.heroesWin}");
                Assert.That(++guard, Is.LessThan(500), "战斗模拟未在合理行动数内结束。");
            }

            return result;
        }
    }
}
