# MVHS Unity Editor Tools

A collection of Unity Editor tools to support group projects and collaborative game development workflows. These tools handle project setup, asset validation, and asset distribution for teams using Git.

All tools live under `Assets/Editor/MVHS/` and share a common configuration through `AssetPipelineConfig.cs`. They are accessed from the Unity menu at **Tools > MVHS**.

## Installation

1. Download the latest `.unitypackage` from the [Releases](../../releases) page
2. Open your Unity project
3. Go to **Assets > Import Package > Custom Package** and select the downloaded `.unitypackage`
4. Import all files — the tools will be placed in `Assets/Editor/MVHS/` automatically
5. The tools are now available under **Tools > MVHS**

Check the [Releases](../../releases) page for version notes when updating to a newer release.

## Tools

### Project Directory Creator

**Menu:** Tools > MVHS > Project Directory Creator

Creates the standard project folder structure used for all unity projects in game programming course. Only missing folders are created — running it on an existing project is safe. This should be the first tool run when setting up a new project.

---

### Asset Structure Validator

**Menu:** Tools > MVHS > Asset Structure Validator

Scans scene and GameObject dependencies to verify that assets are in the correct project folders.

**Tabs:**

- **Scene Dependency Viewer** — scans all loaded scenes and checks every referenced asset against the validation rules
- **GameObject Dependency Viewer** — scans a single selected GameObject and its dependencies
- **Rules** — displays rules where each asset type must be placed

Assets that fail validation can be moved to the correct folder directly from the tool. Assets can also be moved inside the unity editor. **Do not move assets from outside Unity.** 

---

### Asset Pipeline Manager

**Menu:** Tools > MVHS > Asset Pipeline Manager

Handles exporting and importing `.unitypackage` files for distributing binary assets (models, textures, audio) that are not tracked in Git.

**Export** collects all assets from the configured project folders, bundles them into a `.unitypackage` (preserving `.meta` files and GUIDs), and generates a manifest file recording what was included.

**Import** opens a file browser to select and import a `.unitypackage` received from a teammate.

**Startup check:** When Unity opens the project, `AssetPipelineStartupCheck` automatically reads all manifests created from exporting assets. If any folders are missing assets that the manifests say should exist, a warning dialog appears reminding to inport assets before saving any work. Folders that are empty because no one has added assets there yet are left alone — the check only flags folders where a manifest expects files.

The startup check can also be run manually from **Tools > MVHS > Asset Pipeline Startup Check**.

---

## Workflow

**Project lead (initial setup):**
1. Create a new Unity project
2. Import the MVHS tools `.unitypackage` from the latest release
3. Run **Tools > MVHS > Project Directory Creator**
4. Import assets into the appropriate project folders
5. Run Asset Structure Validator to verify placement
6. Run Asset Pipeline Manager > Export
7. Upload the exported `.unitypackage` to a shared location (Google Drive)
8. Commit and push to Git (the repo contains scripts, scenes, prefabs, manifests — not external binary assets)

**Teammates (after cloning):**
1. Clone the repository (the MVHS tools are already in the repo under `Assets/Editor/MVHS/`)
2. Open Unity — the startup check warns if assets are missing
3. Open **Tools > MVHS > Asset Pipeline Manager** > Import
4. Select the asset `.unitypackage` from the shared location
5. All asset references reconnect automatically

**When new assets are added:**
1. Import assets into the correct project folders
2. Run Asset Structure Validator to verify
3. Export a new package and upload it
4. Commit the new manifest file
5. Notify teammates to pull and re-import

