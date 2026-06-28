#nullable enable

#if UNITY_EDITOR

using UnityEngine;

namespace ChannelPacker
{
    public class TextureChannel
    {
        public Texture2D? input;

        public float @default;

        public float mult = 1;

        public ChannelPacker.ColorChannel from;

        public bool invert;

        public RenderTexture? blit;

        public static TextureChannel FromChannel(ChannelPackerChannel channel)
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