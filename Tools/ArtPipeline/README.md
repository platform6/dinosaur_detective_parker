# Art pipeline scripts

These Python scripts turn the hand-drawn art into game-ready sprites. They need Python 3 and Pillow (`pip install pillow`). The folder sits outside `Assets/`, so Unity ignores it.

Run every command from the project root.

## drawing_to_sprite.py: one photographed drawing → one sprite

```
python Tools/ArtPipeline/drawing_to_sprite.py <src photo> <dst.png> [height=256] [mirror|nomirror] [ops...]
```

- **What it does:**
  - Turns the paper transparent.
  - Fills the inside of the figure with cream (255, 236, 214).
  - Normalises the ink to (151, 73, 18).
  - Crops tight and scales the sprite to `height` px.
- **Direction:** by default the output is mirrored so the character faces right. Pass `nomirror` if the drawing already faces right. To use ops without mirroring, put any other word in this 4th slot.
- **Ops** run in order, in pixel coordinates of the image at that point:
  - `crop:x0,y0,x1,y1` keeps only that box.
  - `erase:x0,y0,x1,y1` paints the box with the paper colour, to remove stray marks.
  - `rotate:deg` rotates counter-clockwise; use it to stand a tilted figure upright.

Example (the vampire):

```
python Tools/ArtPipeline/drawing_to_sprite.py Assets/_Project/Art/Placeholder/vampire.JPEG Assets/_Project/Art/Characters/Enemy_Vampire.png 255
```

## sheet_to_frames.py / alpha_sheet_to_frames.py: walk-cycle sheet → frames

```
python Tools/ArtPipeline/sheet_to_frames.py       <sheet.png> <out dir> <prefix> [height=255]
python Tools/ArtPipeline/alpha_sheet_to_frames.py <sheet.png> <out dir> <prefix> [height=255]
```

- **Which one:**
  - Use `sheet_to_frames.py` for a horizontal sheet on a white or glowing background.
  - Use `alpha_sheet_to_frames.py` for a sheet that already has transparency. It also drops stray red and yellow specks and keeps only the main figure in each frame.
- **Output:** `<prefix>_0.png`, `<prefix>_1.png`, … All frames are the same size, aligned on the body centre and share a foot baseline, so the cycle doesn't jitter.
- **Preview:** both scripts also write `<prefix>_contact.png` to check the frames at a glance. Write to a temporary folder first, then copy only the numbered frames into `Assets/_Project/Art/Characters/<Name>Walk/`, so the preview doesn't get imported as a sprite.

## make_sky.py: cartoon cloud sky

```
python Tools/ArtPipeline/make_sky.py Assets/_Project/Art/Environment/Sky_Clouds.png
```

- **Output:** a 4096×512 image that tiles seamlessly left to right: a blue gradient with puffy outlined clouds.
- **Where the clouds sit:** they go in the lower band (`uniform(0.55, 0.80)`), because the camera only sees the backdrop up to about y = 4.5.
- **Customising:**
  - Change `random.seed` to get a different cloud layout.
  - The `top` and `horizon` colours set the gradient.
  - If you change `top`, update `EnvironmentDresser.SkyTop` to match.

## After generating art

- Images under `Assets/_Project/Art/Characters/` are imported as sprites automatically by `CharacterSpriteImporter` (PPU 150, bottom-centre pivot).
- Wire new art into prefabs through a `Tools/Animals v. Monsters/...` menu item in `TemplateSceneBuilder.cs`. See `AGENTS.md`.
