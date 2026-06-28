#nullable enable

using UnityEngine.Rendering;

namespace MythicFoundry.TexturePacker.Editor
{
    internal static class RenderPipelineAssetExtensions
    {
        public static string? GetFullName(this RenderPipelineAsset pipeline)
        {
            return pipeline.GetType().FullName;
        }

        internal static RenderPipeline? GetEnum(this RenderPipelineAsset? pipeline)
        {
            string? pipelineName = pipeline?.GetFullName();
            if (string.IsNullOrWhiteSpace(pipelineName))
            {
                return RenderPipeline.Standard;
            }
            
            if (pipelineName?.Contains("Universal") ?? false)
            {
                return RenderPipeline.Universal;
            }
            
            if (pipelineName?.Contains("HDRenderPipeline") ?? false)
            {
                return RenderPipeline.HighDefinition;
            }

            return null;
        }
    }
}