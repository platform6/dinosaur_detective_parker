using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AnimalsVsMonsters.EditorTools
{
    /// <summary>
    /// Builds the 2.5D template scene, placeholder sprites, materials and prefabs.
    /// Runs automatically once if the scene does not exist yet; re-run any time via
    /// Tools > Animals v. Monsters > Build 2.5D Template Scene.
    /// </summary>
    [InitializeOnLoad]
    public static class TemplateSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ArtDir = Root + "/Art/Placeholder";
        private const string MatDir = Root + "/Materials";
        private const string PrefabDir = Root + "/Prefabs";
        private const string ScenePath = "Assets/Scenes/Main25D.unity";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const int PPU = 32;

        /// <summary>An extra enemy type made from a drawing: prefab, sprite, walk/float, stats, and scene spawns.</summary>
        private readonly struct EnemyType
        {
            public readonly string PrefabName, SpritePath;
            public readonly bool Floating;
            public readonly int Hp;
            public readonly float Speed, Detect;
            public readonly Vector3[] Spawns;
            public EnemyType(string prefabName, string sprite, bool floating, int hp, float speed, float detect, params Vector3[] spawns)
            {
                PrefabName = prefabName; SpritePath = Root + "/Art/Characters/" + sprite;
                Floating = floating; Hp = hp; Speed = speed; Detect = detect; Spawns = spawns;
            }
            public string PrefabPath => PrefabDir + "/" + PrefabName + ".prefab";
        }

        // Add new drawn enemies here: one line each.
        private static readonly EnemyType[] ExtraEnemyTypes =
        {
            new EnemyType("FloatingEnemy", "Enemy_Vampire.png", floating: true, hp: 2, speed: 2f, detect: 8f,
                          new Vector3(-4f, 0.6f, 8f), new Vector3(20f, 0.6f, -9.5f)),
            new EnemyType("Enemy_Walker", "Enemy_Walker.png", floating: false, hp: 2, speed: 3.2f, detect: 7f,
                          new Vector3(0f, 0f, -8f), new Vector3(34f, 0f, -4f)),
        };
        // (Spawns above are used when adding a type; keep them in sync with Layout further down.)

        // Friendly animal companion.
        private const string AllyName = "Ally_Jerry";
        private const string AllySpritePath = Root + "/Art/Characters/Ally_Jerry.png";
        private static Vector3 AllySpawn => Layout[AllyName][0];

        static TemplateSceneBuilder()
        {
            // One-time upgrades of an existing scene: add anything whose prefab doesn't exist yet.
            if (File.Exists(ScenePath))
            {
                string playerPrefabPath = PrefabDir + "/Player.prefab";
                // No session flag here: the prefab gaining "walkFrames" is what marks this done,
                // so a failed attempt simply retries on the next script reload.
                if (File.Exists(playerPrefabPath) && File.Exists(WalkFramePath(WalkFrameCount - 1)) &&
                    !File.ReadAllText(playerPrefabPath).Contains("walkFrames"))
                {
                    var framePaths = new string[WalkFrameCount];
                    for (int i = 0; i < WalkFrameCount; i++) framePaths[i] = WalkFramePath(i);
                    RunWhenSpritesReady(framePaths, ApplyWalkCycleToPlayer);
                }

                if (!File.ReadAllText(ScenePath).Contains("m_Name: " + FieldMarker) && !SessionState.GetBool("AvM_FieldExpanded", false))
                {
                    SessionState.SetBool("AvM_FieldExpanded", true);
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        ExpandField();
                    };
                }

                if (!File.Exists(PrefabDir + "/" + AllyName + ".prefab") && !SessionState.GetBool("AvM_AllyAdded", false))
                {
                    RunWhenSpriteReady(AllySpritePath, () =>
                    {
                        SessionState.SetBool("AvM_AllyAdded", true);
                        AddAllyToScene();
                    });
                }

                foreach (var type in ExtraEnemyTypes)
                {
                    string key = "AvM_ExtraAdded_" + type.PrefabName;
                    if (File.Exists(type.PrefabPath) || SessionState.GetBool(key, false)) continue;
                    var t = type;
                    RunWhenSpriteReady(t.SpritePath, () =>
                    {
                        SessionState.SetBool(key, true);
                        AddExtraEnemyType(t);
                    });
                }
                return;
            }

            if (File.Exists(ScenePath) || SessionState.GetBool("AvM_TemplateBuilt", false)) return;
            SessionState.SetBool("AvM_TemplateBuilt", true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Build();
            };
        }

        [MenuItem("Tools/Animals v. Monsters/Build 2.5D Template Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(ArtDir);
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);

            // --- Placeholder art ---
            Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Characters/Player_Dino.png")
                                  ?? CreateSprite("Player_Placeholder", DrawPlayer(), new Vector2(0.5f, 0f));
            Sprite enemySprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Characters/Enemy_Mummy.png")
                                 ?? CreateSprite("Enemy_Placeholder", DrawEnemy(), new Vector2(0.5f, 0f));
            Sprite shadowSprite = CreateSprite("BlobShadow", DrawShadow(), new Vector2(0.5f, 0.5f));

            // --- Materials ---
            Material ground = CreateLitMaterial("Ground", new Color(0.36f, 0.62f, 0.32f));
            Material prop = CreateLitMaterial("Prop_Crate", new Color(0.62f, 0.45f, 0.27f));
            Material platform = CreateLitMaterial("Platform", new Color(0.55f, 0.55f, 0.6f));
            Material backdrop = CreateLitMaterial("Backdrop", new Color(0.45f, 0.65f, 0.85f));

            var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MatDir + "/NoFriction.physicsMaterial");
            if (noFriction == null)
            {
                noFriction = new PhysicsMaterial("NoFriction")
                {
                    dynamicFriction = 0f, staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum
                };
                AssetDatabase.CreateAsset(noFriction, MatDir + "/NoFriction.physicsMaterial");
            }

            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (input == null) Debug.LogWarning($"Input actions not found at {InputPath}; assign one on the Player.");

            // --- Prefabs ---
            GameObject playerPrefab = BuildPlayerPrefab(playerSprite, shadowSprite, noFriction, input);
            if (File.Exists(WalkFramePath(WalkFrameCount - 1))) ApplyWalkCycleToPlayer();
            GameObject enemyPrefab = BuildEnemyPrefab(enemySprite, shadowSprite, noFriction);
            var floatingPrefabs = new System.Collections.Generic.List<(GameObject prefab, Vector3[] spawns)>();
            foreach (var type in ExtraEnemyTypes)
            {
                var s = LoadSpriteImportingIfNeeded(type.SpritePath);
                if (s != null) floatingPrefabs.Add((BuildExtraEnemyPrefab(s, shadowSprite, noFriction, type), type.Spawns));
            }
            var frankFrames = File.Exists(FrankFramePath(FrankFrameCount - 1)) ? LoadFrankFrames() : null;
            GameObject frankPrefab = frankFrames != null ? BuildFrankPrefab(frankFrames, shadowSprite, noFriction) : null;
            var allySprite = LoadSpriteImportingIfNeeded(AllySpritePath);
            GameObject allyPrefab = allySprite != null ? BuildAllyPrefab(allySprite, shadowSprite, noFriction) : null;

            // --- Scene ---
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.8f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.3f);

            var env = new GameObject("Environment").transform;
            LayoutEnvironment(env);
            EnvironmentDresser.Apply(env);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = Layout["Player"][0];
            if (allyPrefab != null)
            {
                var ally = (GameObject)PrefabUtility.InstantiatePrefab(allyPrefab);
                ally.transform.position = AllySpawn;
            }

            var enemies = new GameObject("Enemies").transform;
            foreach (var pos in Layout["Enemy"])
            {
                var e = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, enemies);
                e.transform.position = pos;
            }
            if (frankPrefab != null)
                foreach (var pos in FrankSpawns)
                    ((GameObject)PrefabUtility.InstantiatePrefab(frankPrefab, enemies)).transform.position = pos;
            foreach (var (floatingPrefab, spawns) in floatingPrefabs)
            {
                foreach (var pos in spawns)
                {
                    var e = (GameObject)PrefabUtility.InstantiatePrefab(floatingPrefab, enemies);
                    e.transform.position = pos;
                }
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = EnvironmentDresser.SkyTop;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow25D>();
            follow.SetTarget(player.transform);
            SetField(follow, "target", player.transform);
            camGo.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 4.5f, -9f), Quaternion.Euler(20f, 0f, 0f));

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Animals v. Monsters] 2.5D template scene built at {ScenePath}. Press Play: WASD/left stick move, Space/South button jump, LMB/West button attack, Shift/left stick press sprint.");
        }

        // ---------------- Player walk cycle ----------------

        private const int WalkFrameCount = 8;
        private const int IdleFrameIndex = 4; // legs together
        private const int AirFrameIndex = 2;  // legs spread
        private static string WalkFramePath(int i) => $"{Root}/Art/Characters/DinoWalk/DinoWalk_{i}.png";

        /// <summary>Adds (or refreshes) the WalkCycleAnimator on the Player prefab using the DinoWalk frames.</summary>
        [MenuItem("Tools/Animals v. Monsters/Apply Dino Walk Cycle To Player")]
        public static void ApplyWalkCycleToPlayer()
        {
            var frames = new Sprite[WalkFrameCount];
            for (int i = 0; i < WalkFrameCount; i++)
            {
                frames[i] = LoadSpriteImportingIfNeeded(WalkFramePath(i));
                if (frames[i] == null)
                {
                    Debug.LogWarning($"[Animals v. Monsters] Walk frame missing: {WalkFramePath(i)}; walk cycle not applied.");
                    return;
                }
            }

            string path = PrefabDir + "/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var sr = root.GetComponentInChildren<SpriteRenderer>();
            var visual = sr.transform;
            sr.sprite = frames[IdleFrameIndex]; // so the editor view matches the idle pose

            var anim = root.GetComponent<WalkCycleAnimator>();
            if (anim == null) anim = root.AddComponent<WalkCycleAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            var arr = so.FindProperty("walkFrames");
            arr.arraySize = WalkFrameCount;
            for (int i = 0; i < WalkFrameCount; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("idleFrame").objectReferenceValue = frames[IdleFrameIndex];
            so.FindProperty("airFrame").objectReferenceValue = frames[AirFrameIndex];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Animals v. Monsters] Applied {WalkFrameCount}-frame walk cycle to the Player ({visual.name}).");
        }

        // ---------------- Frank: walks from the back toward the camera and falls off the front ----------------

        private const string FrankName = "Enemy_Frank";
        private const int FrankFrameCount = 8;
        private static string FrankFramePath(int i) => $"{Root}/Art/Characters/FrankWalk/FrankWalk_{i}.png";
        private static readonly Vector3[] FrankSpawns =
            { new Vector3(-18f, 0f, 10.5f), new Vector3(10f, 0f, 5f), new Vector3(28f, 0f, 0f) };

        private static Sprite[] LoadFrankFrames()
        {
            var frames = new Sprite[FrankFrameCount];
            for (int i = 0; i < FrankFrameCount; i++)
            {
                frames[i] = LoadSpriteImportingIfNeeded(FrankFramePath(i));
                if (frames[i] == null) { Debug.LogWarning($"[Animals v. Monsters] Frank frame missing: {FrankFramePath(i)}"); return null; }
            }
            return frames;
        }

        private static GameObject BuildFrankPrefab(Sprite[] frames, Sprite shadow, PhysicsMaterial mat)
        {
            var go = new GameObject(FrankName);
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.6f; col.radius = 0.4f; col.center = new Vector3(0f, 0.8f, 0f);
            col.sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;

            var sr = AddVisual(go, frames[0], shadow, 0.45f);
            var hp = go.AddComponent<Health>();
            SetField(hp, "maxHealth", 2);
            go.AddComponent<FrontWalkerEnemy25D>();

            var anim = go.AddComponent<WalkCycleAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("spriteRenderer").objectReferenceValue = sr;
            var arr = so.FindProperty("walkFrames");
            arr.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            so.FindProperty("idleFrame").objectReferenceValue = frames[0];
            so.FindProperty("framesPerSecond").floatValue = 8f;
            so.FindProperty("referenceSpeed").floatValue = 1.8f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(go, FrankName);
        }

        /// <summary>Builds the Frank prefab and places Franks at the back of the existing scene.</summary>
        [MenuItem("Tools/Animals v. Monsters/Add Frank (Front Walker) To Scene")]
        public static void AddFrankToScene()
        {
            var frames = LoadFrankFrames();
            var shadow = LoadSpriteImportingIfNeeded(ArtDir + "/BlobShadow.png");
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MatDir + "/NoFriction.physicsMaterial");
            if (frames == null || shadow == null) return;

            GameObject prefab = BuildFrankPrefab(frames, shadow, mat);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            Transform parent = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Enemies") parent = go.transform;

            foreach (var pos in FrankSpawns)
            {
                var e = parent != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
                    : (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                e.transform.position = pos;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Animals v. Monsters] Added {FrankSpawns.Length} {FrankName} to {ScenePath}.");
        }

        // ---------------- Field layout ----------------

        private const float FieldWidth = 80f, FieldDepth = 24f;
        private const string FieldMarker = "Pillar_1"; // present once the scene has the expanded field

        /// <summary>Where each character (by source prefab name) stands in the scene, in order of instance.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, Vector3[]> Layout = new()
        {
            ["Player"] = new[] { new Vector3(-30f, 0f, 0f) },
            ["Ally_Jerry"] = new[] { new Vector3(-31.8f, 0f, 1f) },
            ["Enemy"] = new[] { new Vector3(-12f, 0f, -6f), new Vector3(8f, 0f, 6f), new Vector3(26f, 0f, 0f) },
            ["FloatingEnemy"] = new[] { new Vector3(-4f, 0.6f, 8f), new Vector3(20f, 0.6f, -9.5f) },
            ["Enemy_Walker"] = new[] { new Vector3(0f, 0f, -8f), new Vector3(34f, 0f, -4f) },
            ["Enemy_Frank"] = FrankSpawns, // declared above, so already initialised
        };

        /// <summary>Creates or updates (by name) the ground, bounds and props under the Environment root.</summary>
        private static void LayoutEnvironment(Transform env)
        {
            Material ground = CreateLitMaterial("Ground", new Color(0.36f, 0.62f, 0.32f));
            Material prop = CreateLitMaterial("Prop_Crate", new Color(0.62f, 0.45f, 0.27f));
            Material platform = CreateLitMaterial("Platform", new Color(0.55f, 0.55f, 0.6f));
            Material backdrop = CreateLitMaterial("Backdrop", new Color(0.45f, 0.65f, 0.85f));
            float hx = FieldWidth / 2f, hz = FieldDepth / 2f;

            PlaceBox(env, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(FieldWidth, 1f, FieldDepth), ground);
            PlaceBox(env, "Backdrop", new Vector3(0f, 4f, hz + 0.5f), new Vector3(FieldWidth, 10f, 1f), backdrop);
            PlaceBox(env, "Wall_Left", new Vector3(-hx - 0.5f, 2f, 0f), new Vector3(1f, 6f, FieldDepth), platform);
            PlaceBox(env, "Wall_Right", new Vector3(hx + 0.5f, 2f, 0f), new Vector3(1f, 6f, FieldDepth), platform);
            PlaceBox(env, "Bound_Front", new Vector3(0f, 2f, -hz - 0.5f), new Vector3(FieldWidth, 6f, 1f), null);

            // Original props (middle of the field).
            PlaceBox(env, "Crate_1", new Vector3(-4f, 0.5f, 2f), Vector3.one, prop);
            PlaceBox(env, "Crate_2", new Vector3(-3f, 0.5f, 2.5f), Vector3.one, prop);
            PlaceBox(env, "Crate_3", new Vector3(-3.5f, 1.5f, 2.2f), Vector3.one, prop);
            PlaceBox(env, "Platform_1", new Vector3(5f, 0.5f, 1f), new Vector3(3f, 1f, 3f), platform);
            PlaceBox(env, "Platform_2", new Vector3(8f, 1.25f, 1.5f), new Vector3(2.5f, 0.5f, 2.5f), platform);

            // Props for the expanded area.
            PlaceBox(env, "Crate_4", new Vector3(-22f, 0.5f, -5f), Vector3.one, prop);
            PlaceBox(env, "Crate_5", new Vector3(-21f, 0.5f, -5.6f), Vector3.one, prop);
            PlaceBox(env, "Crate_6", new Vector3(30f, 0.5f, 6f), Vector3.one, prop);
            PlaceBox(env, "Crate_7", new Vector3(14f, 0.5f, 8f), Vector3.one, prop);
            PlaceBox(env, "Platform_3", new Vector3(20f, 0.5f, -6f), new Vector3(4f, 1f, 3f), platform);
            PlaceBox(env, "Platform_4", new Vector3(23.5f, 1.25f, -5.5f), new Vector3(3f, 0.5f, 3f), platform);
            PlaceBox(env, FieldMarker, new Vector3(-14f, 1.5f, 7f), new Vector3(1.5f, 3f, 1.5f), platform);
        }

        private static void PlaceBox(Transform env, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            Transform t = env.Find(name);
            if (t == null)
            {
                var go = Box(name, env, pos, scale, mat);
                if (mat == null) // invisible boundary
                {
                    Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(go.GetComponent<MeshFilter>());
                }
                return;
            }
            t.localPosition = pos;
            t.localScale = scale;
        }

        /// <summary>Moves every character prefab instance to its Layout slot; extras are kept inside the field.</summary>
        private static void LayoutCharacters(Scene scene)
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            float hx = FieldWidth / 2f - 1f, hz = FieldDepth / 2f - 1f;
            Transform player = null;

            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source == null) continue;
                if (source.name == "Player") player = t;

                counts.TryGetValue(source.name, out int i);
                counts[source.name] = i + 1;
                if (Layout.TryGetValue(source.name, out var slots) && i < slots.Length)
                    t.position = slots[i];
                else
                    t.position = new Vector3(Mathf.Clamp(t.position.x, -hx, hx), t.position.y, Mathf.Clamp(t.position.z, -hz, hz));
            }

            // Frame the editor camera on the player's new start position.
            if (player != null)
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponent<Camera>() != null)
                        root.transform.position = player.position + new Vector3(0f, 4.5f, -9f);
        }

        /// <summary>Resizes the existing scene's field to FieldWidth x FieldDepth, adds new props, and spreads everyone out.</summary>
        [MenuItem("Tools/Animals v. Monsters/Expand Field And Spread Out Characters")]
        public static void ExpandField()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            GameObject envGo = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Environment") envGo = root;
            if (envGo == null)
            {
                envGo = new GameObject("Environment");
                SceneManager.MoveGameObjectToScene(envGo, scene);
            }

            LayoutEnvironment(envGo.transform);
            EnvironmentDresser.Apply(envGo.transform);
            LayoutCharacters(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Animals v. Monsters] Field expanded to {FieldWidth}x{FieldDepth} and characters spread out in {ScenePath}.");
        }

        /// <summary>Adds every extra enemy type (vampire, walker) to the existing scene. Each run adds another set.</summary>
        [MenuItem("Tools/Animals v. Monsters/Add Extra Enemies To Scene")]
        public static void AddExtraEnemiesToScene()
        {
            foreach (var type in ExtraEnemyTypes) AddExtraEnemyType(type);
        }

        /// <summary>
        /// Creates one extra enemy prefab, gives existing characters a ground-tracking shadow,
        /// and places that enemy into the existing Main25D scene without rebuilding it.
        /// </summary>
        private static void AddExtraEnemyType(EnemyType type)
        {
            var sprite = LoadSpriteImportingIfNeeded(type.SpritePath);
            var shadow = LoadSpriteImportingIfNeeded(ArtDir + "/BlobShadow.png");
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MatDir + "/NoFriction.physicsMaterial");
            if (sprite == null || shadow == null)
            {
                Debug.LogWarning($"[Animals v. Monsters] Missing sprite ({type.SpritePath} or BlobShadow); {type.PrefabName} not added.");
                return;
            }

            // Existing characters get a shadow that stays on the ground when they jump.
            foreach (var path in new[] { PrefabDir + "/Player.prefab", PrefabDir + "/Enemy.prefab" })
            {
                if (!File.Exists(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var sh = root.transform.Find("BlobShadow");
                if (sh != null && sh.GetComponent<GroundShadow>() == null)
                {
                    sh.gameObject.AddComponent<GroundShadow>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject prefab = BuildExtraEnemyPrefab(sprite, shadow, mat, type);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            Transform parent = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Enemies") parent = go.transform;
            if (parent == null)
            {
                var enemies = new GameObject("Enemies");
                SceneManager.MoveGameObjectToScene(enemies, scene);
                parent = enemies.transform;
            }

            foreach (var pos in type.Spawns)
            {
                var e = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                e.transform.position = pos;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Animals v. Monsters] Added {type.Spawns.Length} {type.PrefabName} to {ScenePath}.");
        }

        /// <summary>Creates the Jerry companion prefab and places Jerry next to the player in the existing scene.</summary>
        [MenuItem("Tools/Animals v. Monsters/Add Jerry (Ally) To Scene")]
        public static void AddAllyToScene()
        {
            var sprite = LoadSpriteImportingIfNeeded(AllySpritePath);
            var shadow = LoadSpriteImportingIfNeeded(ArtDir + "/BlobShadow.png");
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MatDir + "/NoFriction.physicsMaterial");
            if (sprite == null || shadow == null)
            {
                Debug.LogWarning($"[Animals v. Monsters] Missing sprite ({AllySpritePath} or BlobShadow); Jerry not added.");
                return;
            }

            GameObject prefab = BuildAllyPrefab(sprite, shadow, mat);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            var ally = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            ally.transform.position = AllySpawn;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Animals v. Monsters] Added Jerry (friendly companion) to {ScenePath}.");
        }

        private static GameObject BuildAllyPrefab(Sprite sprite, Sprite shadow, PhysicsMaterial mat)
        {
            var go = new GameObject(AllyName);
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.3f; col.radius = 0.4f; col.center = new Vector3(0f, 0.65f, 0f);
            col.sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;

            var sr = AddVisual(go, sprite, shadow, 0.5f);
            var ai = go.AddComponent<AllyAI25D>();
            SetField(ai, "spriteRenderer", sr);
            return SavePrefab(go, AllyName);
        }

        /// <summary>
        /// Runs an action once the sprite at the path has finished importing as a Sprite. Newly added
        /// PNGs can still be mid-import right after a script reload, so poll for up to 30 seconds
        /// instead of trying once.
        /// </summary>
        private static void RunWhenSpriteReady(string spritePath, System.Action action) =>
            RunWhenSpritesReady(new[] { spritePath }, action);

        /// <summary>Like RunWhenSpriteReady, but waits until every listed path has imported as a Sprite.</summary>
        private static void RunWhenSpritesReady(string[] spritePaths, System.Action action)
        {
            double deadline = EditorApplication.timeSinceStartup + 30.0;
            double nextTry = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                double now = EditorApplication.timeSinceStartup;
                if (now < nextTry) return;
                nextTry = now + 0.5;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

                bool ready = System.Array.TrueForAll(spritePaths, p => AssetDatabase.LoadAssetAtPath<Sprite>(p) != null);
                if (!ready && now < deadline) return;

                EditorApplication.update -= poll;
                action(); // logs a clear warning if the sprite still isn't ready after the deadline
            };
            EditorApplication.update += poll;
        }

        // A freshly copied PNG may not be imported as a Sprite yet when this runs; force the import.
        private static Sprite LoadSpriteImportingIfNeeded(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null || !File.Exists(path)) return sprite;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------------- Prefabs ----------------

        private static GameObject BuildExtraEnemyPrefab(Sprite sprite, Sprite shadow, PhysicsMaterial mat, EnemyType type)
        {
            var go = new GameObject(type.PrefabName);
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.4f; col.radius = 0.45f; col.center = new Vector3(0f, 0.7f, 0f);
            col.sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = !type.Floating;

            var sr = AddVisual(go, sprite, shadow, 0.5f);
            var hp = go.AddComponent<Health>();
            SetField(hp, "maxHealth", type.Hp);
            var ai = go.AddComponent<EnemyAI25D>();
            SetField(ai, "spriteRenderer", sr);
            SetField(ai, "floating", type.Floating);
            SetField(ai, "hoverHeight", 0.6f);
            SetField(ai, "moveSpeed", type.Speed);
            SetField(ai, "detectRange", type.Detect);
            SetField(ai, "loseRange", type.Detect * 1.5f);

            return SavePrefab(go, type.PrefabName);
        }

        private static GameObject BuildPlayerPrefab(Sprite sprite, Sprite shadow, PhysicsMaterial mat, InputActionAsset input)
        {
            var go = new GameObject("Player");
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.4f; col.radius = 0.35f; col.center = new Vector3(0f, 0.7f, 0f);
            col.sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var sr = AddVisual(go, sprite, shadow, 0.45f);
            var hp = go.AddComponent<Health>();
            SetField(hp, "maxHealth", 5);
            SetField(hp, "destroyOnDeath", false);
            SetField(hp, "invulnerableTime", 1f);

            var pc = go.AddComponent<PlayerController25D>();
            SetField(pc, "inputActions", input);
            SetField(pc, "spriteRenderer", sr);

            return SavePrefab(go, "Player");
        }

        private static GameObject BuildEnemyPrefab(Sprite sprite, Sprite shadow, PhysicsMaterial mat)
        {
            var go = new GameObject("Enemy");
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1f; col.radius = 0.45f; col.center = new Vector3(0f, 0.5f, 0f);
            col.sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;

            var sr = AddVisual(go, sprite, shadow, 0.55f);
            var hp = go.AddComponent<Health>();
            SetField(hp, "maxHealth", 3);
            var ai = go.AddComponent<EnemyAI25D>();
            SetField(ai, "spriteRenderer", sr);

            return SavePrefab(go, "Enemy");
        }

        private static SpriteRenderer AddVisual(GameObject root, Sprite sprite, Sprite shadow, float shadowSize)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            visual.AddComponent<Billboard>();

            var sh = new GameObject("BlobShadow");
            sh.transform.SetParent(root.transform, false);
            sh.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.transform.localScale = new Vector3(shadowSize * 2f, shadowSize, 1f);
            var ssr = sh.AddComponent<SpriteRenderer>();
            ssr.sprite = shadow;
            ssr.color = new Color(0f, 0f, 0f, 0.45f);
            ssr.sortingOrder = -1;
            sh.AddComponent<GroundShadow>();
            return sr;
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------- Scene helpers ----------------

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.isStatic = true;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material CreateLitMaterial(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"Field '{field}' not found on {target.GetType().Name}"); return; }
            switch (value)
            {
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case bool b: p.boolValue = b; break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------- Placeholder sprite generation ----------------

        private static Sprite CreateSprite(string name, Texture2D tex, Vector2 pivot)
        {
            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PPU;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color32[w * h];
            t.SetPixels32(clear);
            return t;
        }

        private static void Ellipse(Texture2D t, float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = 0; y < t.height; y++)
            for (int x = 0; x < t.width; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy <= 1f) t.SetPixel(x, y, c);
            }
        }

        private static void FillRect(Texture2D t, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c);
        }

        private static void Outline(Texture2D t, Color c)
        {
            var src = t.GetPixels32();
            int w = t.width, h = t.height;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (src[y * w + x].a > 0) continue;
                bool edge = (x > 0 && src[y * w + x - 1].a > 0) || (x < w - 1 && src[y * w + x + 1].a > 0) ||
                            (y > 0 && src[(y - 1) * w + x].a > 0) || (y < h - 1 && src[(y + 1) * w + x].a > 0);
                if (edge) t.SetPixel(x, y, c);
            }
        }

        // A friendly fox-ish animal, 32x48, facing right.
        private static Texture2D DrawPlayer()
        {
            var t = NewTex(32, 48);
            Color fur = new Color(0.95f, 0.55f, 0.2f), belly = new Color(1f, 0.92f, 0.8f);
            Color dark = new Color(0.2f, 0.12f, 0.08f), white = Color.white;

            FillRect(t, 10, 1, 4, 8, dark);            // legs
            FillRect(t, 18, 1, 4, 8, dark);
            Ellipse(t, 16, 18, 9, 11, fur);         // body
            Ellipse(t, 17, 16, 5, 7, belly);        // belly
            Ellipse(t, 6, 16, 5, 3, fur);           // tail
            Ellipse(t, 3, 16, 2, 2, white);         // tail tip
            Ellipse(t, 17, 34, 9, 8, fur);          // head
            Ellipse(t, 21, 31, 5, 3.5f, belly);     // muzzle
            FillRect(t, 25, 31, 2, 2, dark);            // nose
            for (int i = 0; i < 6; i++)             // ears
            {
                FillRect(t, 10 + i / 2, 40 + i, 6 - i, 1, fur);
                FillRect(t, 18 + i / 2, 40 + i, 6 - i, 1, fur);
            }
            FillRect(t, 19, 35, 3, 3, white);           // eye
            FillRect(t, 20, 35, 2, 2, dark);
            Outline(t, dark);
            t.Apply();
            return t;
        }

        // A purple one-eyed horned monster, 40x32, facing right.
        private static Texture2D DrawEnemy()
        {
            var t = NewTex(40, 32);
            Color skin = new Color(0.55f, 0.25f, 0.7f), skinDark = new Color(0.38f, 0.15f, 0.5f);
            Color horn = new Color(0.95f, 0.9f, 0.75f), dark = new Color(0.12f, 0.05f, 0.15f);

            FillRect(t, 11, 1, 5, 5, skinDark);          // feet
            FillRect(t, 24, 1, 5, 5, skinDark);
            Ellipse(t, 20, 14, 15, 11, skin);        // body
            Ellipse(t, 20, 9, 11, 5, skinDark);      // underside
            for (int i = 0; i < 6; i++)              // horns
            {
                FillRect(t, 9 + i / 2, 23 + i, 4 - i / 2, 1, horn);
                FillRect(t, 27 - i / 2, 23 + i, 4 - i / 2, 1, horn);
            }
            Ellipse(t, 23, 17, 6, 6, Color.white);   // eye
            Ellipse(t, 25, 17, 3, 3.5f, new Color(0.9f, 0.1f, 0.1f));
            FillRect(t, 25, 16, 2, 2, dark);
            FillRect(t, 14, 8, 16, 3, dark);             // mouth
            for (int x = 15; x < 30; x += 3) FillRect(t, x, 9, 2, 2, horn); // teeth
            Outline(t, dark);
            t.Apply();
            return t;
        }

        private static Texture2D DrawShadow()
        {
            const int s = 32;
            var t = NewTex(s, s);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d * d)));
            }
            t.Apply();
            return t;
        }
    }
}
