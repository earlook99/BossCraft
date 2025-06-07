# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
BossCraft is a Unity 6 (6000.0.42f1) turn-based battle game where 4 players cooperate to defeat an AI-generated boss. The game features Pokemon-style combat mechanics with a unique boss generation system using Hugging Face AI models.

## Key Development Commands

### Unity Editor Operations
- **Open Project**: Launch Unity Hub and open the project with Unity 6000.0.42f1
- **Build Game**: File → Build Settings → Select platform → Build
- **Run Game**: Press Play button in Unity Editor
- **Build WebGL**: File → Build Settings → Switch to WebGL → Build (output to Builds/WebGL)

### Scene Navigation
- **MainMenu**: Entry point scene
- **BossGeneration**: AI boss creation scene  
- **Battle**: Main combat scene

### Important Configuration
1. **Hugging Face API Setup**: Copy `Assets/Scripts/AI/ServerConfig.cs.template` to `ServerConfig.cs` and add your Hugging Face Space URL
2. **Input System**: Uses Unity's new Input System (configured in InputSystem_Actions.inputactions)

## Architecture Overview

### Core Systems

#### Battle System (`GameSystem/`)
The heart of the game, managing turn-based combat flow:
- **BattleManager**: Orchestrates battle state machine (Initializing → PlayerChoice → BossAction → ExecutingAction → CheckBattleEnd)
- **BattleEntity**: Base class for all combatants with stats, moves, and status effects
- **DamageFormula**: Calculates damage using Pokemon-style type effectiveness
- **UtilityAI**: Boss decision-making system with weighted scoring

#### Boss Generation System (`AI/`)
Integrates with Hugging Face for AI-powered boss creation:
- **BossGenerationManager**: Handles image upload, AI generation requests, and boss creation
- **BossContainer**: Singleton storing generated boss data across scenes
- **ServerConfig**: API configuration (must be created from template)

#### Entity System (`Entity/`)
- **BattleEntity**: Core stats (HP, Attack, Defense, Speed) and battle mechanics
- **BossEntity**: Special boss features including shield patterns and AI behavior

#### UI System (`UI/` and `GameSystem/UI/`)
- **BattleUIController**: Manages battle HUD, action menus, and animations
- **CharacterStatusUI**: Health bars, status effects, shield displays
- **ActionMenuUI**: Player move selection interface
- **TargetSelectionUI**: Multi-target selection system

### Data Architecture
Uses ScriptableObjects for configuration:
- **MoveData**: Attack definitions with effects, power, accuracy, targeting
- **BattleSettings**: Game balance parameters
- **AIWeights**: Boss AI behavior tuning
- **ShieldPattern**: Boss shield mechanics
- **UITheme**: Visual styling configuration

### Key Design Patterns
1. **State Machine**: Battle flow management
2. **Manager Pattern**: Centralized system controllers
3. **ScriptableObject Data**: Decoupled game configuration
4. **Component Architecture**: Unity MonoBehaviour-based modularity

## Critical Files and Locations

### Battle Logic
- `BattleManager.cs`: Core battle loop and state management
- `DamageFormula.cs`: Damage calculation and type effectiveness
- `TypeChart.cs`: Pokemon-style type matchup matrix

### Boss AI
- `UtilityAI.cs`: AI decision scoring system
- `BossGenerationManager.cs`: Hugging Face integration
- `BossContainer.cs`: Cross-scene boss data persistence

### Player Systems
- `ActionMenuUI.cs`: Move selection interface
- `TargetSelectionUI.cs`: Target selection logic
- `BattleUIController.cs`: UI orchestration

### WebGL Features
- `FileUploader.jslib`: JavaScript bridge for web file uploads
- `WebGLFileUploader.cs`: C# wrapper for file operations

## Special Considerations

### Performance
- Uses Universal Render Pipeline (URP) with multiple quality presets
- Cinemachine for dynamic camera management
- Billboard sprites for 2.5D effect

### Multiplayer
- Currently single-player controlling 4 characters
- Infrastructure supports future multiplayer expansion

### Localization
- Korean character names: 시간의현자, 월광퇴마검사, 태양의무승, 황혼의수호자
- UI uses TextMeshPro with custom Pokemon-style fonts

### Private Files
- `ServerConfig.cs`: Contains API URLs (gitignored)
- Must be created from template before boss generation works