#nullable enable

#if UNITY_EDITOR
using System;

namespace ChannelPacker
{
    [Serializable]
    public class ChannelPackerChannel
    {
        public string name = string.Empty;
        
        public float @default = 1;
        
        public ChannelPacker.ColorChannel from;
        
        public bool invert;

        public static ChannelPackerChannel FromChannel(TextureChannel channel)
        {
            return new ChannelPackerChannel
            {
                @default = channel.@default,
                from = channel.from,
                invert = channel.invert
            };
        }
    }
}

#endif