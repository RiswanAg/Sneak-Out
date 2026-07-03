# Escape!

A 2-player co-op stealth game built in Unity (URP) with Photon PUN networking.
Two players (Hazim and Amir) sneak through a school/dorm across 3 levels,
avoiding an NPC warden and guards via a sound + vision detection system,
solving minigames, and reaching checkpoints together to win.

- **Unity version:** `6000.5.1f1` (Unity 6.5)
- **Render pipeline:** URP
- **Networking:** Photon PUN 2

## ⚠️ This repo does not contain third-party assets

To keep the repository small, the packages below are excluded via
`.gitignore` and are **not** in version control. Cloning this repo gives you
all custom code, scenes, and project config — but the project **will not
open cleanly in Unity** until these are re-imported into the matching
`Assets/` folder paths.

### Required to compile (the project has hard script dependencies on these)

| Folder | Package | Where to get it |
|---|---|---|
| `Assets/Photon/` | Photon PUN 2 | [Unity Asset Store – PUN 2 FREE](https://assetstore.unity.com/packages/tools/network/pun-2-free-119922). After importing, set up a free App ID at the [Photon Dashboard](https://dashboard.photonengine.com/) and paste it into `PhotonServerSettings` (Window → Photon Unity Networking → Highlight Server Settings). |
| `Assets/TextMesh Pro/` | TextMesh Pro Essentials | Unity will prompt you to import this automatically (Window → TextMeshPro → Import TMP Essential Resources) the first time you open the project — you likely don't need to source this separately. |
| `Assets/StarterAssets/` | Unity Starter Assets (ThirdPerson) | [Unity Asset Store – Starter Assets - ThirdPerson](https://assetstore.unity.com/packages/essentials/starter-assets-thirdperson-updates-in-new-charactercontroller-pa-196526) |

### Environment / prop / VFX packs (used for level dressing)

These are referenced by scenes/prefabs for visuals. The project may still
open and compile without them, but scenes will show missing-material/pink
placeholders and some prefabs will be broken until reimported.

| Folder | Notes |
|---|---|
| `Assets/Asset/` | Large mixed folder of ~40 individual free/purchased packs (furniture, environment props, terrain textures, character models, etc. — includes `HandballStadium`, `Unhyeongung`, `Abandoned_Asylum`, `LeartesStudios`, `Furniture Mega Pack`, and many small single-prop packs). No single source — these were pulled from multiple sites (Asset Store, Sketchfab, CGTrader, itch.io). Re-sourcing this folder fully will take manual digging; much of it (e.g. `HandballStadium`, the whole of `TerrainDemoScene_URP`) is unused demo content and can likely be skipped entirely. |
| `Assets/Bublisher/` | Asset Store pack (name as imported) |
| `Assets/Gabies_Assets/` | Asset Store pack |
| `Assets/Hovl Studio/` | Hovl Studio VFX pack ([hovlstudio.com](https://www.hovlstudio.com/) / Asset Store) |
| `Assets/Low_Poly_Road_Pack/` | Asset Store — Low Poly Road Pack |
| `Assets/Mesh Master/` | Asset Store pack |
| `Assets/Pak Guard/` | Asset Store pack |
| `Assets/Polyeler/` | Asset Store — includes "Simple Retro Car" |
| `Assets/Polytope Studio/` | Polytope Studio pack (Asset Store) |
| `Assets/Prototype Collection/` | Asset Store — greybox/prototyping kit |
| `Assets/StoneBricksSplitface001/` | PBR material pack (likely from a texture site like ambientCG/Poliigon) |
| `Assets/Synty Animation/` | Synty Studios animation pack ([syntystore.com](https://syntystore.com/)) |
| `Assets/TerrainDemoScene_URP/` | Unused demo scene (confirmed zero references from actual game scenes) — **safe to skip re-importing.** |
| `Assets/nappin/` | Includes "Office Essentials Pack" |

### Also removed (junk, not a real dependency)

- `Assets/Sprite/EpicInstaller-19.0.0.msi` and `Assets/Sprite/Thunderstore Mod Manager - Installer.exe` — Windows installers that had accidentally been imported into the project. Not needed for anything.

## Setup steps

1. Install Unity **6000.5.1f1** via Unity Hub.
2. Clone this repo and open it as a Unity project.
3. Unity will report missing packages/compile errors on first open — this is expected.
4. Re-import Photon PUN 2 and Starter Assets (required — see table above) into their original folder paths.
5. Create a free Photon account and App ID, and set it in `PhotonServerSettings`.
6. Optionally re-import the environment packs if you need the scenes to look correct; skip `TerrainDemoScene_URP` and anything else confirmed unused.
7. Open `Assets/Scenes/Main Menu.unity` and press Play.

## Scenes

| Scene | Purpose |
|---|---|
| `Main Menu` | Room creation/joining via Photon |
| `IntroCutscene` | Opening cutscene |
| `Level 1` / `Level 2` / `Level 3` | Gameplay levels |
| `Level1VictoryCutscene` / `Level2VictoryCutscene` | Post-level cutscenes |
| `SampleScene` | Unity default template scene, unused |
