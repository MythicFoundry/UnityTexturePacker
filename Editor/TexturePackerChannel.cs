#nullable enable

#if UNITY_EDITOR
using System;

namespace MythicFoundry.TexturePacker.Editor
{
    [Serializable]
    public class TexturePackerChannel
    {
        public string name = string.Empty;
        
        public float @default = 1;
        
        public ColorChannel from;
        
        public bool invert;

        public static TexturePackerChannel FromChannel(TextureChannel channel)
        {
            return new TexturePackerChannel
            {
                @default = channel.@default,
                from = channel.from,
                invert = channel.invert
            };
        }
    }
}

#endif