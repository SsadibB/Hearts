using System;
using System.IO;
using UnityEngine;

namespace Hearts.Persistence
{
    public static class SaveManager
    {
        const string FileName = "hearts-settings.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static PlayerSettingsData Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return PlayerSettingsData.CreateDefault();

                string json = File.ReadAllText(SavePath);
                if (string.IsNullOrWhiteSpace(json))
                    return Recover();

                var data = JsonUtility.FromJson<PlayerSettingsData>(json);
                if (data == null)
                    return Recover();

                if (data.Sanitize())
                    Write(data);
                Apply(data);
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hearts settings could not be read and were reset. " + exception.Message);
                return Recover();
            }
        }

        public static void Save(PlayerSettingsData data)
        {
            if (data == null)
                data = PlayerSettingsData.CreateDefault();
            data.Sanitize();
            Write(data);
            Apply(data);
        }

        public static void Apply(PlayerSettingsData data)
        {
            if (data == null)
                return;
            AudioListener.volume = data.MasterVolume;
        }

        static PlayerSettingsData Recover()
        {
            var data = PlayerSettingsData.CreateDefault();
            try
            {
                Write(data);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Hearts settings could not be rewritten. " + exception.Message);
            }
            Apply(data);
            return data;
        }

        static void Write(PlayerSettingsData data)
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }
    }
}
