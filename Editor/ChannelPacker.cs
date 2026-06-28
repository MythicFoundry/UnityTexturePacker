#nullable enable

#if UNITY_EDITOR
/*
Channel Packer
Made by Camobiwon

Original code from: https://www.reddit.com/r/Unity3D/comments/glkvp2/i_made_another_mask_map_packer_for_hdrp/
Thank you original creator! This has been extremely useful to me, and whoever is using this, I hope Channel Packer is useful to you :)
*/

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

        private const string LegacyUserSettingsPath = UserSettingsFolder + "/ChannelPackerSettings.asset";

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
        private readonly TextureChannel[] _channels =
        {
            new(),
            new(),
            new(),
            new()
        };

        private Texture2D? _previewAlbedo;

        private Texture2D? _previewNormal;

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

        private void ClearInputs()
        {
            _channels[0].input = null;
            _channels[1].input = null;
            _channels[2].input = null;
            _channels[3].input = null;
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

            if (!_channels[0].input && !_channels[1].input && !_channels[2].input && !_channels[3].input)
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
                ClearInputs();
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
                TextureChannel channel = _channels[channelInput];
                
                GUILayout.BeginVertical(EditorStyles.helpBox);
                channel.input = (Texture2D)EditorGUILayout.ObjectField($"{preset?[channelInput].name} Input",
                    channel.input, typeof(Texture2D), false);

                Texture2D? textureInput = channel.input;
                if (!textureInput)
                {
                    GUILayout.Label($"No {preset?[channelInput].name} Input, use slider to set value", _regularSmall);
                    channel.@default = EditorGUILayout.Slider(channel.@default, 0f, 1f);
                    GUILayout.EndVertical();
                    return;
                }

                if (_textureDimensions != Vector2Int.zero && (textureInput.width != _textureDimensions.x ||
                                                              textureInput.height != _textureDimensions.y))
                {
                    channel.input = null;
                    Debug.LogWarning("Input texture is not the same resolution as other textures! Rejecting");
                }

                if (_textureDimensions == Vector2Int.zero)
                {
                    _textureDimensions = textureInput
                        ? new Vector2Int(textureInput.width, textureInput.height)
                        : Vector2Int.zero;
                }

                channel.from = (ColorChannel)EditorGUILayout.EnumPopup("From Channel", channel.from);
                channel.mult = EditorGUILayout.Slider("Multiplier", channel.mult, 0f, 1f);
                channel.invert = EditorGUILayout.Toggle("Invert", channel.invert);
                if (textureInput && textureInput.graphicsFormat.ToString().Contains("SRGB"))
                {
                    GUILayout.Label("Texture marked as sRGB! Disabling recommended", _smallWarn);
                }

                GUILayout.EndVertical();
            }
        }

        private void SetChannelValues()
        {
            if (!fastPack)
            {
                Debug.LogError("Channel Packer compute shader was not found.");
                return;
            }

            TextureChannel rChannel = _channels[0];
            TextureChannel gChannel = _channels[1];
            TextureChannel bChannel = _channels[2];
            TextureChannel aChannel = _channels[3];
            
            // Send textures to compute shader
            int kernel = fastPack.FindKernel("CSMain");
            fastPack.SetTexture(kernel, _result, _packedTexture);
            fastPack.SetTexture(kernel, _r, rChannel.blit);
            fastPack.SetTexture(kernel, _g, gChannel.blit);
            fastPack.SetTexture(kernel, _b, bChannel.blit);
            fastPack.SetTexture(kernel, _a, aChannel.blit);

            // Ternary hell, send data to compute shader for processing
            fastPack.SetInts(_froms,
                rChannel.input ? (int)rChannel.from : 0,
                gChannel.input ? (int)gChannel.from : 0,
                bChannel.input ? (int)bChannel.from : 0,
                aChannel.input ? (int)aChannel.from : 0);
            fastPack.SetInts(_inverts,
                rChannel.input ? (rChannel.invert ? 1 : 0) : 0,
                gChannel.input ? (gChannel.invert ? 1 : 0) : 0,
                bChannel.input ? (bChannel.invert ? 1 : 0) : 0,
                aChannel.input ? (aChannel.invert ? 1 : 0) : 0);
            fastPack.SetFloats(_mults,
                rChannel.input ? rChannel.mult : 1,
                gChannel.input ? gChannel.mult : 1,
                bChannel.input ? bChannel.mult : 1,
                aChannel.input ? aChannel.mult : 1);
            fastPack.Dispatch(kernel, _textureDimensions.x, _textureDimensions.y, 1);
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

                SetChannelValues();

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
                TextureChannel channel = _channels[channelInput];
                
                EditorUtility.DisplayProgressBar($"Packing {preset?[channelInput].name}", string.Empty, 1f);
                
                channel.blit = new RenderTexture(_textureDimensions.x, _textureDimensions.y, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                
                if (channel.input)
                {
                    Graphics.Blit(channel.input, channel.blit);
                    return;
                }

                channel.blit.enableRandomWrite = true;
                channel.blit.Create();

                fastPack.SetTexture(blitKernel, _packed, channel.blit);
                fastPack.SetFloat(_packedCol, channel.@default);
                fastPack.Dispatch(blitKernel, _textureDimensions.x, _textureDimensions.y, 1);
            }
        }

        private Texture2D? GetFirstValidTexture()
        {
            return _channels.FirstOrDefault(channel => channel.input)?.input;
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
            ChannelPackerSettings currentSettings = ChannelPackerSettings.instance;
            settings = currentSettings;
            MigrateLegacySettings(currentSettings);

            if (!preset)
            {
                if (!currentSettings.lastPreset)
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
                    preset = currentSettings.lastPreset;
                }
            }

            if (currentSettings.lastPreset != preset)
            {
                currentSettings.lastPreset = preset;
                currentSettings.SaveSettings();
            }

            // Pull settings from preset
            for (int index = 0; index < _channels.Length; index++)
            {
                TextureChannel textureChannel = _channels[index];
                ChannelPackerChannel channelPackerChannel = preset.channels[index];
                
                textureChannel.@default = channelPackerChannel.@default;
                textureChannel.from = channelPackerChannel.from;
                textureChannel.invert = channelPackerChannel.invert;
            }

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

        private static void MigrateLegacySettings(ChannelPackerSettings settings)
        {
            if (settings.lastPreset)
                return;

            ChannelPackerSettings? legacySettings =
                AssetDatabase.LoadAssetAtPath<ChannelPackerSettings>(LegacyUserSettingsPath);
            if (!legacySettings || !legacySettings.lastPreset)
                return;

            settings.lastPreset = legacySettings.lastPreset;
            settings.SaveSettings();
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
            for (int index = 0; index < _channels.Length; index++)
            {
                TextureChannel textureChannel = _channels[index];
                ChannelPackerChannel channelPackerChannel = created.channels[index];

                channelPackerChannel.@default = textureChannel.@default;
                channelPackerChannel.from = textureChannel.from;
                channelPackerChannel.invert = textureChannel.invert;
            }

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
