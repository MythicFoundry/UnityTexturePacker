#if UNITY_EDITOR
/*
Channel Packer
Made by Camobiwon

Original code from: https://www.reddit.com/r/Unity3D/comments/glkvp2/i_made_another_mask_map_packer_for_hdrp/
Thank you original creator! This has been extremely useful to me, and whoever is using this, I hope Channel Packer is useful to you :)
*/

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using File = UnityEngine.Windows.File;

namespace ChannelPacker
{
    public class ChannelPacker : EditorWindow
    {
        private const string PackageName = "com.mythicfoundry.channel-packer";

        private const string PackageRoot = "Packages/" + PackageName;

        private const string PackageDefaultPresetPath = PackageRoot + "/ChannelPackerDefault.asset";

        private const string PackageFastPackPath = PackageRoot + "/Editor/ChannelPacker_FastPack.compute";

        private const string UserSettingsFolder = "Assets/ChannelPacker";

        private const string UserSettingsPath = UserSettingsFolder + "/ChannelPackerSettings.asset";

        private const string UserDefaultPresetPath = UserSettingsFolder + "/ChannelPackerDefault.asset";

        // Use a compute shader to greatly speed up packing time
        [SerializeField]
        private ComputeShader fastPack;

        [SerializeField]
        private ImageExportFormat exportFormat = ImageExportFormat.TGA;

        private static ChannelPacker window;

        private static readonly int _baseMap = Shader.PropertyToID("_BaseMap");

        private static readonly int _normalMap = Shader.PropertyToID("_NormalMap");

        private static readonly int _metallic = Shader.PropertyToID("_Metallic");

        private static readonly int _mults = Shader.PropertyToID("mults");

        private static readonly int _inverts = Shader.PropertyToID("inverts");

        private static readonly int _froms = Shader.PropertyToID("froms");

        private static readonly int _a = Shader.PropertyToID("a");

        private static readonly int _b = Shader.PropertyToID("b");

        private static readonly int _g = Shader.PropertyToID("g");

        private static readonly int _r = Shader.PropertyToID("r");

        private static readonly int _result = Shader.PropertyToID("Result");

        private static readonly int _packedCol = Shader.PropertyToID("packedCol");

        private static readonly int _packed = Shader.PropertyToID("Packed");

        public ChannelPackerPreset preset;

        public ChannelPackerSettings settings;

        // Inputs
        private Texture2D[] inputs = new Texture2D[4];

        public float[] defaults = new float[4];

        public float[] mults = new float[4] { 1, 1, 1, 1 };

        public ColorChannel[] froms = new ColorChannel[4];

        public bool[] inverts = new bool[4];

        private Texture2D previewAlbedo, previewNormal;

        private RenderTexture[] blits = new RenderTexture[4];

        private Vector2 scrollPos;

        private GUIStyle regularStyle, regularSmall, smallWarn, regularWarn;

        private RenderTexture packedTexture;

        private Texture2D finalTexture;

        private Vector2Int textureDimensions;

        private Editor previewMatViewer;

        private Material previewMat;

        private bool previewShaderFound;

        private string previewMapProperty;

        private string previewMapShaderKeyword;

        // Show the window
        [MenuItem("Tools/Channel Packer")]
        public static void ShowWindow()
        {
            window = (ChannelPacker)GetWindow(typeof(ChannelPacker), false, "Channel Packer");
        }

        private void OnEnable()
        {
            LoadPackageAssets();
            LoadSettings();
            InitGUIStyles();
            textureDimensions = Vector2Int.zero;
        }

        // If for some reason the window becomes null, get it again.
        private void OnInspectorUpdate()
        {
            if (!window)
            {
                window = (ChannelPacker)GetWindow(typeof(ChannelPacker), false, "Channel Packer");
            }
        }

