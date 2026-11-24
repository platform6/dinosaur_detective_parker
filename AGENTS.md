# AGENTS.md: Animals v. Monsters

A 2.5D brawler in Unity 6 (6000.3) with URP and the new Input System. Hand-drawn sprites stand in a 3D world. Physics runs on the XZ plane, and an angled camera follows the player.

## Layout

| Path | What's there |
|---|---|
| `Assets/Scenes/Main25D.unity` | The only scene (first in Build Settings) |
| `Assets/_Project/Scripts/Runtime/` | Gameplay code. asmdef `AnimalsVsMonsters.Runtime`, namespace `AnimalsVsMonsters` |
| `Assets/_Project/Scripts/Editor/` | Editor tooling. asmdef `AnimalsVsMonsters.Editor`, namespace `AnimalsVsMonsters.EditorTools` |
| `Assets/_Project/Prefabs/` | `Player`, `Enemy` (mummy), `FloatingEnemy` (vampire), `Enemy_Walker`, `Enemy_Frank`, `Ally_Jerry` |
| `Assets/_Project/Art/Characters/` | Cleaned character sprites and walk-cycle frames (`DinoWalk/`, `FrankWalk/`) |
| `Assets/_Project/Art/Placeholder/` | Original photos and sprite sheets, plus procedural placeholder art |
| `Assets/_Project/Art/Environment/` | CC0 ambientCG textures (Grass004, Concrete034) and the generated `Sky_Clouds.png` |
| `Tools/ArtPipeline/` | Python scripts that clean drawings, split sprite sheets and generate the sky (outside `Assets/`, so Unity ignores them) |
| `Assets/_Project/Materials/`, `Meshes/` | Environment materials and per-size cube meshes (generated; don't hand-edit) |

## Runtime scripts

- `PlayerController25D`: moves relative to the camera, sprints, jumps (SphereCast ground check) and attacks (OverlapSphere). Exposes `IsGrounded` and `Facing`.
- `Health`: HP, invulnerability frames, knockback, a damage flash and the `OnHealthChanged`/`OnDied` events.
- `EnemyAI25D`: wanders, chases and deals contact damage. It has an optional floating (hover and bob) mode.
- `FrontWalkerEnemy25D` (Frank): walks toward the camera, falls off the front edge (it ignores `Bound_Front`), then respawns at the back.
- `AllyAI25D` (Jerry): follows the player and attacks nearby enemies.
- `WalkCycleAnimator`: flipbook sprite animation driven by Rigidbody speed.
- `Billboard`, `GroundShadow`, `GroundUtil`, `CameraFollow25D`, `CloudScroller`: presentation helpers.

## Conventions

- **Scene and prefab changes are code.** Put persistent setup in `TemplateSceneBuilder.cs` or `EnvironmentDresser.cs` as a `Tools/Animals v. Monsters/...` menu item, so the scene can always be rebuilt. Hand edits to the scene are lost the next time **Build 2.5D Template Scene** runs.
- Character placement lives in the `Layout` dictionary in `TemplateSceneBuilder`. The field size is set by `FieldWidth` (80) and `FieldDepth` (24).
- Use Rigidbody `linearVelocity`, `FindFirstObjectByType` and `FindObjectsByType`, not the deprecated APIs.
- Never use `?.` or `??` on Unity objects, because of Unity's fake null. Write an explicit `== null` check.
- In the builder, static fields initialize in declaration order. A field can only reference fields declared above it.

## Art pipeline

The scripts are in `Tools/ArtPipeline/` (Python 3 + Pillow); usage is in its `README.md`. To change how art is processed, improve and reuse these scripts instead of writing one-off ones.

- **Characters:** `drawing_to_sprite.py` cleans photos of orange-marker drawings are cleaned with Python and Pillow:
  - The paper becomes transparent.
  - The fill becomes cream (255, 236, 214) and the ink is normalised to (151, 73, 18).
  - Sprites are mirrored to face right and scaled to about 255 px tall.
  - Results are saved in `Art/Characters/`.
- **Sprite sheets:** `sheet_to_frames.py` (white or glowing background) or `alpha_sheet_to_frames.py` (transparent background) splits a sheet into per-frame PNGs, aligned on the body centre with a shared foot baseline.
- **Import settings:** `CharacterSpriteImporter` sets up any texture under `Art/Characters/` automatically: Sprite, PPU 150, bottom-centre pivot, and it forces `Texture2D` shape. Don't override these by hand.
- **New PNGs:** let Unity create the `.meta` files. Gate builder steps on the sprites actually loading; `RunWhenSpritesReady` does this.
- **Environment:** run **Tools → Animals v. Monsters → Apply Environment Textures**. Textured boxes get generated meshes with world-scaled UVs. Unity's default cube has flipped UVs on its −Z face, and stretched boxes would distort the texture.
- New third-party assets must be CC0 or similarly permissive. Add a licence note next to them.

## Editor automation (unity-editor-mcp)

If the `unity-editor-mcp` server is connected (its tools are deferred, so load them via ToolSearch), drive the Editor directly:

1. Edit the scripts, then `recompile` and poll `recompile_status`.
2. Run the relevant menu item with `invoke_menu`.
3. Verify by querying state with `eval` (C#). A successful menu call alone proves nothing.
4. For a visual check, `start` Play mode and take a `game_view` screenshot (default `source=screen`, with `output-path` set). In Edit mode with `source=camera`, only sprites render. Then `stop`.

In Play mode, static batching shows environment meshes as "Combined Mesh". That is expected.

Without MCP, use `[InitializeOnLoad]` one-shots that run once Unity has focus, and check the results in `%LOCALAPPDATA%\Unity\Editor\Editor.log`.

## Gotchas

- After a scripted text edit (PowerShell `.Replace`, sed), grep to confirm it applied; one silently failed before. Prefer a direct file edit.
- The camera (offset (0, 4.5, −9), pitch 20°, FOV 40) only sees the backdrop up to about y = 4.5. Put any sky detail in that band.
- **Git:** the project is a git repo on `main`, with binary art in Git LFS (see `.gitattributes`).
  - Always commit `.meta` files together with their assets.
  - Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or the generated `.csproj`/`.slnx` files.
