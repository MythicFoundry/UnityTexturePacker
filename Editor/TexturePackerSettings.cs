#nullable enable

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace MythicFoundry.TexturePacker.Editor
{
    [FilePath("UserSettings/TexturePackerSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class TexturePackerSettings : ScriptableSingleton<TexturePackerSettings>
    {
        public const int ChannelCount = 4;

        public TexturePackerPreset? lastPreset;

        public Texture2D?[] lastInputs = new Texture2D?[ChannelCount];
        
        public ColorChannel[] lastOutputs = CreateDefaultOutputs();

        public Texture2D? GetLastInput(int channelIndex)
        {
            EnsureLastInputs();
            
            return !IsValidChannelIndex(channelIndex) ? null : lastInputs[channelIndex];
        }

        public bool SetLastInput(int channelIndex, Texture2D? input)
        {
            EnsureLastInputs();
            
            if (!IsValidChannelIndex(channelIndex) || lastInputs[channelIndex] == input)
                return false;

            lastInputs[channelIndex] = input;
            return true;
        }
        
        public ColorChannel GetLastOutput(int channelIndex)
        {
            EnsureLastOutputs();
            
            return !IsValidChannelIndex(channelIndex)
                ? ChannelIndexToColorChannel(channelIndex)
                : lastOutputs[channelIndex];
        }

        public bool SetLastOutput(int channelIndex, ColorChannel output)
        {
            EnsureLastOutputs();
            if (!IsValidChannelIndex(channelIndex) || lastOutputs[channelIndex] == output)
                return false;

            lastOutputs[channelIndex] = output;
            return true;
        }

        public void EnsureLastInputs()
        {
            if (lastInputs is { Length: ChannelCount })
                return;

            Texture2D?[] resized = new Texture2D?[ChannelCount];
            if (lastInputs != null)
            {
                int copyCount = lastInputs.Length < resized.Length ? lastInputs.Length : resized.Length;
                for (int index = 0; index < copyCount; index++)
                {
                    resized[index] = lastInputs[index];
                }
            }

            lastInputs = resized;
        }
        
        public void EnsureLastOutputs()
        {
            if (lastOutputs is { Length: ChannelCount })
                return;

            ColorChannel[] resized = CreateDefaultOutputs();
            if (lastOutputs != null)
            {
                int copyCount = lastOutputs.Length < resized.Length ? lastOutputs.Length : resized.Length;
                for (int index = 0; index < copyCount; index++)
                {
                    resized[index] = lastOutputs[index];
                }
            }

            lastOutputs = resized;
        }

        public void SaveSettings()
        {
            Save(true);
        }

        private static bool IsValidChannelIndex(int channelIndex)
        {
            return channelIndex is >= 0 and < ChannelCount;
        }
        
        private static ColorChannel[] CreateDefaultOutputs()
        {
            ColorChannel[] outputs = new ColorChannel[ChannelCount];
            for (int index = 0; index < ChannelCount; index++)
            {
                outputs[index] = ChannelIndexToColorChannel(index);
            }

            return outputs;
        }

        private static ColorChannel ChannelIndexToColorChannel(int channelIndex)
        {
            if (channelIndex is < 0 or >= ChannelCount)
                return ColorChannel.R;

            return (ColorChannel)channelIndex;
        }
    }
}

#endif
