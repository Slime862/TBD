using System;
using System.IO;
using System.Text;
using TBD.Core;
using UnityEngine;

namespace TBD.Persistence
{
    public sealed class PlayerProfileStore
    {
        private const string FileName = "player_profile.json";
        private readonly string filePath;

        public PlayerProfileStore(string directoryPath = null)
        {
            var targetDirectory = string.IsNullOrWhiteSpace(directoryPath)
                ? Application.persistentDataPath
                : directoryPath;
            filePath = Path.Combine(targetDirectory, FileName);
        }

        public string FilePath => filePath;

        public CharacterLoadout LoadOrDefault()
        {
            if (!File.Exists(filePath))
            {
                return CharacterLoadout.CreateDefault();
            }

            try
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                var profile = JsonUtility.FromJson<PlayerProfileData>(json);
                if (profile == null || profile.version != PrototypeRules.ProfileVersion)
                {
                    Debug.LogWarning($"角色配置版本不受支持，已使用默认配置。文件：{filePath}");
                    return CharacterLoadout.CreateDefault();
                }

                return profile.loadout.Sanitized();
            }
            catch (Exception exception)
            {
                Debug.LogError($"读取角色配置失败，已使用默认配置。文件：{filePath}\n{exception}");
                return CharacterLoadout.CreateDefault();
            }
        }

        public void Save(CharacterLoadout loadout)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException($"角色配置路径无效：{filePath}");
            }

            Directory.CreateDirectory(directory);
            var profile = new PlayerProfileData
            {
                version = PrototypeRules.ProfileVersion,
                loadout = loadout.Sanitized(),
            };
            File.WriteAllText(filePath, JsonUtility.ToJson(profile, true), new UTF8Encoding(false));
        }
    }
}
