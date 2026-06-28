#nullable enable

using UnityEngine;

namespace ChannelPacker
{
    internal enum RenderPipeline
    {
        Standard,

        Universal,

        HighDefinition
    }

    internal static class RenderPipelineExtensions
    {
        public static string GetMapProperty(this RenderPipeline pipeline)
        {
            return pipeline switch
            {
                RenderPipeline.Standard       => "_MetallicGlossMap",
                RenderPipeline.Universal      => "_MetallicGlossMap",
                RenderPipeline.HighDefinition => "_MaskMap",
                _                             => string.Empty
            };
        }

        public static string GetShaderKeyword(this RenderPipeline pipeline, string? mapProperty)
        {
            if (string.IsNullOrEmpty(mapProperty))
            {
                return string.Empty;
            }
            
            return pipeline switch
            {
                RenderPipeline.Standard                                          => "_METALLICGLOSSMAP",
                RenderPipeline.Universal when mapProperty is "_MetallicGlossMap" => "_METALLICSPECGLOSSMAP",
                RenderPipeline.Universal                                         => "_METALLICGLOSSMAP",
                RenderPipeline.HighDefinition                                    => "_MASKMAP",
                _                                                                => string.Empty
            };
        }

        public static string GetDefaultShaderName(this RenderPipeline pipeline)
        {
            return pipeline switch
            {
                RenderPipeline.Universal      => "Universal Render Pipeline/Lit",
                RenderPipeline.HighDefinition => "HDRP/Lit",
                RenderPipeline.Standard       => "Standard",
                _                             => string.Empty
            };
        }

        public static Shader? GetDefaultShader(this RenderPipeline pipeline)
        {
            return Shader.Find(pipeline.GetDefaultShaderName());
        }

        public static RenderPipeline? FromShaderName(string value)
        {
            return value switch
            {
                "Universal Render Pipeline/Lit" => RenderPipeline.Universal,
                "HDRP/Lit"                      => RenderPipeline.HighDefinition,
                "Standard"                      => RenderPipeline.Standard,
                _                               => null
            };
        }

        public static RenderPipeline? FromShaderName(Shader shader)
        {
            return shader.name switch
            {
                "Universal Render Pipeline/Lit" => RenderPipeline.Universal,
                "HDRP/Lit"                      => RenderPipeline.HighDefinition,
                "Standard"                      => RenderPipeline.Standard,
                _                               => null
            };
        }
    }
}