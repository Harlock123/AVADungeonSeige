# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers the game, the controls, the solution
layout, the content files and the test list in detail; this file covers how to build it and the rules that keep it
working.

## What this is

A recreation of the look, feel and opening chapter of Dungeon Siege (2002), in C# on .NET 10 and Avalonia 12: real-time
pausable party combat, skills that rise by use, and one continuous world that streams in around the party. Everything
is drawn in code (custom `DrawingContext` views on Skia); there are no image or sound assets.

## Commands

```bash
dotnet build AVADungeonSeige.slnx                 # needs the .NET 10 SDK; there is no global.json
dotnet test AVADungeonSeige.slnx                  # Core tests (xUnit) and headless UI tests; no window opens
dotnet run --project src/DungeonSiege.App         # play it
./tools/screenshots.sh                            # regenerate screenshots/ through the headless UI tests
./build.sh                                        # publish all six platforms into BUILDS/ (build.ps1 on Windows)
```

- The screenshot tests write nothing unless `AVADS_README_SHOTS` names a folder; the script sets it.
- `Directory.Build.props` sets net10.0, nullable, implicit usings, deterministic builds and central package
  versions (`Directory.Packages.props`). Warnings are not errors.
- The app takes no command-line arguments of its own.

## Layout

- `src/DungeonSiege.Core/`: the engine, with no UI references.
  - `Engine/GameSession.cs` owns a play-through: the fixed-step loop, commands and dialogue scripting.
    `GameSession.Simulation.cs` has AI and combat.
  - `World/`: `WorldStreamer` loads and unloads regions around the party; `RegionInstance`, `WorldState`, `Enemy`,
    `Npc`, `Prop`.
  - `Characters/`, `Parties/`, `Items/`, `Quests/`, `Dialogue/`: what their names say.
  - `Content/`: `ContentSchemas`, `ContentDatabase` (loads and cross-checks), and `Data/*.json`, the whole of
    Chapter 1, embedded in the assembly.
- `src/DungeonSiege.App/`: the Avalonia front end (MVVM with CommunityToolkit.Mvvm), published as `AVADungeonSeige`.
  - `Views/MainWindow.axaml.cs` handles keys; `MainView` holds the world view and the HUD.
  - `Controls/WorldView.cs` and `MinimapView.cs` draw the world; `ViewModels/MainViewModel.cs` ticks the session and
    refreshes the HUD.
  - `Services/GameLoop.cs` is the 60 Hz frame clock; `Themes/` holds the stone-look styles.
- `tests/DungeonSiege.Core.Tests/`: content, progression and session tests.
- `tests/DungeonSiege.App.Tests/`: headless tests on the real `MainWindow`; `ReadmeScreenshots.cs` stages each
  screenshot scene.

## Rules that keep it working

- **Fixed steps.** `GameSession.Advance(elapsed)` runs the simulation in fixed 1/30 s steps (`FixedStep`) whatever
  the frame rate. A test checks that 60 fps and 20 fps hosts simulate the same steps.
- **One seeded random source.** All game randomness comes from the session's `Random(seed)` (seed 2002 by default).
  Don't add other random sources to the simulation; the screenshot scenes rely on replaying identically. The
  renderer's own `Random` instances only vary decoration.
- **Engine first.** Gameplay belongs in Core, which tests reach without a window. The app issues commands and reads
  state; the engine reports back through `MessageLogged`, `RegionEntered` and `PauseChanged`.
- **One thread.** The loop runs on the UI thread, so the engine has no locks. Keep it that way.
- **Content is validated.** `ContentDatabase` checks every cross-reference at load and fails with a list of problems.
  Add new content to the JSON files, keeping the ids consistent.
- **Nothing is saved.** There are no save files or high scores yet.

## Making a change

- Add or update Core tests with every gameplay change; `dotnet test` must pass.
- If the change shows on screen, rerun `tools/screenshots.sh` and look at the pictures.
- Update `README.md` (controls, content tables, the test list) to match.

## CI and releases

`.github/workflows/build.yml` builds and tests on Ubuntu for every push and pull request. It then publishes
self-contained single-file builds for win-x64/arm64, osx-x64/arm64 and linux-x64/arm64, kept as workflow artifacts.
Pushes to `main` update the `latest` pre-release; a `v*` tag makes a proper release. A release also asks the
portfolio site to rebuild.
