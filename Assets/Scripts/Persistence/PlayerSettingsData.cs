using System;

namespace Hearts.Persistence
{
    [Serializable]
    public class PlayerSettingsData
    {
        public int Version = 1;
        public string DisplayName = "Player";
        public int AvatarIndex;
        public float MasterVolume = 1f;
        public float SfxVolume = 1f;

        public const int CurrentVersion = 1;

        public static PlayerSettingsData CreateDefault()
        {
            return new PlayerSettingsData();
        }

        public bool Sanitize()
        {
            bool changed = false;
            if (Version != CurrentVersion)
            {
                Version = CurrentVersion;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = "Player";
                changed = true;
            }
            DisplayName = DisplayName.Trim();
            if (DisplayName.Length > 24)
            {
                DisplayName = DisplayName.Substring(0, 24);
                changed = true;
            }
            if (AvatarIndex < 0 || AvatarIndex > 12)
            {
                AvatarIndex = 0;
                changed = true;
            }
            if (float.IsNaN(MasterVolume) || MasterVolume < 0f || MasterVolume > 1f)
            {
                MasterVolume = 1f;
                changed = true;
            }
            if (float.IsNaN(SfxVolume) || SfxVolume < 0f || SfxVolume > 1f)
            {
                SfxVolume = 1f;
                changed = true;
            }
            return changed;
        }
    }
}
