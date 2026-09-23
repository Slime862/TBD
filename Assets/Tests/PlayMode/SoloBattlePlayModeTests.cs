using System.Collections;
using NUnit.Framework;
using TBD.Core;
using UnityEngine.TestTools;

namespace TBD.Tests.PlayMode
{
    public sealed class SoloBattlePlayModeTests
    {
        [UnityTest]
        public IEnumerator SoloBattle_WithFixedAi_CompletesWithoutSteam()
        {
            var player = new LobbyPlayerState
            {
                slot = 0,
                displayName = "本地玩家",
                loadout = CharacterLoadout.CreateDefault(),
            };
            var companion = new LobbyPlayerState
            {
                slot = 1,
                displayName = "圣骑士 AI",
                loadout = PrototypeRules.CreateAiLoadout(),
                isAi = true,
            };
            var simulation = new BattleSimulation(player, companion, 480);
            var waveCount = 0;
            var supportActionCount = 0;
            var guard = 0;

            while (simulation.TryStep(out var battleEvent))
            {
                if (battleEvent.type == BattleEventType.WaveStarted)
                {
                    waveCount++;
                }

                if (battleEvent.type == BattleEventType.Heal || battleEvent.type == BattleEventType.Shield)
                {
                    supportActionCount++;
                }

                Assert.That(++guard, Is.LessThan(600));
                yield return null;
            }

            Assert.That(waveCount, Is.EqualTo(3));
            Assert.That(supportActionCount, Is.GreaterThan(0));
            Assert.That(simulation.HeroesWon, Is.True);
        }
    }
}
