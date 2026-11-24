using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalsVsMonsters.EditorTools
{
    /// <summary>
    /// Dresses the Environment: grass on the ground, concrete on walls/platforms/pillar, and the
    /// cartoon cloud sky on the backdrop. Gives each textured box a cube mesh whose UVs are scaled
    /// to the box's size, so textures tile at the same density everywhere instead of stretching.
    /// </summary>
    public static class EnvironmentDresser
    {
        private const string EnvArt = "Assets/_Project/Art/Environment";
        private const string MatDir = "Assets/_Project/Materials";
        private const string MeshDir = "Assets/_Project/Meshes";
        private const string ScenePath = "Assets/Scenes/Main25D.unity";

        private const float GrassTile = 2.5f;     // metres per grass texture repeat
        private const float ConcreteTile = 2.0f;  // metres per concrete texture repeat
        public static readonly Color SkyTop = new Color(88 / 255f, 150 / 255f, 222 / 255f);

        [MenuItem("Tools/Animals v. Monsters/Apply Environment Textures")]
        public static void ApplyToMainScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Environment") Apply(root.transform);
                var cam = root.GetComponent<Camera>();
                if (cam != null) cam.backgroundColor = SkyTop;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Animals v. Monsters] Applied grass, concrete and cloud sky textures.");
        }

        /// <summary>Textures the materials and fixes up meshes for every box under the Environment root.</summary>
        public static void Apply(Transform env)
        {
            ConfigureImporters();
            var grassMat = SetupLit("Ground", EnvArt + "/Grass004_Color.jpg", EnvArt + "/Grass004_Normal.jpg", 0.05f);
            var concreteMat = SetupLit("Platform", EnvArt + "/Concrete034_Color.jpg", EnvArt + "/Concrete034_Normal.jpg", 0.15f);
            var skyMat = SetupSky();

            foreach (Transform t in env)
            {
                var mr = t.GetComponent<MeshRenderer>();
                var mf = t.GetComponent<MeshFilter>();
                if (mr == null || mf == null) continue;

                if (mr.sharedMaterial == grassMat)
                    mf.sharedMesh = ScaledCube(t.localScale, new Vector2(GrassTile, GrassTile));
                else if (mr.sharedMaterial == concreteMat)
                    mf.sharedMesh = ScaledCube(t.localScale, new Vector2(ConcreteTile, ConcreteTile));
                else if (t.name == "Backdrop")
                {
                    mr.sharedMaterial = skyMat;
                    // One copy of the sky across the whole face, upright (the default cube's -Z face is flipped).
                    mf.sharedMesh = ScaledCube(t.localScale, new Vector2(t.localScale.x, t.localScale.y));
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (t.GetComponent<CloudScroller>() == null) t.gameObject.AddComponent<CloudScroller>();
                }
            }
        }

        private static void ConfigureImporters()
        {
            foreach (var n in new[] { "Grass004_Normal.jpg", "Concrete034_Normal.jpg" })
            {
                var imp = AssetImporter.GetAtPath($"{EnvArt}/{n}") as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.NormalMap)
                {
                    imp.textureType = TextureImporterType.NormalMap;
                    imp.SaveAndReimport();
                }
            }
            var sky = AssetImporter.GetAtPath($"{EnvArt}/Sky_Clouds.png") as TextureImporter;
            if (sky != null && (sky.maxTextureSize < 4096 || sky.wrapModeU != TextureWrapMode.Repeat || sky.textureShape != TextureImporterShape.Texture2D))
            {
                sky.textureShape = TextureImporterShape.Texture2D;
                sky.maxTextureSize = 4096;
                sky.wrapModeU = TextureWrapMode.Repeat;
                sky.wrapModeV = TextureWrapMode.Clamp;
                sky.SaveAndReimport();
            }
        }

        private static Material SetupLit(string name, string colorPath, string normalPath, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetTextureScale("_BaseMap", Vector2.one); // tiling comes from the scaled meshes
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SetupSky()
        {
            string path = $"{MatDir}/Sky_Clouds.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{EnvArt}/Sky_Clouds.png"));
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Unit cube whose UVs on each face are (face width, face height) / tile for the given
        /// object scale, so a stretched box shows the texture repeated rather than stretched.
        /// Cached as an asset per size.
        /// </summary>
        private static Mesh ScaledCube(Vector3 scale, Vector2 tile)
        {
            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/_Project", "Meshes");
            string key = $"Cube_{scale.x:0.##}x{scale.y:0.##}x{scale.z:0.##}_t{tile.x:0.##}x{tile.y:0.##}".Replace('.', 'p');
            string path = $"{MeshDir}/{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            // (normal, up) per face; right = Cross(normal, up) gives clockwise front faces in Unity.
            var faces = new[]
            {
                (Vector3.right, Vector3.up), (Vector3.left, Vector3.up),
                (Vector3.up, Vector3.forward), (Vector3.down, Vector3.forward),
                (Vector3.forward, Vector3.up), (Vector3.back, Vector3.up),
            };
            foreach (var (n, up) in faces)
            {
                Vector3 right = Vector3.Cross(n, up);
                float sizeR = Mathf.Abs(Vector3.Dot(right, scale));
                float sizeU = Mathf.Abs(Vector3.Dot(up, scale));
                int start = verts.Count;
                foreach (var (a, b) in new[] { (-0.5f, -0.5f), (-0.5f, 0.5f), (0.5f, 0.5f), (0.5f, -0.5f) })
                {
                    verts.Add(n * 0.5f + right * a + up * b);
                    norms.Add(n);
                    uvs.Add(new Vector2((a + 0.5f) * sizeR / tile.x, (b + 0.5f) * sizeU / tile.y));
                }
                tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