        private void OnGUI()
        {
            if (window)
            {
                window.Repaint();
                GUILayout.BeginArea(new Rect(0, 0, window.position.size.x, window.position.size.y));
                GUILayout.BeginVertical();
                scrollPos = GUILayout.BeginScrollView(scrollPos, false, true, GUILayout.ExpandHeight(true));
            }

            if (!inputs[0] && !inputs[1] && !inputs[2] && !inputs[3])
                textureDimensions = Vector2Int.zero;

            GUILayout.Label("Channel Packer", regularStyle);
            GUILayout.Label("Add textures to be packed together", regularStyle);

            // Inputs
            ChannelInput(0); // Red
            ChannelInput(1); // Green
            ChannelInput(2); // Blue
            ChannelInput(3); // Alpha

            // Input field for each color channel
            void ChannelInput(int channelInput)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                inputs[channelInput] = (Texture2D)EditorGUILayout.ObjectField($"{preset.names[channelInput]} Input",
                    inputs[channelInput], typeof(Texture2D), false);
                if (!inputs[channelInput])
                {
                    GUILayout.Label($"No {preset.names[channelInput]} Input, use slider to set value", regularSmall);
                    defaults[channelInput] = EditorGUILayout.Slider(defaults[channelInput], 0f, 1f);
                }
                else
                {
                    if (textureDimensions != Vector2Int.zero && (inputs[channelInput].width != textureDimensions.x ||
                                                                 inputs[channelInput].height != textureDimensions.y))
                    {
                        inputs[channelInput] = null;
                        Debug.LogWarning("Input texture is not the same resolution as other textures! Rejecting");
                    }

                    if (textureDimensions == Vector2Int.zero)
                    {
                        textureDimensions.x = inputs[channelInput].width;
                        textureDimensions.y = inputs[channelInput].height;
                    }

                    froms[channelInput] = (ColorChannel)EditorGUILayout.EnumPopup("From Channel", froms[channelInput]);
                    mults[channelInput] = EditorGUILayout.Slider($"Multiplier", mults[channelInput], 0f, 1f);
                    inverts[channelInput] = EditorGUILayout.Toggle("Invert", inverts[channelInput]);
                    if (inputs[channelInput] && inputs[channelInput].graphicsFormat.ToString().Contains("SRGB"))
                    {
                        GUILayout.Label("Texture marked as sRGB! Disabling recommended", smallWarn);
                    }
                }

                GUILayout.EndVertical();
            }

            GUILayout.Space(5f);

            // Main Options
            GUILayout.BeginVertical(EditorStyles.helpBox);
            if (!fastPack)
            {
                GUILayout.Label(
                    "Channel Packer compute shader was not found. Reimport the package or verify the package installation.",
                    regularWarn);
            }

            exportFormat = (ImageExportFormat)EditorGUILayout.EnumPopup("Export Format", exportFormat);

