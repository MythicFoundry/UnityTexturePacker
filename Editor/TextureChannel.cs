#nullable enable

#if UNITY_EDITOR

using UnityEngine;

namespace MythicFoundry.TexturePacker.Editor
{
    public class TextureChannel
    {
        public Texture2D? input;

        public float @default;

        public float mult = 1;

        public ColorChannel from;
        
        public ColorChannel to;

        public bool invert;

        public RenderTexture? blit;

        public static TextureChannel FromChannel(TexturePackerChannel channel)
        {
            return new TextureChannel
            {
                @default = channel.@default,
                from = channel.from,
                invert = channel.invert
            };
        }
    }
}

#endif