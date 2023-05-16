using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityTextureChannelPacker
{

    [ExecuteInEditMode] [CanEditMultipleObjects]
    [CustomEditor(typeof(OcbTextureChannelPacker))]
    public class OcbTextureChannelPackerEditor : Editor
    {

        float uiWidth = 100; // Update when the windows is repainted

        static readonly GUILayoutOption options = GUILayout.Height(EditorGUIUtility.singleLineHeight);

        static readonly string[] textureSizes = new string[] { "128", "256", "512", "1024", "2048", "4096", "8192" };

        // Basic function running in the editor
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUI.BeginChangeCheck();

            // Update the UI width only on repaint events
            if (Event.current.type.Equals(EventType.Repaint))
                uiWidth = GUILayoutUtility.GetLastRect().width;

            var script = (OcbTextureChannelPacker)target;
            string path = AssetDatabase.GetAssetPath(target);

            GUILayout.Space(12);

            script.TextureSize = EditorGUILayout.Popup("Texture Size", script.TextureSize, textureSizes);

            GUILayout.Space(12);

            int size = 2 << (script.TextureSize + 6);

            RenderChannelConfig(ref script.ChannelRed, "Red Channel", size);
            RenderChannelConfig(ref script.ChannelGreen, "Green Channel", size);
            RenderChannelConfig(ref script.ChannelBlue, "Blue Channel", size);
            RenderChannelConfig(ref script.ChannelAlpha, "Alpha Channel", size);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Invert Channel", GUILayout.Width(EditorGUIUtility.labelWidth - 30));
            EditorGUILayout.Space(22);
            GUILayout.Label("R");
            script.ChannelRed.invert = EditorGUILayout.Toggle(script.ChannelRed.invert);
            EditorGUILayout.Space(8);
            GUILayout.Label("G");
            script.ChannelGreen.invert = EditorGUILayout.Toggle(script.ChannelGreen.invert);
            EditorGUILayout.Space(8);
            GUILayout.Label("B");
            script.ChannelBlue.invert = EditorGUILayout.Toggle(script.ChannelBlue.invert);
            EditorGUILayout.Space(8);
            GUILayout.Label("A");
            script.ChannelAlpha.invert = EditorGUILayout.Toggle(script.ChannelAlpha.invert);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);

            var output = Path.ChangeExtension(path, ".png");
            var texture = AssetDatabase.LoadAssetAtPath(output, typeof(Texture2D));
            var lblBtn = texture ? "Update Packed Texture" : "Create Packed Texture";
            if (GUILayout.Button(lblBtn, GUILayout.Height(48)))
            {
                ExportPackedTexture(script, output);
                AssetDatabase.Refresh(); // Refresh first
            }

            if (texture is Texture2D tex2d)
            {
                GUILayout.Space(20);
                Rect rect = EditorGUILayout.GetControlRect(false, uiWidth);
                EditorGUI.DrawPreviewTexture(rect, tex2d);
            }

            GUILayout.Space(20);

            // Note: doesn't support "undo"
            if (EditorGUI.EndChangeCheck())
                script.SetDirty();

        }

        // Render the UI for the channel configs
        // Also the does the required base checks
        private void RenderChannelConfig(ref ChannelConfig cfg, string title, int size)
        {
            if (cfg.mode == MixMode.Fix)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(title, GUILayout.ExpandWidth(false));
                EditorGUILayout.Space(6);
                cfg.mode = (MixMode)EditorGUILayout.EnumPopup(cfg.mode);
                EditorGUILayout.Space(8);
                cfg.value = EditorGUILayout.IntSlider(cfg.value, 0, 255);
                EditorGUILayout.Space(6);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                cfg.src = EditorGUILayout.ObjectField(title, cfg.src,
                    typeof(Texture2D), false, options) as Texture2D;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(6);
                cfg.mode = (MixMode)EditorGUILayout.EnumPopup(cfg.mode,
                    GUILayout.Width(EditorGUIUtility.labelWidth - 36));
                EditorGUILayout.Space(20);
                GUILayout.Label("R");
                cfg.factor.r = EditorGUILayout.Toggle(cfg.factor.r > 0) ? 1f : 0;
                EditorGUILayout.Space(8);
                GUILayout.Label("G");
                cfg.factor.g = EditorGUILayout.Toggle(cfg.factor.g > 0) ? 1f : 0;
                EditorGUILayout.Space(8);
                GUILayout.Label("B");
                cfg.factor.b = EditorGUILayout.Toggle(cfg.factor.b > 0) ? 1f : 0;
                EditorGUILayout.Space(8);
                GUILayout.Label("A");
                cfg.factor.a = EditorGUILayout.Toggle(cfg.factor.a > 0) ? 1f : 0;
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(2);
            CheckReadableTexture(cfg.src);
            CheckUncompressedTexture(cfg.src);
            CheckTextureSize(cfg.src, size);
            GUILayout.Space(18);
        }

        // Create and store the generated texture (same base name as this asset)
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance",
            "UNT0017:SetPixels invocation is slow", Justification = "Editor Only")]
        private void ExportPackedTexture(OcbTextureChannelPacker script, string path)
        {
            int size = 2 << (script.TextureSize + 6);
            Texture2D packed = new Texture2D(size, size,
                TextureFormat.RGBA32, true);
            Color[] to = new Color[size * size];
            ApplyPixelChanges(script.ChannelRed, ref to, size, new Color(1, 0, 0, 0));
            ApplyPixelChanges(script.ChannelGreen, ref to, size, new Color(0, 1, 0, 0));
            ApplyPixelChanges(script.ChannelBlue, ref to, size, new Color(0, 0, 1, 0));
            ApplyPixelChanges(script.ChannelAlpha, ref to, size, new Color(0, 0, 0, 1));
            packed.SetPixels(to);
            packed.Apply(true, false);
            var bytes = packed.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }

        // Generate all channels from given textures and their configs
        private void ApplyPixelChanges(ChannelConfig cfg,
            ref Color[] dst, int size, Color factor)
        {
            // Mode for fixed value or if texture is not given
            if (cfg.mode == MixMode.Fix || cfg.src == null)
            {
                int value = cfg.mode == MixMode.Fix ? cfg.value : 0;
                float saturation = (float)value / byte.MaxValue;
                saturation = Mathf.Max(0, Mathf.Min(1, saturation));
                if (cfg.invert) saturation = 1 - saturation;
                for (int i = 0; i < dst.Length; i += 1)
                {
                    dst[i].r = Mathf.Max(dst[i].r, factor.r * saturation);
                    dst[i].g = Mathf.Max(dst[i].g, factor.g * saturation);
                    dst[i].b = Mathf.Max(dst[i].b, factor.b * saturation);
                    dst[i].a = Mathf.Max(dst[i].a, factor.a * saturation);
                }
            }
            else
            {
                var from = cfg.src.GetPixels(MipMapOffset(cfg.src.width, size));
                if (from.Length != dst.Length) throw new Exception("Size mismatch");
                if (cfg.src.width != cfg.src.height) throw new Exception("Ratio mismatch");
                if ((cfg.src.width & -cfg.src.width) != cfg.src.width) throw
                        new Exception("Texture size is not a power of two");
                for (int i = 0; i < dst.Length; i += 1)
                {
                    float saturation; Color sample = new Color(
                        from[i].r * cfg.factor.r, from[i].g * cfg.factor.g,
                        from[i].b * cfg.factor.b, from[i].a * cfg.factor.a);
                    if (cfg.mode == MixMode.Min) saturation = Mathf.Min(sample.r, Mathf.Min(sample.g, Mathf.Min(sample.b, sample.a)));
                    else if (cfg.mode == MixMode.Max) saturation = Mathf.Max(sample.r, Mathf.Max(sample.g, Mathf.Max(sample.b, sample.a)));
                    else saturation = (sample.r + sample.g + sample.b + sample.a) / (cfg.factor.r + cfg.factor.g + cfg.factor.b + cfg.factor.a);
                    saturation = Mathf.Max(0, Mathf.Min(1, saturation));
                    if (cfg.invert) saturation = 1 - saturation;
                    dst[i].r = Mathf.Max(dst[i].r, factor.r * saturation);
                    dst[i].g = Mathf.Max(dst[i].g, factor.g * saturation);
                    dst[i].b = Mathf.Max(dst[i].b, factor.b * saturation);
                    dst[i].a = Mathf.Max(dst[i].a, factor.a * saturation);
                }
            }
        }

        private int MipMapOffset(int src, int size)
            => (int)Math.Log(src / size, 2);

        // Make sure we can read the texture on the GPU
        private static void CheckReadableTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (texture.isReadable) return;
            if (GUILayout.Button("Fix: Mark readable", GUILayout.Height(24)))
            {
                string path = AssetDatabase.GetAssetPath(texture);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    if (importer.isReadable) return;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
        }

        // Make sure we do not compress source textures
        // We only want to compress the final result
        // Otherwise we may do two lossy compressions
        private static void CheckUncompressedTexture(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    if (GUILayout.Button("Fix: Mark uncompressed", GUILayout.Height(24)))
                    {
                        importer.textureCompression = 
                            TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        // From https://answers.unity.com/questions/185871/
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance",
            "UNT0017:SetPixels invocation is slow", Justification = "Editor Only")]
        private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            Texture2D result = new Texture2D(targetWidth, targetHeight, source.format, true);
            Color[] rpixels = result.GetPixels(0);
            float incX = (float)source.width / targetWidth / source.width;
            float incY = (float)source.height / targetHeight / source.height;
            for (int px = 0; px < rpixels.Length; px++)
                rpixels[px] = source.GetPixelBilinear(incX * ((float)px % targetWidth),
                                  incY * ((float)Mathf.Floor(px / targetWidth)));
            result.SetPixels(rpixels, 0);
            result.Apply();
            return result;
        }

        // Check that texture size is big enough
        // We take from mipmap if texture is bigger
        // Otherwise we must upscale source texture
        // Note: better to do this in e.g. photoshop?
        private void CheckTextureSize(Texture2D texture, int size)
        {
            if (texture == null) return;
            if (texture.width >= size && texture.height >= size) return;
            if (GUILayout.Button($"Fix: Upscale to {size}", GUILayout.Height(24)))
            {
                var path = AssetDatabase.GetAssetPath(texture);
                texture = ScaleTexture(texture, size, size);
                var bytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();
            }
        }

    }

}