            EditorGUI.BeginDisabledGroup(!fastPack);
            if (GUILayout.Button("Pack Texture") && textureDimensions != Vector2Int.zero)
            {
                CreatePackedTexture();
                SaveTexture();
                EditorUtility.ClearProgressBar();
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Clear All"))
            {
                inputs[0] = inputs[1] = inputs[2] = inputs[3] = previewAlbedo = previewNormal = null;
                previewMatViewer = null;
            }

            if (GUILayout.Button("Save Preset"))
            {
                SavePreset();
            }

            EditorGUI.BeginChangeCheck();
            preset = (ChannelPackerPreset)EditorGUILayout.ObjectField(
                new GUIContent("Preset", "The preset packing settings to be used"), preset, typeof(ChannelPackerPreset),
                preset);
            if (EditorGUI.EndChangeCheck())
            {
                LoadSettings();
            }

            GUILayout.EndVertical();

            GUILayout.Space(5f);
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Preview
            if (previewShaderFound)
            {
                EditorGUI.BeginDisabledGroup(!fastPack);
                if (GUILayout.Button("Update Preview") && textureDimensions != Vector2Int.zero)
                {
                    EditorUtility.DisplayProgressBar("Packing texture", "", 0f);
                    CreatePackedTexture();
                    EditorUtility.ClearProgressBar();
                }

                EditorGUI.EndDisabledGroup();

                previewAlbedo = (Texture2D)EditorGUILayout.ObjectField("Preview Albedo (Optional)", previewAlbedo,
                    typeof(Texture2D), false);
                previewNormal = (Texture2D)EditorGUILayout.ObjectField("Preview Normal (Optional)", previewNormal,
                    typeof(Texture2D), false);

                if (previewMat && previewMatViewer)
                {
                    GUILayout.Label("Preview", regularStyle);
                    previewMatViewer.OnPreviewGUI(GUILayoutUtility.GetRect(256, 256), EditorStyles.objectField);
                    GUILayout.Space(10f);
                }
            }
            else if (string.IsNullOrEmpty(preset.previewShader))
            {
                GUILayout.Label(
                    $"Preset does not have a path for a preview shader. Assign a shader path and map keyword in the preset file",
                    regularWarn);
            }
            else
            {
                GUILayout.Label(
                    $"Preview Shader ({preset.previewShader}) not found.\nYou can still compile maps, but previewing is disabled.",
                    regularWarn);
            }

            GUILayout.EndVertical();

            if (window)
            {
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        private void CreatePackedTexture()
        {
            if (!fastPack)
            {
                Debug.LogError("Channel Packer compute shader was not found.");
                return;
            }

            finalTexture = new Texture2D(textureDimensions.x, textureDimensions.y, TextureFormat.ARGB32, false, true);
            int blitKernel = fastPack.FindKernel("ChannelSet");

            PackTexture(0); // Red
            PackTexture(1); // Green
            PackTexture(2); // Blue
            PackTexture(3); // Alpha

            // Prepare textures for packing
            void PackTexture(int channelInput)
            {
                EditorUtility.DisplayProgressBar($"Packing {preset.names[channelInput]}", "", 1f);
                blits[channelInput] = new RenderTexture(textureDimensions.x, textureDimensions.y, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                if (inputs[channelInput])
                    Graphics.Blit(inputs[channelInput], blits[channelInput]);
                else
                {
                    blits[channelInput].enableRandomWrite = true;
                    blits[channelInput].Create();

                    fastPack.SetTexture(blitKernel, _packed, blits[channelInput]);
                    fastPack.SetFloat(_packedCol, defaults[channelInput]);
                    fastPack.Dispatch(blitKernel, textureDimensions.x, textureDimensions.y, 1);
                }
            }

            EditorUtility.DisplayProgressBar("Combining Maps", "", 1f);
            // Create the render texture
            if (textureDimensions != Vector2Int.zero)
            {
                // Setup
                packedTexture = new RenderTexture(textureDimensions.x, textureDimensions.y, 32,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    enableRandomWrite = true
                };
                packedTexture.Create();

                // Send textures to compute shader
                int kernel = fastPack.FindKernel("CSMain");
                fastPack.SetTexture(kernel, _result, packedTexture);
                fastPack.SetTexture(kernel, _r, blits[0]);
                fastPack.SetTexture(kernel, _g, blits[1]);
                fastPack.SetTexture(kernel, _b, blits[2]);
                fastPack.SetTexture(kernel, _a, blits[3]);

                // Ternary hell, send data to compute shader for processing
                fastPack.SetInts(_froms, inputs[0] ? (int)froms[0] : 0, inputs[1] ? (int)froms[1] : 0,
                    inputs[2] ? (int)froms[2] : 0, inputs[3] ? (int)froms[3] : 0);
                fastPack.SetInts(_inverts, inputs[0] ? (inverts[0] ? 1 : 0) : 0, inputs[1] ? (inverts[1] ? 1 : 0) : 0,
                    inputs[2] ? (inverts[2] ? 1 : 0) : 0, inputs[3] ? (inverts[3] ? 1 : 0) : 0);
                fastPack.SetFloats(_mults, inputs[0] ? mults[0] : 1, inputs[1] ? mults[1] : 1,
                    inputs[2] ? mults[2] : 1, inputs[3] ? mults[3] : 1);
                fastPack.Dispatch(kernel, textureDimensions.x, textureDimensions.y, 1);

                // Final output
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = packedTexture;
                finalTexture.ReadPixels(new Rect(0, 0, packedTexture.width, packedTexture.height), 0, 0);
                finalTexture.Apply();
                RenderTexture.active = previous;
            }

            if (previewShaderFound)
            {
                GeneratePreview();
            }
        }

        private void SaveTexture()
        {
            // Find non-null channel input, bleh
            Texture2D validTex;
            if (inputs[0])
            {
                validTex = inputs[0];
            }
            else if (inputs[1])
            {
                validTex = inputs[1];
            }
            else if (inputs[2])
            {
                validTex = inputs[2];
            }
            else
            {
                validTex = inputs[3];
            }

            string texPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(validTex));
            if (string.IsNullOrEmpty(texPath) || texPath.StartsWith("Packages/"))
                texPath = "Assets";

            string extension = GetExportExtension(exportFormat);
            string path = EditorUtility.SaveFilePanelInProject("Save Texture To Directory", "PackedTexture", extension,
                "Saved", texPath);
            byte[] imageData = EncodeTexture(finalTexture, exportFormat);

            // Export to directory
            if (path.Length != 0 && imageData != null)
            {
                File.WriteAllBytes(path, imageData);
                Debug.Log($"Packed texture saved to: {path}");
                AssetDatabase.Refresh();

                // Disable sRGB
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }
            else
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void LoadSettings()
        {
            settings = LoadFirstAsset<ChannelPackerSettings>("t:ChannelPackerSettings", new[] { "Assets" });
            
            if (!settings)
            {
                settings = CreateProjectAsset<ChannelPackerSettings>(UserSettingsPath);
            }

            if (!preset)
            {
                if (!settings.lastPreset)
                {
                    preset = AssetDatabase.LoadAssetAtPath<ChannelPackerPreset>(PackageDefaultPresetPath);

                    if (!preset)
                    {
                        preset = LoadFirstAsset<ChannelPackerPreset>("ChannelPackerDefault t:ChannelPackerPreset");
                    }

                    if (!preset)
                    {
                        preset = CreateProjectAsset<ChannelPackerPreset>(UserDefaultPresetPath);
                    }
                }
                else
                {
                    preset = settings.lastPreset;
                }
            }

            if (settings.lastPreset != preset)
            {
                settings.lastPreset = preset;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            // Pull settings from preset
            Array.Copy(preset.defaults, defaults, 4);
            Array.Copy(preset.froms, froms, 4);
            Array.Copy(preset.inverts, inverts, 4);

            // Load preview shader
            previewMat = null;
            Shader preview = GetPreviewShader(out previewMapProperty, out previewMapShaderKeyword);
            if (preview)
            {
                previewMat = new Material(preview);
            }
            previewShaderFound = previewMat;
        }

        private void SavePreset()
        {
            // Create new preset SO
            string presetPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(preset));
            if (string.IsNullOrEmpty(presetPath) || presetPath.StartsWith("Packages/"))
                presetPath = UserSettingsFolder;
            EnsureProjectFolder(UserSettingsFolder);

            string path =
                EditorUtility.SaveFilePanelInProject("Save Preset To Directory", "Preset", "asset", "Saved",
                    presetPath);

            if (path.Length != 0)
            {
                ChannelPackerPreset created = Instantiate(preset); //< Copy current preset

                // Copy editable values from window
                Array.Copy(defaults, created.defaults, 4);
                Array.Copy(froms, created.froms, 4);
                Array.Copy(inverts, created.inverts, 4);

                AssetDatabase.CreateAsset(created, path);
                preset = AssetDatabase.LoadAssetAtPath<ChannelPackerPreset>(path);

                EditorUtility.SetDirty(preset);

                Debug.Log($"Preset saved to: {path}");
                AssetDatabase.Refresh();
            }
        }

        private void LoadPackageAssets()
        {
            if (fastPack == null)
            {
                fastPack = AssetDatabase.LoadAssetAtPath<ComputeShader>(PackageFastPackPath);
            }
            
            if (fastPack == null)
            {
                fastPack = LoadFirstAsset<ComputeShader>("ChannelPacker_FastPack t:ComputeShader");
            }
        }

        private static T LoadFirstAsset<T>(string filter, string[] searchInFolders = null) where T : UnityEngine.Object
        {
            string[] guids = searchInFolders == null
                ? AssetDatabase.FindAssets(filter)
                : AssetDatabase.FindAssets(filter, searchInFolders);
            if (guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T CreateProjectAsset<T>(string path) where T : ScriptableObject
        {
            EnsureProjectFolder(Path.GetDirectoryName(path)?.Replace("\\", "/"));

            T created = CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void EnsureProjectFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private void InitGUIStyles()
        {
            regularStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin ? new Color(0.76f, 0.76f, 0.76f, 1f) : Color.black
                }
            };

            regularSmall = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin ? new Color(0.76f, 0.76f, 0.76f, 1f) : Color.black
                }
            };

            smallWarn = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.90f, 0.65f, 0.10f, 1f)
                        : new Color(0.60f, 0.35f, 0.00f, 1f)
                }
            };

