# CLAUDE.md — BasketballScout

## Project Overview

BasketballScout is a cross-platform mobile app for live basketball game scouting and stat tracking. It targets iOS and Android, runs fully offline (no server/hosting), and is built by a single developer (Michiel) who is an experienced .NET architect.

The app is inspired by iScore Basketball but improves on competitor weaknesses identified through user review analysis of 6 apps (iScore, GameChanger, HoopMetrics, Easy Stats, iScout, Hoopsalytics). Key differentiators: court-first scoring UX, speed-first input design, offline-first architecture, and zero hosting costs.

## Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| **Framework** | .NET MAUI (.NET 10) | Single C# codebase → iOS, Android, Windows, macOS |
| **Language** | C# | Developer's primary language |
| **UI Pattern** | MVVM | CommunityToolkit.Mvvm with source generators |
| **UI Toolkit** | CommunityToolkit.Maui | Extra controls, popups, behaviors |
| **Database** | SQLite via EF Core | Microsoft.EntityFrameworkCore.Sqlite |
| **Data Pattern** | Repository Pattern + Services | Same architecture as developer's EaseeChargeMaster project |
| **PDF Generation** | PdfSharp.Maui (MIT) | On-device PDF generation for match/season reports |
| **Import/Export** | System.Text.Json | JSON file exchange, share via native share sheet |
| **IDE** | Visual Studio 2025 (for Android) / VS 2026 | VS 2026 has known Android toolchain bugs — use VS 2025 or dotnet CLI for Android builds |
| **Source Control** | GitHub (private repo under fcbcameo) | |

### Why these choices

- **QuestPDF was considered but rejected**: Dropped MAUI/mobile support in 2024.x after switching from SkiaSharp to custom Skia layer. Not usable on iOS/Android.
- **iText 7**: Officially supports MAUI (v9.2.0+) but AGPL license requires open-sourcing the app or buying a commercial license. Fallback option if PdfSharp.Maui proves insufficient.
- **No server/hosting**: Everything runs on-device. SQLite database is a local file. Import/export via JSON files shared through email, AirDrop, WhatsApp, etc. Total annual cost: ~€124 (€99 Apple Developer + €25 Google Play one-time).

## Solution Structure

```
BasketballScout.sln
│
├── src/
│   ├── BasketballScout.Core/              ← .NET Class Library
│   │   ├── Models/
│   │   │   ├── Game.cs
│   │   │   ├── Player.cs
│   │   │   ├── Team.cs
│   │   │   ├── Season.cs
│   │   │   ├── StatEvent.cs               ← Individual stat entries (event-sourced)
│   │   │   ├── ShotLocation.cs            ← X,Y coords on court
│   │   │   └── QuarterScore.cs
│   │   ├── Enums/
│   │   │   ├── StatType.cs                ← Points2, Points3, FT, Rebound, Assist, etc.
│   │   │   ├── ShotResult.cs              ← Made, Missed
│   │   │   └── FoulType.cs               ← Personal, Technical
│   │   └── Interfaces/
│   │       ├── IGameRepository.cs
│   │       ├── IPlayerRepository.cs
│   │       ├── ISeasonRepository.cs
│   │       └── IPdfReportService.cs
│   │
│   ├── BasketballScout.Data/              ← .NET Class Library
│   │   ├── ScoutDbContext.cs               ← EF Core DbContext + SQLite
│   │   ├── Repositories/
│   │   │   ├── GameRepository.cs
│   │   │   ├── PlayerRepository.cs
│   │   │   └── SeasonRepository.cs
│   │   └── Migrations/
│   │
│   ├── BasketballScout.Services/          ← .NET Class Library
│   │   ├── GameScoringService.cs          ← Live game stat tracking logic
│   │   ├── SeasonStatsService.cs          ← Averages, aggregations
│   │   ├── PdfReportService.cs            ← Match + season PDF generation
│   │   └── ImportExportService.cs         ← JSON/CSV import & export
│   │
│   └── BasketballScout.App/              ← .NET MAUI App Project
│       ├── Views/
│       │   ├── GameScoringPortraitPage.xaml    ← Phone layout (V2)
│       │   ├── GameScoringLandscapePage.xaml   ← Tablet layout (V3)
│       │   ├── ShotChartView.xaml              ← Reusable court component
│       │   ├── TeamRosterPage.xaml
│       │   ├── SeasonOverviewPage.xaml
│       │   ├── GameReportPage.xaml
│       │   └── SettingsPage.xaml
│       ├── ViewModels/
│       │   ├── GameScoringViewModel.cs         ← Shared between portrait & landscape
│       │   ├── SeasonOverviewViewModel.cs
│       │   └── ...
│       ├── Resources/
│       │   ├── Images/court_half.svg
│       │   └── Fonts/
│       └── MauiProgram.cs                      ← DI registration + idiom routing
│
└── CLAUDE.md
```

