using System;

namespace TBD.Core
{
    public enum GameMode
    {
        Solo,
        SteamCoop,
    }

    public enum GameFlowState
    {
        Home,
        Lobby,
        Battle,
        Result,
    }

    public enum PrototypeSkill
    {
        PowerStrike,
        Heal,
        Shield,
    }

    public enum BattleSide
    {
        Heroes,
        Enemies,
    }

    public enum BattleEventType
    {
        WaveStarted,
        Attack,
        Damage,
        Heal,
        Shield,
        UnitDied,
        BattleEnded,
    }

    [Serializable]
    public struct CharacterLoadout : IEquatable<CharacterLoadout>
    {
        public int portraitId;
        public int colorId;
        public PrototypeSkill skillA;
        public PrototypeSkill skillB;

        public static CharacterLoadout CreateDefault()
        {
            return new CharacterLoadout
            {
                portraitId = 0,
                colorId = 0,
                skillA = PrototypeSkill.PowerStrike,
                skillB = PrototypeSkill.Heal,
            };
        }

        public CharacterLoadout Sanitized()
        {
            var sanitized = this;
            sanitized.portraitId = Clamp(sanitized.portraitId, 0, PrototypeRules.PortraitCount - 1);
            sanitized.colorId = Clamp(sanitized.colorId, 0, PrototypeRules.ColorCount - 1);
            sanitized.skillA = SanitizeSkill(sanitized.skillA);
            sanitized.skillB = SanitizeSkill(sanitized.skillB);

            // 两个技能槽必须不同，非法或重复配置统一落到下一个有效技能。
            if (sanitized.skillA == sanitized.skillB)
            {
                sanitized.skillB = (PrototypeSkill)(((int)sanitized.skillA + 1) % PrototypeRules.SkillCount);
            }

            return sanitized;
        }

        public bool Contains(PrototypeSkill skill)
        {
            return skillA == skill || skillB == skill;
        }

        public bool Equals(CharacterLoadout other)
        {
            return portraitId == other.portraitId &&
                   colorId == other.colorId &&
                   skillA == other.skillA &&
                   skillB == other.skillB;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterLoadout other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = portraitId;
                hashCode = (hashCode * 397) ^ colorId;
                hashCode = (hashCode * 397) ^ (int)skillA;
                hashCode = (hashCode * 397) ^ (int)skillB;
                return hashCode;
            }
        }

        private static PrototypeSkill SanitizeSkill(PrototypeSkill skill)
        {
            return (int)skill >= 0 && (int)skill < PrototypeRules.SkillCount
                ? skill
                : PrototypeSkill.PowerStrike;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }

    [Serializable]
    public struct LobbyPlayerState
    {
        public int slot;
        public ulong steamId;
        public string displayName;
        public CharacterLoadout loadout;
        public bool ready;
        public bool isAi;
    }

    [Serializable]
    public struct BattleEvent
    {
        public int sequence;
        public BattleEventType type;
        public int waveIndex;
        public BattleSide sourceSide;
        public int sourceSlot;
        public BattleSide targetSide;
        public int targetSlot;
        public int amount;
        public int targetHp;
        public int targetMaxHp;
        public int targetShield;
        public bool heroesWin;
        public string message;
    }

    [Serializable]
    public struct BattleState
    {
        public int waveIndex;
        public int actionSequence;
        public int randomSeed;
    }

    [Serializable]
    public sealed class PlayerProfileData
    {
        public int version = PrototypeRules.ProfileVersion;
        public CharacterLoadout loadout = CharacterLoadout.CreateDefault();
    }

    public static class PrototypeRules
    {
        public const int AppId = 480;
        public const int ProtocolVersion = 1;
        public const int ProfileVersion = 1;
        public const int PortraitCount = 5;
        public const int ColorCount = 4;
        public const int SkillCount = 3;
        public const int PlayerCount = 2;

        public static CharacterLoadout CreateAiLoadout()
        {
            return new CharacterLoadout
            {
                portraitId = 0,
                colorId = 2,
                skillA = PrototypeSkill.Heal,
                skillB = PrototypeSkill.Shield,
            };
        }
    }

    public static class LobbyRules
    {
        public static bool CanStart(LobbyPlayerState[] players)
        {
            if (players == null || players.Length != PrototypeRules.PlayerCount)
            {
                return false;
            }

            return players[0].ready && players[1].ready;
        }

        public static LobbyPlayerState ApplyLoadout(LobbyPlayerState player, CharacterLoadout loadout)
        {
            player.loadout = loadout.Sanitized();
            player.ready = false;
            return player;
        }
    }
}
