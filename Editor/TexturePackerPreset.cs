#if UNITY_EDITOR
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace MythicFoundry.TexturePacker.Editor
{
    public class TexturePackerPreset : ScriptableObject
    {
        #if ODIN_INSPECTOR
        [ListDrawerSettings(ListElementLabelName = nameof(name))]
        #endif
        [SerializeField]
        public TexturePackerChannel[] channels =
        {
            new() { name = "Red" },
            new() { name = "Green" },
            new() { name = "Blue" },
            new() { name = "Alpha" }
        };

        #if ODIN_INSPECTOR
        [TitleGroup("Preview")]
        #endif
        [SerializeField]
        public string previewShader;

        #if ODIN_INSPECTOR
        [TitleGroup("Preview")]
        #endif
        [SerializeField]
        public string previewMapKeyword;
        
        public TexturePackerChannel this[int index]
        {
            get => channels[index];
            set => channels[index] = value;
        }
    }
}
#endif