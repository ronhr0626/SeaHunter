# Sea Hunter

A 2D arcade submarine game built in C# with [Avalonia UI](https://avaloniaui.net/), inspired by classic arcade games like *Sea Wolf* and *Depth Charge*.

You command a submarine. Sink as many ships as you can before their depth charges sink you.

## Features

- Smooth 60 fps game loop with delta time
- Submarine that moves and dives with sprite graphics
- Torpedoes: 5 shots, then reload
- Three ship types sailing at different distances
- Depth charges dropped by armed ships
- 3 lives with short invulnerability after being hit
- Levels: every 1000 points brings more ships and more bombs
- Start screen with instructions, and game over screen
- Top 10 high scores saved to file
- Pixel art scaled sharply to any window size

## Controls

| Key | Action |
|---|---|
| A D / ← → | Move left and right |
| W S / ↑ ↓ | Rise and dive |
| Space | Fire torpedo |
| Enter | Start / play again |

## Targets

| Ship | Distance | Points | Notes |
|---|---|---|---|
| Patrol boat | Far | 450 | Fast, drops depth charges |
| Freighter | Middle | 200 | Unarmed |
| Battleship | Near | 50 | Takes 2 hits, drops depth charges |

## Running the project

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/ronhr0626/SeaHunter.git
cd SeaHunter
dotnet run
```

## Overview

![SeaHunter diagrams](Docs/SeaHunter.png)

## Structure

```
Assets/           Background and sprites (PNG)
Game/             Game logic, no UI
  GameWorld.cs        All game rules: spawning, hits, score, levels, lives
  Submarine.cs        The player
  Ship.cs             Ship types, lanes, points and depth charge timing
  Torpedo.cs          Player shots
  DepthCharge.cs      Enemy bombs
  Explosion.cs        Explosion effect
  InputState.cs       Which keys are held or just pressed
  HighScoreStore.cs   Top 10 saved as JSON in AppData
GameCanvas.cs     Game loop and all drawing
MainWindow.axaml  Start screen and game over screen (XAML)
Docs/             Diagrams
```

## What I learned

- **Custom rendering** with `DrawingContext` in Avalonia: images, shapes and text
- **Game loop** with `DispatcherTimer` and **delta time**, so speed is the same on every PC
- **Virtual resolution** (640x360) scaled to the window with transforms
- **Sprites**: drawing, mirroring and keeping pixel art sharp
- **Input handling**: held keys vs. one-shot presses
- **Collision detection** with rectangle overlap and fair hitboxes
- **Separating logic from drawing**: `GameWorld` knows nothing about the screen
- **Events**: the game tells the window when it is over
- **XAML overlays** on top of a custom control, scaled with `Viewbox`
- **File handling** with JSON for high scores

## Tech

- C# / .NET 10
- Avalonia 12