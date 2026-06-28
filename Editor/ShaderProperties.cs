#nullable enable

using UnityEngine;
using UnityEngine.Rendering;

namespace ChannelPacker
{
    internal readonly struct ShaderProperties
    {
        public readonly Shader? Shader;

        public readonly string MapProperty;

        public readonly string ShaderKeyword;

        public ShaderProperties(Shader? shader = null, string mapProperty = "", string shaderKeyword = "")
        {
            Shader = shader;
            MapProperty = mapProperty;
            ShaderKeyword = shaderKeyword;
        }

        public static ShaderProperties FromShaderName(RenderPipeline? renderPipeline)
        {
            if (renderPipeline is null)
            {
                return new ShaderProperties();
            }
            
            string defaultMapProperty = renderPipeline.Value.GetMapProperty();
            Shader? defaultShader = renderPipeline.Value.GetDefaultShader();
            string shaderKeyword = renderPipeline.Value.GetShaderKeyword(defaultMapProperty);
            return new ShaderProperties(defaultShader, defaultMapProperty, shaderKeyword);
        }

        public static ShaderProperties GetPipelineDefaultPreviewShader()
        {
            return FromShaderName(GraphicsSettings.currentRenderPipeline.GetEnum());
        }
            
        public static ShaderProperties FromShader(Shader shader)
        {
            string shaderName = shader ? shader.name : string.Empty;
            return shaderName switch
            {
                "Universal Render Pipeline/Lit" => new ShaderProperties(shader, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP"),
                "HDRP/Lit"                      => new ShaderProperties(shader, "_MaskMap", "_MASKMAP"),
                _                               => new ShaderProperties(shader, "_MetallicGlossMap", "_METALLICGLOSSMAP")
            };
        }
    }
}
