#nullable enable

#if UNITY_EDITOR

using UnityEditor;

namespace MythicFoundry.TexturePacker.Editor
{
    [FilePath("UserSettings/TexturePackerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class TexturePackerSettings : ScriptableSingleton<TexturePackerSettings>
    {
        public TexturePackerPreset? lastPreset;

        public void SaveSettings()
        {
            Save(true);
        }
    }
}

#endif
