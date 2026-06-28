#nullable enable

#if UNITY_EDITOR

using UnityEditor;

namespace ChannelPacker
{
    [FilePath("UserSettings/ChannelPackerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ChannelPackerSettings : ScriptableSingleton<ChannelPackerSettings>
    {
        public ChannelPackerPreset? lastPreset;

        public void SaveSettings()
        {
            Save(true);
        }
    }
}

#endif