            regularWarn = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.90f, 0.65f, 0.10f, 1f)
                        : new Color(0.60f, 0.35f, 0.00f, 1f)
                }
            };
        }

        private void GeneratePreview()
        {
            if (previewAlbedo)
            {
                previewMat.SetTexture(_baseMap, previewAlbedo);
            }

            if (previewNormal)
            {
                previewMat.EnableKeyword("_NORMALMAP");
                previewMat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                previewMat.SetTexture(_normalMap, previewNormal);
            }

            previewMat.SetFloat(_metallic, 1f);
            if (!string.IsNullOrEmpty(previewMapShaderKeyword))
            {
                previewMat.EnableKeyword(previewMapShaderKeyword);
            }
            
            if (!string.IsNullOrEmpty(previewMapProperty))
            {
                previewMat.SetTexture(previewMapProperty, finalTexture);
            }

            previewMatViewer = Editor.CreateEditor(previewMat);
        }

        private Shader GetPreviewShader(out string mapProperty, out string shaderKeyword)
        {
            mapProperty = preset.previewMapKeyword;
            shaderKeyword = GetPreviewShaderKeyword(null, mapProperty);

            if (string.IsNullOrEmpty(preset.previewShader))
            {
                return GetPipelineDefaultPreviewShader(out mapProperty, out shaderKeyword);
            }

            Shader configuredShader = Shader.Find(preset.previewShader);
                
            if (configuredShader && string.IsNullOrEmpty(mapProperty))
            {
                GetDefaultPreviewMapSettings(configuredShader, out mapProperty, out shaderKeyword);
            }
            else
            {
                shaderKeyword = GetPreviewShaderKeyword(configuredShader, mapProperty);
            }
                
            return configuredShader;

        }

        private static Shader GetPipelineDefaultPreviewShader(out string mapProperty, out string shaderKeyword)
        {
            UnityEngine.Rendering.RenderPipelineAsset pipeline =
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (!pipeline)
            {
                return GetPreviewShader("Standard", "_MetallicGlossMap", "_METALLICGLOSSMAP", out mapProperty,
                    out shaderKeyword);
            }

            string pipelineName = pipeline.GetType().FullName;
            if (pipelineName?.Contains("Universal") ?? false)
            {
                return GetPreviewShader("Universal Render Pipeline/Lit", "_MetallicGlossMap", "_METALLICSPECGLOSSMAP",
                    out mapProperty, out shaderKeyword);
            }
            
            if (pipelineName?.Contains("HDRenderPipeline") ?? false)
            {
                return GetPreviewShader("HDRP/Lit", "_MaskMap", "_MASKMAP", out mapProperty, out shaderKeyword);
            }

            mapProperty = string.Empty;
            shaderKeyword = string.Empty;
            return null;
        }

        private static Shader GetPreviewShader(string shaderName, string defaultMapProperty,
            string defaultShaderKeyword, out string mapProperty, out string shaderKeyword)
        {
            mapProperty = defaultMapProperty;
            shaderKeyword = defaultShaderKeyword;
            return Shader.Find(shaderName);
        }

        private static void GetDefaultPreviewMapSettings(Shader shader, out string mapProperty,
            out string shaderKeyword)
        {
            string shaderName = shader ? shader.name : string.Empty;
            switch (shaderName)
            {
                case "Universal Render Pipeline/Lit":
                {
                    mapProperty = "_MetallicGlossMap";
                    shaderKeyword = "_METALLICSPECGLOSSMAP";
                    return;
                }
                case "HDRP/Lit":
                {
                    mapProperty = "_MaskMap";
                    shaderKeyword = "_MASKMAP";
                    return;
                }
                default:
                {
                    mapProperty = "_MetallicGlossMap";
                    shaderKeyword = "_METALLICGLOSSMAP";
                    break;
                }
            }
        }

        private static string GetPreviewShaderKeyword(Shader shader, string mapProperty)
        {
            if (string.IsNullOrEmpty(mapProperty))
            {
                return string.Empty;
            }
            
            if (shader && shader.name == "Universal Render Pipeline/Lit" && mapProperty == "_MetallicGlossMap")
            {
                return "_METALLICSPECGLOSSMAP";
            }

            return mapProperty.ToUpper();
        }

        private static byte[] EncodeTexture(Texture2D texture, ImageExportFormat format)
        {
            switch (format)
            {
                case ImageExportFormat.JPG:
                {
                    return texture.EncodeToJPG(100);
                }
                case ImageExportFormat.PNG:
                {
                    return texture.EncodeToPNG();
                }
                case ImageExportFormat.TGA:
                default:
                {
                    return texture.EncodeToTGA();
                }
            }
        }

        private static string GetExportExtension(ImageExportFormat format)
        {
            switch (format)
            {
                case ImageExportFormat.JPG:
                {
                    return "jpg";
                }
                case ImageExportFormat.PNG:
                {
                    return "png";
                }
                case ImageExportFormat.TGA:
                default:
                {
                    return "tga";
                }
            }
        }

        public enum ImageExportFormat
        {
            TGA,
            PNG,
            JPG
        }

        public enum ColorChannel
        {
            R,
            G,
            B,
            A
        }
    }
}
#endif