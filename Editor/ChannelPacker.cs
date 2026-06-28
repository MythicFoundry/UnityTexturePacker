#nullable enable

#if UNITY_EDITOR
/*
Channel Packer
Made by Camobiwon

Original code from: https://www.reddit.com/r/Unity3D/comments/glkvp2/i_made_another_mask_map_packer_for_hdrp/
Thank you original creator! This has been extremely useful to me, and whoever is using this, I hope Channel Packer is useful to you :)
*/

using System;
using System.IO;
using System.Linq;
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
        private ComputeShader? fastPack;

        [SerializeField]
        private ImageExportFormat exportFormat = ImageExportFormat.TGA;

        private static ChannelPacker? _window;

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

        public ChannelPackerPreset? preset;

        public ChannelPackerSettings? settings;

        // Inputs
        private readonly Texture2D?[] _inputs = new Texture2D[4];

        public float[] defaults = new float[4];

        public float[] mults = { 1, 1, 1, 1 };

        public ColorChannel[] froms = new ColorChannel[4];

        public bool[] inverts = new bool[4];

        private Texture2D? _previewAlbedo;

        private Texture2D? _previewNormal;

        private readonly RenderTexture[] _blits = new RenderTexture[4];

        private Vector2 _scrollPos;

        private GUIStyle? _regularStyle;

        private GUIStyle? _regularSmall;

        private GUIStyle? _smallWarn;

        private GUIStyle? _regularWarn;

        private RenderTexture? _packedTexture;

        private Texture2D? _finalTexture;

        private Vector2Int _textureDimensions = Vector2Int.zero;

        private Editor? _previewMatViewer;

        private Material? _previewMat;

        private bool _previewShaderFound;

        private string _previewMapProperty = string.Empty;

        private string _previewMapShaderKeyword = string.Empty;

        // Show the window
        [MenuItem("Tools/Channel Packer")]
        public static void ShowWindow()
        {
            _window = (ChannelPacker)GetWindow(typeof(ChannelPacker), false, "Channel Packer");
        }

        private void OnEnable()
        {
            LoadPackageAssets();
            LoadSettings();
            InitGUIStyles();
            _textureDimensions = Vector2Int.zero;
        }

        // If for some reason the window becomes null, get it again.
        private void OnInspectorUpdate()
        {
            if (!_window)
            {
                _window = (ChannelPacker)GetWindow(typeof(ChannelPacker), false, "Channel Packer");
            }
        }

        private void OnGUI()
        {
            if (_window)
            {
                _window.Repaint();
                GUILayout.BeginArea(new Rect(0, 0, _window.position.size.x, _window.position.size.y));
                GUILayout.BeginVertical();
                _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true, GUILayout.ExpandHeight(true));
            }

            if (!_inputs[0] && !_inputs[1] && !_inputs[2] && !_inputs[3])
            {
                _textureDimensions = Vector2Int.zero;
            }

            GUILayout.Label("Channel Packer", _regularStyle);
            GUILayout.Label("Add textures to be packed together", _regularStyle);

            // Inputs
            _ChannelInput(0); // Red
            _ChannelInput(1); // Green
            _ChannelInput(2); // Blue
            _ChannelInput(3); // Alpha

            GUILayout.Space(5f);

            // Main Options
            GUILayout.BeginVertical(EditorStyles.helpBox);
            if (!fastPack)
            {
                GUILayout.Label(
                    "Channel Packer compute shader was not found. Reimport the package or verify the package installation.",
                    _regularWarn);
            }

            exportFormat = (ImageExportFormat)EditorGUILayout.EnumPopup("Export Format", exportFormat);

            EditorGUI.BeginDisabledGroup(!fastPack);
            if (GUILayout.Button("Pack Texture") && _textureDimensions != Vector2Int.zero)
            {
                CreatePackedTexture();
                SaveTexture();
                EditorUtility.ClearProgressBar();
            }

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Clear All"))
            {
                _inputs[0] = null;
                _inputs[1] = null;
                _inputs[2] = null;
                _inputs[3] = null;
                _previewAlbedo = null;
                _previewNormal = null;
                _previewMatViewer = null;
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
            if (_previewShaderFound)
            {
                EditorGUI.BeginDisabledGroup(!fastPack);
                if (GUILayout.Button("Update Preview") && _textureDimensions != Vector2Int.zero)
                {
                    EditorUtility.DisplayProgressBar("Packing texture", string.Empty, 0f);
                    CreatePackedTexture();
                    EditorUtility.ClearProgressBar();
                }

                EditorGUI.EndDisabledGroup();

                _previewAlbedo = (Texture2D)EditorGUILayout.ObjectField("Preview Albedo (Optional)", _previewAlbedo,
                    typeof(Texture2D), false);
                _previewNormal = (Texture2D)EditorGUILayout.ObjectField("Preview Normal (Optional)", _previewNormal,
                    typeof(Texture2D), false);

                if (_previewMat && _previewMatViewer)
                {
                    GUILayout.Label("Preview", _regularStyle);
                    _previewMatViewer.OnPreviewGUI(GUILayoutUtility.GetRect(256, 256), EditorStyles.objectField);
                    GUILayout.Space(10f);
                }
            }
            else if (string.IsNullOrEmpty(preset.previewShader))
            {
                GUILayout.Label(
                    "Preset does not have a path for a preview shader. Assign a shader path and map keyword in the preset file",
                    _regularWarn);
            }
            else
            {
                GUILayout.Label(
                    $"Preview Shader ({preset.previewShader}) not found.\nYou can still compile maps, but previewing is disabled.",
                    _regularWarn);
            }

            GUILayout.EndVertical();

            if (_window)
            {
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            return;

            // Input field for each color channel
            void _ChannelInput(int channelInput)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                _inputs[channelInput] = (Texture2D)EditorGUILayout.ObjectField($"{preset?.names[channelInput]} Input",
                    _inputs[channelInput], typeof(Texture2D), false);

                Texture2D? textureInput = _inputs[channelInput];
                if (!textureInput)
                {
                    GUILayout.Label($"No {preset?.names[channelInput]} Input, use slider to set value", _regularSmall);
                    defaults[channelInput] = EditorGUILayout.Slider(defaults[channelInput], 0f, 1f);
                }
                else
                {
                    if (_textureDimensions != Vector2Int.zero && (textureInput.width != _textureDimensions.x ||
                                                                  textureInput.height != _textureDimensions.y))
                    {
                        _inputs[channelInput] = null;
                        Debug.LogWarning("Input texture is not the same resolution as other textures! Rejecting");
                    }

                    if (_textureDimensions == Vector2Int.zero)
                    {
                        _textureDimensions = textureInput
                            ? new Vector2Int(textureInput.width, textureInput.height)
                            : Vector2Int.zero;
                    }

                    froms[channelInput] = (ColorChannel)EditorGUILayout.EnumPopup("From Channel", froms[channelInput]);
                    mults[channelInput] = EditorGUILayout.Slider($"Multiplier", mults[channelInput], 0f, 1f);
                    inverts[channelInput] = EditorGUILayout.Toggle("Invert", inverts[channelInput]);
                    if (textureInput && textureInput.graphicsFormat.ToString().Contains("SRGB"))
                    {
                        GUILayout.Label("Texture marked as sRGB! Disabling recommended", _smallWarn);
                    }
                }

                GUILayout.EndVertical();
            }
        }

        private void CreatePackedTexture()
        {
            if (!fastPack)
            {
                Debug.LogError("Channel Packer compute shader was not found.");
                return;
            }

            _finalTexture = new Texture2D(_textureDimensions.x, _textureDimensions.y, TextureFormat.ARGB32, false,
                true);
            int blitKernel = fastPack.FindKernel("ChannelSet");

            _PackTexture(0); // Red
            _PackTexture(1); // Green
            _PackTexture(2); // Blue
            _PackTexture(3); // Alpha

            EditorUtility.DisplayProgressBar("Combining Maps", string.Empty, 1f);
            // Create the render texture
            if (_textureDimensions != Vector2Int.zero)
            {
                // Setup
                _packedTexture = new RenderTexture(_textureDimensions.x, _textureDimensions.y, 32,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    enableRandomWrite = true
                };
                _packedTexture.Create();

                // Send textures to compute shader
                int kernel = fastPack.FindKernel("CSMain");
                fastPack.SetTexture(kernel, _result, _packedTexture);
                fastPack.SetTexture(kernel, _r, _blits[0]);
                fastPack.SetTexture(kernel, _g, _blits[1]);
                fastPack.SetTexture(kernel, _b, _blits[2]);
                fastPack.SetTexture(kernel, _a, _blits[3]);

                // Ternary hell, send data to compute shader for processing
                fastPack.SetInts(_froms,
                    _inputs[0] ? (int)froms[0] : 0,
                    _inputs[1] ? (int)froms[1] : 0,
                    _inputs[2] ? (int)froms[2] : 0,
                    _inputs[3] ? (int)froms[3] : 0);
                fastPack.SetInts(_inverts,
                    _inputs[0] ? (inverts[0] ? 1 : 0) : 0,
                    _inputs[1] ? (inverts[1] ? 1 : 0) : 0,
                    _inputs[2] ? (inverts[2] ? 1 : 0) : 0,
                    _inputs[3] ? (inverts[3] ? 1 : 0) : 0);
                fastPack.SetFloats(_mults,
                    _inputs[0] ? mults[0] : 1,
                    _inputs[1] ? mults[1] : 1,
                    _inputs[2] ? mults[2] : 1,
                    _inputs[3] ? mults[3] : 1);
                fastPack.Dispatch(kernel, _textureDimensions.x, _textureDimensions.y, 1);

                // Final output
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = _packedTexture;
                _finalTexture.ReadPixels(new Rect(0, 0, _packedTexture.width, _packedTexture.height), 0, 0);
                _finalTexture.Apply();
                RenderTexture.active = previous;
            }

            if (_previewShaderFound)
            {
                GeneratePreview();
            }

            return;

            // Prepare textures for packing
            void _PackTexture(int channelInput)
            {
                EditorUtility.DisplayProgressBar($"Packing {preset?.names[channelInput]}", string.Empty, 1f);
                _blits[channelInput] = new RenderTexture(_textureDimensions.x, _textureDimensions.y, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                if (_inputs[channelInput])
                {
                    Graphics.Blit(_inputs[channelInput], _blits[channelInput]);
                }
                else
                {
                    _blits[channelInput].enableRandomWrite = true;
                    _blits[channelInput].Create();

                    fastPack.SetTexture(blitKernel, _packed, _blits[channelInput]);
                    fastPack.SetFloat(_packedCol, defaults[channelInput]);
                    fastPack.Dispatch(blitKernel, _textureDimensions.x, _textureDimensions.y, 1);
                }
            }
        }

        private Texture2D? GetFirstValidTexture()
        {
            return _inputs.First(input => input);
        }

        private void SaveTexture()
        {
            // Find non-null channel input
            Texture2D? validTex = GetFirstValidTexture();

            string? texPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(validTex));
            if (string.IsNullOrEmpty(texPath) || texPath.StartsWith("Packages/"))
            {
                texPath = "Assets";
            }

            string extension = GetExportExtension(exportFormat);
            string path = EditorUtility.SaveFilePanelInProject("Save Texture To Directory", "PackedTexture", extension,
                "Saved", texPath);
            byte[]? imageData = EncodeTexture(_finalTexture, exportFormat);

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
            _previewMat = null;
            ShaderProperties previewProperties = GetPreviewShader();
            if (previewProperties.Shader)
            {
                _previewMat = new Material(previewProperties.Shader);
            }

            _previewMapProperty = previewProperties.MapProperty;
            _previewMapShaderKeyword = previewProperties.ShaderKeyword;
            _previewShaderFound = _previewMat;
        }

        private void SavePreset()
        {
            // Create new preset SO
            string? presetPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(preset));
            if (string.IsNullOrEmpty(presetPath) || presetPath.StartsWith("Packages/"))
                presetPath = UserSettingsFolder;
            EnsureProjectFolder(UserSettingsFolder);

            string path =
                EditorUtility.SaveFilePanelInProject("Save Preset To Directory", "Preset", "asset", "Saved",
                    presetPath);

            if (path.Length == 0)
                return;

            ChannelPackerPreset? created = Instantiate(preset); //< Copy current preset
            if (!created)
            {
                Debug.LogError($"Failed to instantiate preset {preset?.name}");
                return;
            }

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

        private static T? LoadFirstAsset<T>(string filter, string[]? searchInFolders = null)
            where T : UnityEngine.Object
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

        private static void EnsureProjectFolder(string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath!.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private void InitGUIStyles()
        {
            _regularStyle = new GUIStyle
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

            _regularSmall = new GUIStyle
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

            _smallWarn = new GUIStyle
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

            _regularWarn = new GUIStyle
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
            if (!_previewMat)
            {
                Debug.LogWarning("Preview material not set");
                return;
            }

            if (_previewAlbedo)
            {
                _previewMat.SetTexture(_baseMap, _previewAlbedo);
            }

            if (_previewNormal)
            {
                _previewMat.EnableKeyword("_NORMALMAP");
                _previewMat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                _previewMat.SetTexture(_normalMap, _previewNormal);
            }

            _previewMat.SetFloat(_metallic, 1f);
            if (!string.IsNullOrEmpty(_previewMapShaderKeyword))
            {
                _previewMat.EnableKeyword(_previewMapShaderKeyword);
            }

            if (!string.IsNullOrEmpty(_previewMapProperty))
            {
                _previewMat.SetTexture(_previewMapProperty, _finalTexture);
            }

            _previewMatViewer = Editor.CreateEditor(_previewMat);
        }

        private ShaderProperties GetPreviewShader()
        {
            if (!preset)
            {
                Debug.LogWarning("Preset not set");
                return new ShaderProperties();
            }

            Shader? configuredShader = null;
            string? mapProperty = preset.previewMapKeyword;
            string shaderKeyword = configuredShader.GetPreviewShaderKeyword(mapProperty);

            if (string.IsNullOrEmpty(preset.previewShader))
            {
                return ShaderProperties.GetPipelineDefaultPreviewShader();
            }

            configuredShader = Shader.Find(preset.previewShader);

            if (configuredShader && string.IsNullOrEmpty(mapProperty))
            {
                return ShaderProperties.FromShader(configuredShader);
            }

            shaderKeyword = configuredShader.GetPreviewShaderKeyword(mapProperty);
            return new ShaderProperties(configuredShader, mapProperty, shaderKeyword);
        }

        private static byte[]? EncodeTexture(Texture2D? texture, ImageExportFormat format)
        {
            switch (format)
            {
                case ImageExportFormat.JPG:
                {
                    return texture?.EncodeToJPG(100);
                }
                case ImageExportFormat.PNG:
                {
                    return texture?.EncodeToPNG();
                }
                case ImageExportFormat.TGA:
                default:
                {
                    return texture?.EncodeToTGA();
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

        private enum ImageExportFormat
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