### Critical Architecture Rule

**The Data project (BasketballScout.Data) must NOT reference the MAUI project.** If it does, EF Core CLI tooling (`dotnet ef migrations add`) breaks. Keep it as a plain .NET class library. This is the same pattern used in the developer's EaseeChargeMaster project.

## Data Model

```
Season ──1:N──► Game ──1:N──► StatEvent
  │                │               ├─ PlayerId (FK)
  │                │               ├─ StatType (enum: Points2, Points3, FT, OffRebound,
  │                │               │            DefRebound, Assist, Steal, Block,
  │                │               │            Turnover, PersonalFoul, TechnicalFoul)
  │                │               ├─ ShotResult? (enum: Made, Missed — nullable, only for shots)
  │                │               ├─ CourtX, CourtY (float — shot location as % of court)
  │                │               ├─ Quarter (int)
  │                │               ├─ GameClock (string, e.g. "7:23")
  │                │               ├─ Timestamp (DateTime)
  │                │               └─ LinkedEventId? (FK to self — links assist to the shot it assisted)
  │                │
  │                ├── HomeTeamId / AwayTeamId (FK)
  │                ├── QuarterScores (1:N → QuarterScore)
  │                └── GameDate, Location, Notes
  │
  └──1:N──► Team ──1:N──► Player
                            ├─ Name, JerseyNumber
                            ├─ Position (PG/SG/SF/PF/C)
                            └─ IsActive (bool — on roster or not)
```

### Event-Sourced Design

Each stat entry is stored as an individual `StatEvent` with a timestamp. This enables:
- Full play-by-play reconstruction
- Undo/redo (delete last event)
- Post-game editing (add/remove/modify events)
- Shot charts (aggregate CourtX/CourtY by player)
- Accurate +/- calculation (track which 5 players were on court per event)
- Season averages computed from aggregated events

## UX Design — Scoring Screen

### Core Interaction Flow

**Court-first scoring**: Select player → Tap court location → Confirm shot type & result

This is a 3-tap flow for shots:
1. Tap player (from roster panel)
2. Tap location on the half court
3. Tap 2PT/3PT × Made/Miss in confirmation popup

The app auto-suggests 2PT or 3PT based on tap position relative to the 3-point arc.

### Smart Follow-ups

- After a **made shot** → popup: "Assisted by?" showing the 4 other on-court teammates + SKIP
- After a **missed shot** → popup: "Rebound by?" showing teammates + opponent team button + SKIP
- Follow-ups are optional — one extra tap or skip

### Non-Shot Stats

Free throws, assists, steals, blocks, turnovers, rebounds (OFF/DEF), fouls (personal/technical) are tracked via a compact stat bar. Always visible, always one tap away.

### Two Layouts (same ViewModel)

#### Portrait — Phone (V2)
- Top: Scoreboard (home score | clock/quarter/undo | away score)
- Below: Player bar (active 5 for selected team)
- Center: Half court (tap to place shots)
- Bottom: Quick stat bar (FT, AST, STL, BLK, TO, REB, FOUL)
- Team switching: tap the score area to switch between home/away

