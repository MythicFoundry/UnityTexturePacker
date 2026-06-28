#if UNITY_EDITOR
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace ChannelPacker
{
    public class ChannelPackerPreset : ScriptableObject
    {
        #if ODIN_INSPECTOR
        [ListDrawerSettings(ListElementLabelName = nameof(name))]
        #endif
        [SerializeField]
        public ChannelPackerChannel[] channels =
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
        
        public ChannelPackerChannel this[int index]
        {
            get => channels[index];
            set => channels[index] = value;
        }
    }
}
#endif