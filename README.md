# Double Forward (双向前行)

<p align="center">
  <strong>A 2-Player Co-op Adventure Game for Android</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-2022.3%20LTS-blue?logo=unity" alt="Unity">
  <img src="https://img.shields.io/badge/Platform-Android-green?logo=android" alt="Android">
  <img src="https://img.shields.io/badge/Players-2P%20Co--op-orange" alt="Players">
  <img src="https://img.shields.io/badge/License-MIT-yellow" alt="License">
</p>

---

## About

**Double Forward** is a 2-player cooperative puzzle-adventure game where two characters with complementary abilities must work together to overcome obstacles.

**Lux** (Light) and **Nox** (Shadow) were once a single being, torn apart by a mysterious force. Players guide them through 5 unique worlds, solving puzzles that require both light and shadow to progress.

## Features

- **Asymmetric Co-op**: Two characters with unique abilities that complement each other
- **5 Themed Worlds**: Forest, Factory, Abyss, Ruins, Void - each with distinct mechanics
- **20 Handcrafted Levels**: Progressive difficulty with tutorial integration
- **3 Play Modes**: Local split-screen, LAN, and online multiplayer
- **Boss Battles**: Multi-phase bosses requiring coordinated teamwork
- **Touch + Gamepad**: Virtual joystick for touch, full gamepad support

## Characters

| | Lux (Light) | Nox (Shadow) |
|---|---|---|
| **Primary** | Light Beam - illuminate and activate | Shadow Phase - dash through walls |
| **Secondary** | Light Bridge - create temporary paths | Shadow Zone - create dark areas |
| **Movement** | Double Jump | Speed Dash |
| **Passive** | +20% speed in light | +20% attack in shadow |

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Engine | Unity 2022.3 LTS |
| Language | C# |
| Networking | Unity Netcode for GameObjects |
| Rendering | Universal Render Pipeline (URP) |
| Physics | Unity 2D Physics (Box2D) |
| Input | Unity Input System |
| UI | TextMeshPro + uGUI |

## Project Structure

```
Assets/
  Scripts/
    Core/        # GameManager, SceneLoader, SaveSystem, AudioManager
    Input/       # VirtualJoystick, TouchButton, InputManager
    Player/      # PlayerController, Abilities, Health, Animator
    Level/       # LevelManager, Checkpoint, LevelData, Progression
    Puzzle/      # PressurePlate, LightSensor, ShadowWall, Portal...
    Boss/        # BossBase, VoidBoss, BossArena, HealthBar
    Cutscene/    # DialogueSystem, CutsceneManager
    Camera/      # CameraController, SplitScreenManager
    Network/     # NetworkManager, Lobby, RoomDiscovery, Reconnect
    UI/          # MainMenu, HUD, Settings, Tutorial, LevelSelect
    Editor/      # LevelBuilder, ProjectBootstrapper, Inspectors
  Shaders/       # LightBeam, ShadowZone, LightBridge (URP HLSL)
  InputActions/  # Player input action mappings
```

## Quick Start

1. Install **Unity 2022.3 LTS**
2. Clone and open the project:
   ```bash
   git clone https://github.com/8yangstar8/DoubleForward.git
   ```
3. Open in Unity Hub, wait for package resolution
4. Menu: `DoubleForward > Project Setup Wizard` > **Run Full Setup**
5. Open `Assets/Scenes/Chapter1/Level_1_1.unity`
6. Press Play

## Building for Android

1. File > Build Settings > Switch to Android
2. Player Settings:
   - Min API: 26 (Android 8.0)
   - Scripting Backend: IL2CPP
   - Target: ARM64
3. Build and Run

## Development Roadmap

- [x] Phase 1: Core framework (movement, input, camera)
- [x] Phase 2: Gameplay systems (abilities, puzzles, split-screen)
- [x] Phase 3: Networking (Netcode, lobby, LAN discovery)
- [ ] Phase 4: Level content (20 levels across 5 chapters)
- [ ] Phase 5: Polish (audio, VFX, performance)
- [ ] Phase 6: Release (Google Play)

## License

MIT License - see [LICENSE](LICENSE) for details.
