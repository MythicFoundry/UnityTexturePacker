#nullable enable

using UnityEngine;

namespace MythicFoundry.TexturePacker.Editor
{
    internal static class ShaderExtensions
    {
        public static string GetPreviewShaderKeyword(this Shader? shader, string? mapProperty)
        {
            if (string.IsNullOrEmpty(mapProperty))
            {
                return string.Empty;
            }

            if (shader && shader.name == "Universal Render Pipeline/Lit" && mapProperty == "_MetallicGlossMap")
            {
                return "_METALLICSPECGLOSSMAP";
            }

            return mapProperty?.ToUpper() ?? string.Empty;
        }
    }
}