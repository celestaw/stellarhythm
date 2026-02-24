# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Stellar Rhythm** is a Unity 6 action-rhythm game (version 0.1.0) built on the Universal Render Pipeline (URP). The project is in early development — the core input system and rendering pipeline are configured, but gameplay scripts have not yet been written.

## Build & Development

This is a standard Unity project. There are no CLI build scripts; all building happens through the Unity Editor or Unity's batch-mode CLI.

**Unity batch-mode build (Windows 64-bit):**
```
unity -projectPath . -buildTarget Win64 -executeMethod BuildScript.Build -quit
```

**Opening the project:** Open the root folder in Unity Hub (Unity 6, URP template).

**Main scene:** `Assets/Scenes/SampleScene.unity`

## Architecture

### Input System
`Assets/InputSystem_Actions.inputactions` — the single input asset defining all controls via Unity's new Input System (v1.18.0). Two action maps:
- **Player:** Move, Look, Attack, Interact, Jump, Sprint, Crouch, Previous, Next — supports Keyboard/Mouse, Gamepad, Touch, Joystick, XR.
- **UI:** Standard navigation (Navigate, Submit, Cancel, Point, Click, Scroll).

When adding player scripts, generate a C# class from this asset (right-click → Generate C# Class) rather than reading raw input directly.

### Rendering
Dual render pipeline assets for different platforms:
- `Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset` — high-quality desktop
- `Assets/Settings/Mobile_RPAsset.asset` + `Mobile_Renderer.asset` — mobile-optimized

Post-processing volumes: `DefaultVolumeProfile.asset`, `SampleSceneProfile.asset`.

### Assembly Structure
- `Assembly-CSharp` — main game runtime scripts (currently only `Assets/TutorialInfo/Scripts/Readme.cs`)
- `Assembly-CSharp-Editor` — editor-only scripts (currently only `ReadmeEditor.cs`)

New gameplay scripts go under `Assets/Scripts/` (create this folder). Editor utilities go under `Assets/Editor/`.

### Key Packages
| Package | Purpose |
|---|---|
| `com.unity.inputsystem` v1.18.0 | Player/UI input |
| `com.unity.render-pipelines.universal` v17.3.0 | URP rendering |
| `com.unity.ai.navigation` v2.0.10 | NavMesh for AI |
| `com.unity.timeline` v1.8.10 | Cutscenes / sequencing |
| `com.unity.test-framework` v1.6.0 | Unity Test Runner |

### Testing
Tests use the Unity Test Framework. Run via **Window → General → Test Runner** in the Editor, or via batch mode:
```
unity -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```
