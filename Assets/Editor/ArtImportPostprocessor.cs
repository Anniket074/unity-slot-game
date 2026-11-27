using UnityEditor;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Applies the import settings the art pack needs, automatically, on first import.
    /// Pixel art must not be filtered or compressed, and the button strips are sliced
    /// into the state frames the UI expects.
    /// </summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        private const string SymbolsFolder = "/Art/Symbols/";
        private const string MachineFolder = "/Art/Machine/";
        private const string ButtonsFolder = "/Art/Buttons/";
        private const string UiFolder = "/UI/";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool isArt = path.Contains("/Art/") || path.Contains("/UI/");
            if (!isArt || !path.StartsWith("Assets/"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;

            if (path.Contains(SymbolsFolder) || path.Contains(MachineFolder))
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteImportMode = SpriteImportMode.Single;
                return;
            }

            if (path.Contains(ButtonsFolder))
            {
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteImportMode = SpriteImportMode.Multiple;

                // The modern replacement (ISpriteEditorDataProvider) lives in the 2D Sprite
                // Editor package, which this bare project does not depend on. The legacy
                // property still writes the same slicing data, so it is used deliberately.
#pragma warning disable 0618
                importer.spritesheet = BuildButtonFrames(path);
#pragma warning restore 0618
                return;
            }

            if (path.Contains(UiFolder))
            {
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.spriteImportMode = SpriteImportMode.Single;
            }
        }

        /// <summary>
        /// The pack ships two strip layouts:
        ///  - slot_machine_buttons-0N.png : 256x1024, four stacked 256x256 states
        ///  - Yes_No_Btn.png              : 990x690, two columns (YES / NO) of three states
        /// </summary>
        private static SpriteMetaData[] BuildButtonFrames(string path)
        {
            var frames = new System.Collections.Generic.List<SpriteMetaData>();

            if (path.EndsWith("Yes_No_Btn.png"))
            {
                const int cellWidth = 495;
                const int cellHeight = 230;
                const int textureHeight = 690;
                string[] columns = { "yes", "no" };
                string[] states = { "normal", "hover", "pressed" };

                for (int column = 0; column < columns.Length; column++)
                {
                    for (int state = 0; state < states.Length; state++)
                    {
                        frames.Add(new SpriteMetaData
                        {
                            name = $"{columns[column]}_{states[state]}",
                            rect = new Rect(
                                column * cellWidth,
                                textureHeight - (state + 1) * cellHeight,
                                cellWidth,
                                cellHeight),
                            alignment = (int)SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f)
                        });
                    }
                }

                return frames.ToArray();
            }

            const int size = 256;
            const int stripHeight = 1024;
            for (int i = 0; i < 4; i++)
            {
                frames.Add(new SpriteMetaData
                {
                    name = $"frame_{i}",
                    rect = new Rect(0, stripHeight - (i + 1) * size, size, size),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                });
            }

            return frames.ToArray();
        }
    }
}
