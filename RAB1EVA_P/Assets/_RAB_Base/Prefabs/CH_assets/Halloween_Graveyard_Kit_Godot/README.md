# Low Poly Graveyard Kit — Halloween & Horror 3D Assets (Godot)
by Omie's Assets — https://linktr.ee/Omies_Assets

A stylized, Tim Burton-inspired graveyard kit for Godot 4: crooked tombstones, a wonky mausoleum,
curly iron fences, dead trees, glowing jack-o'-lanterns and everything you need to dress a spooky scene.

## Requirements
- Godot 4.4 or newer (built and tested in Godot 4.7.2, Forward+)
- The demo graveyard uses the free Terrain3D plugin (MIT), included in `addons/terrain_3d`.
  The models, materials and prop scenes work without it.

## What's inside
| Folder | Contents |
|---|---|
| `models/1K`, `models/2K`, `models/4K` | 33 game-ready `.glb` models, sorted by texture size |
| `textures/` | PBR textures (BaseColor, Normal, ORM, Emissive) + tileable grass & dirt |
| `materials/` | 8 ready-made materials, already linked to the models; ground-blend Visual Shader |
| `scenes/PropLayout.tscn` | Every prop laid out and labelled |
| `demo/Graveyard_Demo.tscn` | Full graveyard demo scene (Terrain3D, fog, flickering lights) |
| `demo/GroundBlendTest.tscn` | Example of the vertex-paint grass/dirt blend shader |

## Quick start
1. Open the folder in Godot (Import → select `project.godot`).
2. Let it import, then press **F5** to run the graveyard demo.
3. Fly camera: hold **Right Mouse** to look, **WASD** to move, **Q/E** down/up, **Shift** for speed.
4. Drag any `.glb` from `models/` into your own scene — the materials come with it.

## Ground blend shader (`Mat_GroundBlend`)
Paint vertex colour **red** for dirt, **black** for grass. Sliders: tile size, world-space UVs,
blend sharpness, height influence, tile-rotation variation, and HSL per layer (grass / dirt).

## Licence
See `LICENSE.txt` — use in your commercial or non-commercial projects; do not resell or redistribute the assets.

## Credits
Models, textures and scenes: Omie's Assets. Terrain3D: Cory Petkovsek & Roope Palmroos (MIT, see addons/terrain_3d/LICENSE.txt).
