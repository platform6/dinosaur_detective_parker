using UnityEditor;
using UnityEngine;

namespace AnimalsVsMonsters.EditorTools
{
    /// <summary>
    /// Any texture dropped into Assets/_Project/Art/Characters/ is imported as a character sprite:
    /// single sprite, pivot at the feet (bottom-center), smooth filtering for hand-drawn art.
    /// Applied on first import only, so manual tweaks in the Inspector are kept afterwards.
    /// </summary>
    public class CharacterSpriteImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Art/Characters/";
        private const float PixelsPerUnit = 150f; // ~255px tall art => ~1.7 units tall

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var imp = (TextureImporter)assetImporter;
            // Some images get auto-detected as another shape (e.g. Cube), which yields no Sprite at all.
            if (imp.textureShape != TextureImporterShape.Texture2D) imp.textureShape = TextureImporterShape.Texture2D;
            if (!imp.importSettingsMissing && imp.textureType == TextureImporterType.Sprite) return;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PixelsPerUnit;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;

            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = new Vector2(0.5f, 0f);
            s.spriteMeshType = SpriteMeshType.Tight;
            imp.SetTextureSettings(s);
        }
    }
}