#### Landscape — Tablet (V3)
- Top: Scoreboard bar (compact, horizontal)
- Left column: Home team roster (active 5 + bench)
- Center: Half court + stat bar below
- Right column: Away team roster (active 5 + bench)
- No team switching needed — both rosters always visible
- Home names left-aligned, away names right-aligned (mirrored like a scorebook)

#### Layout Selection
```csharp
// In navigation logic / MauiProgram.cs
if (DeviceInfo.Idiom == DeviceIdiom.Tablet)
    // Load GameScoringLandscapePage (V3)
else
    // Load GameScoringPortraitPage (V2)
```

Both pages bind to the same `GameScoringViewModel`. All game logic is written once.

### Additional UI Elements

- **Undo button**: Always visible in scoreboard area. Removes last StatEvent and reverses score if applicable.
- **Play-by-play log**: Overlay (not a separate tab). Shows timestamped entries with team color indicators.
- **Substitution drawer**: Slides up from bottom. Shows on-court 5 + bench. Tap on-court player → tap bench player to swap.
- **Shot chart**: Builds in real-time on the court as you score. Green ✓ for makes, red ✗ for misses.

## Features Roadmap

### Sprint 0 — Scaffolding & Data Model (Week 1–2)
- Create multi-project solution via dotnet CLI
- Define Core models
- Set up ScoutDbContext with EF Core + SQLite
- Wire up DI in MauiProgram.cs
- Basic navigation shell

### Sprint 1 — Team & Roster Management (Week 3–4)
- Create/edit teams with name, colors
- Add/edit players with name, number, position
- Import roster from JSON
- Season management

### Sprint 2 — Live Game Scoring (Week 5–8) ← CRITICAL
- Game setup: select home/away team, active 5
- Court-first scoring UI (portrait + landscape)
- All stat types
- Substitution tracking + minutes played
- Undo/redo
- Quarter management + game clock
- **TEST AT A REAL GAME after week 6**

### Sprint 3 — Stats & Season Tracking (Week 9–10)
- Box score view per game
- Season averages: PPG, RPG, APG, FG%, 3P%, FT%, +/-
- Player comparison, shot chart visualization

### Sprint 4 — PDF Reports & Import/Export (Week 11–12)
- Match report PDF: box score, shot chart, play-by-play
- Season report PDF: averages, rankings, trends
- JSON import/export + native share sheet

### Sprint 5 — Polish & App Store (Week 13–14)
- iOS/Android builds and testing
- App icons, splash screen, store assets
- Publish to App Store & Google Play

### V2 Future Ideas
- Optional cloud backup (Azure Blob Storage / OneDrive)
- Opponent scouting templates & tendency tracking
- Video timestamp bookmarks
- AI-powered game recap narratives
- Multi-device collaborative scoring (multiple scorers on same game)

## Development Environment

- **OS**: Windows 11
- **IDE**: Visual Studio 2025 (recommended for Android) / VS 2026
- **Android testing**: Android Emulator (Hyper-V accelerated) + physical Android phone via USB
- **iOS testing**: Deferred — no Mac yet. Develop/test on Android first, iOS build when ready to publish
- **XAML Hot Reload**: Enabled for rapid UI iteration on both emulator and physical devices
- **Claude Code**: Used for implementation. Reference this CLAUDE.md for project context.

## Coding Conventions

- Use repository pattern for all data access
- Use CommunityToolkit.Mvvm source generators ([ObservableProperty], [RelayCommand])
- Dependency injection via MAUI's built-in DI container (builder.Services)
- Async/await throughout — no blocking calls
- Separate ViewModels from Views — ViewModels must not reference any MAUI UI types
- Use ObservableCollection<T> for list bindings
- Name XAML pages with Page suffix, ViewModels with ViewModel suffix
- Follow .NET naming conventions (PascalCase for public, _camelCase for private fields)

## Key Files Reference

- **Interactive UX mockups** were created during design (React artifacts in claude.ai):
  - V2 (Portrait/Phone): Court-first single-screen scoring
  - V3 (Landscape/Tablet): 3-column layout (home | court | away)
  - Both mockups are fully interactive and demonstrate the complete scoring flow
