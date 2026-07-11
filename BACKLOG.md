# BasketballScout — Backlog

Post-first-device-test backlog, captured 2026-06-06 after on-device (iPad / TestFlight) testing.

Scope decisions confirmed with the product owner:
- **Shot feedback** → live placement marker while choosing made/miss.
- **Matches overview** → cleaner list within the existing **Season** (no new "Competition" entity).
- **In-match corrections** → quick corrections only (delete recent events + adjust per-player counters), not a full play-by-play editor.
- **Overtime** → standard 5:00 periods, unlimited (OT1, OT2, …).
- **Game lifecycle** → add an explicit `GameStatus` (InProgress / Finished) field; "played" is no longer inferred from event count. A game is finished only when explicitly ended.
- **Resume fidelity** → restore the **exact** clock (seconds remaining) and period (Q/OT) where you left off; persist live clock state on the game.
- **Editing finished games** → a **simple stat editor** that can adjust any recorded stat (points, FTs, fouls, assists, rebounds, steals, blocks, turnovers) — **shot location is the one thing that stays locked** (not editable).
- **Deletion** → cascade delete; **seasons require type-to-confirm** (most destructive), **games are single-confirm**.

Sizes are T-shirt (S/M/L). Suggested implementation order is at the bottom.

---

## US-1 — Fix PDF generation on iOS 🐞
**Priority:** High · **Size:** S · **Type:** Bug

**As a** scorer, **I want** the game report PDF to generate on my iPad, **so that** I can share match reports from the device I actually use.

**Acceptance criteria**
- Tapping **Share Game Report PDF** on iPad produces the PDF and opens the share sheet — no "Failed to generate PDF" error.
- Works on iOS, Android, and Windows (no regressions).
- Text/fonts render correctly in the output.

**Technical notes**
The `TypeInitializationException` in `PdfSharpCore.Utils.FontResolver` means the global font resolver fails to initialize on iOS. Register a custom `IFontResolver` backed by an **embedded TTF** (e.g. OpenSans in `Resources/Fonts`), set `GlobalFontSettings.FontResolver` once before generation, and remove any reliance on system-font lookup. Affects `PdfReportService`.

---

## US-2 — Live shot-placement marker on the court ✨
**Priority:** High · **Size:** S · **Type:** Feature

**As a** scorer, **I want** a marker at the exact spot I tapped while I choose made/miss, **so that** I can confirm (and trust) where the shot is logged.

**Acceptance criteria**
- With a player selected, tapping the court immediately shows a marker/crosshair at that point.
- The marker stays visible while the made/miss confirmation is open.
- On **cancel**, the marker disappears; on **confirm**, it becomes the persistent made/miss dot.
- Works in both portrait and landscape.

**Technical notes**
`OnCourtTapped` already computes the normalized X/Y and sets `PendingShot`. Add an overlay marker bound to `PendingShot`'s coordinates. Affects `GameScoringPage.xaml(.cs)`.

---

## US-3 — Larger in-game buttons / tap targets 🎯
**Priority:** High · **Size:** S · **Type:** UI

**As a** scorer, **I want** bigger action buttons during a live match, **so that** I can tap quickly and accurately courtside.

**Acceptance criteria**
- Quick-stat bar buttons (FT, AST, STL, BLK, TO, REB, FOUL), made/miss, and player buttons are noticeably larger with comfortable tap targets.
- No layout overflow/clipping in portrait or landscape on phone and tablet.

**Technical notes**
Sizing/spacing pass on `GameScoringPage.xaml` (and the landscape page if separate). Could be bundled with US-2 as one "scoring screen polish" PR.

---

## US-4 — Prominent general undo with feedback ↩️
**Priority:** High · **Size:** M · **Type:** Enhancement

**As a** scorer, **I want** a clear undo that tells me what it removed, **so that** I can fix mistakes confidently without guessing.

**Acceptance criteria**
- An always-visible **Undo** button on the scoring screen.
- Undoes the **last action of any type** (shot, FT, foul, rebound, assist, steal, block, turnover) — not just shots.
- Shows a transient toast/banner naming what was undone, e.g. *"Undid: #1 Wemby — Steal"*.
- Reverses the score where applicable; pressing repeatedly steps further back.

**Technical notes**
Extend existing undo (last `StatEvent`) to all types; add a CommunityToolkit.Maui Snackbar/Toast describing the removed event. Affects `GameScoringViewModel`.

---

## US-5 — In-match quick corrections screen 🛠️
**Priority:** Medium · **Size:** M · **Type:** Feature

**As a** scorer, **I want** a separate screen to correct already-registered data mid-game, **so that** I can fix errors I notice later (remove a shot, adjust fouls).

**Acceptance criteria**
- A button on the ongoing-match screen opens a **corrections** screen/drawer.
- I can **delete recent events** (e.g. remove a player's shot) and **adjust per-player counters** (e.g. fouls, points) directly — no full play-by-play log needed.
- Changes update the live score and stats immediately.
- I can return to scoring without losing game state.

**Technical notes**
Operates on the game's `StatEvent`s via the repository; "quick corrections" = recent-event delete + per-player counter adjustments. New view + `GameScoringViewModel` methods. Builds on US-4's event-removal logic.

---

## US-6 — Completed-matches overview per season 📋
**Priority:** Medium · **Size:** M · **Type:** Feature

**As a** user, **I want** a clean list of all matches played in a season, **so that** I can browse them and open any match's details.

**Acceptance criteria**
- Within a season, a list shows **every completed match** (date, teams, final score, W/L), newest first.
- Tapping a match opens its **Box Score** detail (existing page).
- Sensible empty state when no matches have been played.

**Technical notes**
Enhance the existing season games list (`SeasonStatsPage`) into a clear matches overview; wire navigation to the Box Score detail. No new data-model concept — "competition" = existing Season.

---

## US-7 — Overtime support (unlimited 5-minute periods) ⏱️
**Priority:** Medium · **Size:** M · **Type:** Feature

**As a** scorer, **I want** to add overtime periods, **so that** I can score tied games correctly.

**Acceptance criteria**
- When Q4 ends tied (or on demand), I can start **OT1**, then **OT2**, … as many as needed.
- Each OT clock starts at **5:00**; each OT's score is tracked separately like a quarter.
- Box score **and** the game PDF show OT columns.
- No cap on the number of OT periods.

**Technical notes**
`Quarter` is an int — periods 5+ = OT1+. Update clock reset (5:00), period labels, `QuarterScore` handling, and box-score/PDF columns. Affects `GameScoringViewModel`, `PdfReportService`, and the box-score view.

---

## US-8 — Quick-action confirmation feedback 💬
**Priority:** Medium · **Size:** S · **Type:** Feature

**As a** scorer, **I want** a brief on-screen confirmation of the stat I just recorded, **so that** I can trust the tap registered without looking away from the game.

**Acceptance criteria**
- After recording a free throw or any non-shot stat (AST, STL, BLK, TO, O-RB, D-RB, PF, TF), a small confirmation appears naming what was logged (e.g. *"Steal — #1 Wemby"*, *"Personal Foul — #11 Brunson"*).
- It is non-blocking (does not interrupt or capture taps), and disappears on its own after ~1–2 seconds.
- **Field goals (2PT/3PT) are excluded** — they already get a marker dot on the court, so no toast for them (avoids double feedback).
- Rapid successive taps stay readable (latest action clearly shown; queued/short toasts are acceptable).

**Technical notes**
Reuse the CommunityToolkit.Maui `Toast` already wired for undo (US-4). Emit a toast from `RecordStatAsync` / `RecordFreeThrowAsync` in `GameScoringViewModel` (or via the existing description helper `DescribeEvent`). Skip the shot-confirm path. Consider a very short duration; if queued toasts feel laggy under rapid entry, fall back to a single in-place transient label near the stat bar.

---

## US-9 — Reposition a pending shot by tapping the court 🎯
**Priority:** Medium · **Size:** S · **Type:** Feature

**As a** scorer, **I want** to move an in-progress shot by tapping a new spot on the court, **so that** I can correct the location without closing the made/miss menu first.

**Acceptance criteria**
- With the made/miss confirmation open (a pending shot placed), tapping anywhere else on the court **moves** the pending shot to the new point.
- The live marker jumps to the new spot and the suggested **2PT/3PT** zone re-evaluates for the new location.
- The confirmation menu stays open throughout; only confirming (made/miss) or the explicit cancel (✕) dismisses it.
- Tapping the menu's own buttons still confirms as today (not treated as a reposition).

**Technical notes**
Builds on US-2's live marker. In `GameScoringPage.xaml.cs`, `OnCourtTapped` currently returns early when `PendingShot is not null`; allow re-taps to update `PendingShot` instead (keep the overlay tappable while a shot is pending — but not while a follow-up is open). Re-run the 3PT detection and refresh the marker/zone label. The confirm popup is anchored bottom-center, so it won't intercept court taps.

---

## US-10 — Exit and resume an in-progress game ⏸️
**Priority:** High · **Size:** L · **Type:** Feature

**As a** scorer, **I want** to leave an ongoing game and come back to it exactly where I left off, **so that** I can handle interruptions (or close the app) without losing my place or my clock.

**Acceptance criteria**
- I can leave a live game (back-navigate / close) at any time without being forced to finish it; nothing is lost.
- The game appears as **In Progress** in the season's match list, visually distinct from finished games, and is offered as **Resume** rather than a fresh setup.
- Resuming restores: the **exact game clock** (seconds remaining), the **current period** (Q1–Q4 or OT1+), the **score**, **per-player fouls**, the **on-court five** for both teams, and all shot dots.
- A clock that was running when I left resumes **paused** (I tap to restart it) — it never keeps ticking while I'm away.
- An explicit **Finish Game** action marks the game **Finished**; finished games no longer appear as resumable and move to the completed-matches overview (US-6).
- Starting a brand-new game for a matchup that already has an in-progress game does not silently overwrite it.

**Technical notes**
Add a `GameStatus` enum (`InProgress`, `Finished`) to `Game` + EF migration; default new games to `InProgress`. The on-court five is already reconstructable from `SubIn`/`SubOut` events, but the live clock + current period are **in-memory only** in `GameScoringViewModel` (`_clockSeconds`, `GameClock`, `Quarter`, `IsClockRunning`) — persist these on `Game` (e.g. `ClockSecondsRemaining`, `CurrentPeriod`) and save on exit / periodically. On load, hydrate the VM from the persisted clock/period instead of resetting to `10:00 / Q1`. Wire a **Finish Game** command that sets `Status = Finished`. Update US-6's matches list to key off `Status` (not "has events") and surface In-Progress rows with a Resume affordance. The "played" inference in `GetSeasonGameSummariesAsync` should switch to `Status == Finished`.

---

## US-11 — Edit recorded stats of a finished game ✏️
**Priority:** High · **Size:** M · **Type:** Feature

**As a** scorer, **I want** to edit the stats of a finished game, **so that** I can fix mistakes I notice after the final buzzer.

**Acceptance criteria**
- From a finished match (matches overview / box score), I can open a **stat editor** for that game.
- I can **add, remove, and adjust any recorded stat**: 2PT/3PT makes & misses, free throws, assists, steals, blocks, turnovers, offensive/defensive rebounds, and personal/technical fouls — attributed to the correct player.
- The **only thing I cannot edit is a shot's location** on the court; existing shot dots keep their saved X/Y. (A shot's made/miss result and which player took it may still be corrected.)
- Edits immediately and correctly update the **box score, final score, season averages, and the game PDF**.
- I can cancel out of the editor without applying changes, and confirm to save.
- Editing does **not** reopen the game as in-progress — the game stays **Finished**.

**Technical notes**
A dedicated, simple editor (not the live court UI) operating on the game's `StatEvent`s via the repository — reuse the reversal/adjust logic from US-5's corrections drawer (`ApplyReversal`, per-player counter add/remove) but scoped to the full event list of a finished game rather than just recent events. Present stats grouped by player (box-score-like rows) with +/- and delete controls, plus an "add stat" path. Keep shot `CourtX/CourtY` read-only — allow toggling `ShotResult` and re-attribution, but never expose location editing. After save, recomputation flows through existing aggregation (`GameStatsService`) so box score / season stats / PDF stay consistent. Builds on US-4/US-5 event manipulation.

---

## US-12 — Delete games and seasons (with confirmation) 🗑️
**Priority:** Medium · **Size:** M · **Type:** Feature

**As a** user, **I want** to delete games and whole seasons, **so that** I can remove test data and mistakes — but never by accident.

**Acceptance criteria**
- I can delete a single **game** from the matches overview; it requires a **confirmation dialog** naming the match before anything is removed.
- I can delete a **season**; because it's the most destructive action, it requires **type-to-confirm** (typing the season's name, or an equivalent stronger second step) — a single tap is not enough.
- Deleting a season **cascades**: its games, all their `StatEvent`s/`QuarterScore`s, and the season's teams & players are removed.
- Deleting a game removes that game and all its `StatEvent`s/`QuarterScore`s only (season, teams, players untouched).
- After deletion the relevant lists refresh and the item is gone; nothing is left orphaned in the database.
- Cancelling either confirmation leaves all data unchanged.

**Technical notes**
Add delete commands on the appropriate ViewModels (matches overview / season list) and repository methods. Use cascade deletes via EF Core relationship configuration (or explicit child removal in the repos) for `Game → StatEvent/QuarterScore` and `Season → Game/Team/Player`. Verify the EF model's delete behavior (`OnDelete(DeleteBehavior.Cascade)`) so SQLite removes children; add a migration if FK behavior changes. Game delete = a standard `DisplayAlert` confirm; season delete = a prompt requiring the typed season name (e.g. `DisplayPromptAsync`) before enabling the destructive action. Guard against deleting an **in-progress** game without an extra nudge (optional).

---

## US-13 — Tell home from away in the rebound prompt 🎨
**Priority:** High · **Size:** S · **Type:** UI

**As a** scorer, **I want** the "who rebounded?" prompt to clearly separate the two teams, **so that** I don't accidentally credit the rebound to the wrong team's #11.

**Acceptance criteria**
- When the rebound (or any both-teams follow-up) prompt opens, each player chip is **tinted with that player's team color**.
- The two teams are **visually separated** by a labeled divider (e.g. a "HOME" / "AWAY" header or a clear gap between the two groups) — I can tell at a glance which side a number belongs to.
- The chosen player is still attributed correctly: a rebounder on the shooting team records an **offensive** rebound, one on the other team a **defensive** rebound (unchanged behaviour).
- Works in both portrait and landscape; chip tap targets stay comfortably large (no shrinkage to fit the divider).

**Technical notes**
Today `SetFollowUp` flattens `HomeOnCourt` + `AwayOnCourt` into a single `FollowUpCandidates` list of jersey-number buttons with no team distinction (`GameScoringViewModel.cs`). Split that into two collections (e.g. `HomeFollowUpCandidates` / `AwayFollowUpCandidates`) — or wrap each candidate so it carries its team color + an `IsHome` flag — and render them as two labeled, divided groups in `GameScoringPage.xaml`, binding each chip's background to the team color. The OFF/DEF attribution logic already keys off which roster the player is on and needs no change. Pure presentation — no data-model or schema impact.

---

## US-14 — Import & export a single game 📤
**Priority:** Medium · **Size:** M · **Type:** Feature

**As a** scorer, **I want** to export one game from a season and import it elsewhere, **so that** I can back it up or hand it to another scorer without moving the whole season.

**Acceptance criteria**
- From a match (matches overview / box score) I can **export** it to a JSON file and send it via the native share sheet.
- The exported file is **self-contained**: it embeds the game, both teams, their full rosters, every `StatEvent` (including shot locations, assist/rebound links, fouls) and `QuarterScore`s — enough to fully reconstruct the game on a device that has never seen it.
- I can **import** such a file into a chosen season; the app recreates (or matches) the teams and players and adds the game with all its stats intact.
- The imported game's score, box score, shot chart, and PDF match the original exactly.
- **New vs existing players/teams is handled explicitly and predictably** — importing a game must never throw because a player or team "already exists" or is "missing". The rule is decided up front (see notes) and applied consistently, with the import telling me what it did (e.g. "matched 5 existing players, created 7 new"). No duplicate-player explosion on re-import, and no orphaned stat events pointing at a player that wasn't created.
- Importing does **not** silently clobber existing data — a re-import of the same game is either skipped or clearly added as a separate copy (no half-merged state).
- Import validates the file and fails gracefully on a malformed/incompatible file with a clear message.

**Technical notes**
First real use of the planned Sprint-4 `ImportExportService` + `System.Text.Json`. Define a versioned DTO graph (game → teams → players → stat events / quarter scores) so the schema can evolve. **Key subtlety:** primary keys can't be trusted across devices — on import, generate new `Team`/`Player`/`Game`/`StatEvent` ids and **remap every foreign key**, including `StatEvent.PlayerId`, `HomeTeamId`/`AwayTeamId`, and the self-referential `StatEvent.LinkedEventId` (assist→shot, rebound→miss) which must point at the newly-assigned event ids, not the originals. Let the user pick a target season at import time (reuse the season-picker pattern). Export via `Share`/`Launcher` with a `ReadOnlyFile`, mirroring the existing PDF share flow in `SeasonStatsViewModel`. Consider a stable per-game GUID stamped at creation to support the "skip duplicate vs add copy" decision.

**New-vs-existing player/team rule (must be nailed down before coding to avoid import exceptions):** decide and document a single deterministic matching key, then apply it to *every* player and team in the bundle so there's never an unhandled case. Recommended approach — match a **team** within the target season by name (case-insensitive); if found, reuse it, else create it. Match a **player** *within that resolved team* — **by name**, not jersey number, because numbers are not a reliable identity (see below); if found, reuse and remap their stat events to the existing player id, else create the player. Embed a stable per-entity export GUID so future exports can match precisely even if names were edited. The importer builds an old-id → resolved-id map for teams and players first, then writes the game and stat events against the mapped ids — guaranteeing no event references an uncreated player.

> **Note on jersey numbers (answers a product question):** today `Player.JerseyNumber` is a fixed attribute of the `Player` (one team, one number) — a player has the *same* number in every game, so matching imported players by number is unsafe (two players could share a number across the import boundary, or a number could have been reused). Match by name instead. If we ever want **per-game jersey numbers**, that's a separate schema change (move the number onto a per-game roster/lineup record, e.g. a `GamePlayer` join, rather than on `Player`) — call it out as a future story if the need arises; it would also change how box scores/PDFs label players per game.

---

## US-15 — Shot chart shows only the selected player 🎯
**Priority:** Medium · **Size:** S · **Type:** Feature

**As a** scorer, **I want** the court to show only the selected player's makes and misses, **so that** I can read one player's shot pattern without both teams' dots cluttering the court.

**Acceptance criteria**
- With a player selected, the court shows **only that player's** shot dots (makes ✓ / misses ✗); all other dots are hidden.
- With **no player selected**, the court shows **all** dots as it does today.
- Selecting a different player switches the visible dots immediately; deselecting restores the full chart.
- Hidden dots are not deleted — they reappear when their player is selected again or selection is cleared.
- Works while live scoring and on resume (the rebuilt-from-events chart respects the same filter).

**Technical notes**
`ShotDot` currently carries only `EventId, X, Y, IsMade` — add `PlayerId` and populate it both in the live `RecordShot` path and in `RebuildFromEvents` (`GameScoringViewModel.cs`). Filter on `SelectedPlayer`: either bind each dot's `IsVisible` to "no selection OR dot.PlayerId == SelectedPlayer.Id", or keep a full backing list and re-project into the bound `ShotChartDots` collection whenever `SelectedPlayer` changes (`OnSelectedPlayerChanged`). Presentation-only; no schema impact (events already store `PlayerId`).

---

## US-16 — Explicit "leave game" button on the scoring screen 🚪
**Priority:** High · **Size:** S · **Type:** UX Bug

**As a** scorer, **I want** a visible way to leave an in-progress game without finishing it, **so that** I can step away (or check the matches list) and resume later — instead of being forced to "End" the game.

**Acceptance criteria**
- The scoring screen has a clearly labeled **Leave / Exit** control (distinct from **✓ END**) that takes me back to where I came from.
- Leaving **keeps the game In Progress** — its clock, period, score, lineup and shot dots are saved, and it shows up as **Resume** in the matches list (US-10 behaviour), *not* as Finished.
- **✓ END** keeps its current meaning (mark the game Finished); the two actions are visually distinct so I can't confuse "leave for now" with "end the game".
- Works on iPad where there is no hardware/gesture back button.
- If I leave with the clock running, it is saved **paused** (consistent with US-10) so it never keeps ticking while I'm away.

**Technical notes**
Root cause: `GameScoringPage` sets `Shell.NavBarIsVisible="False"`, so there is **no back affordance at all** — the only exit is the `FinishGameCommand` ("✓ END") button. The leave-and-resume plumbing already exists: `OnDisappearing` calls `SaveStateAsync`, and the matches list offers Resume for `InProgress` games. So this is mostly a missing **UI control**: add a "← LEAVE" button in the scoreboard bar that calls `SaveStateAsync` then `Shell.Current.GoToAsync("..")` (a new `LeaveGameCommand`, mirroring `FinishGameAsync` minus the status change). Optionally also enable the system back (Android `OnBackButtonPressed` / a Shell back button) for parity, but the on-screen button is the must-have since iPad has no hardware back. Pure UX wiring — no data-model impact.

---

# Code-review batch (2026-07-05)

Stories US-17 … US-30 come out of a full code review after the US-13…16 batch: three bug-fix stories first, then the improvements that push the app from "stat tracker" to "first-class scout app". Dutch localization was explicitly excluded by the product owner.

---

## US-17 — Fix crashes & leaks 🧯
**Priority:** Critical · **Size:** M · **Type:** Bug

**As a** user, **I want** the app to never crash or silently waste resources during normal roster and game management, **so that** I can trust it courtside.

**Acceptance criteria**
- **Deleting a player who has recorded stats no longer crashes.** The app detects the situation and either blocks with a clear message or offers "mark inactive instead" (player disappears from lineup pickers but history stays intact).
- **Leaving the scoring screen via hardware/gesture back with the clock running stops the clock timer.** No background ticking, no leaked ViewModel; on resume the clock shows the value at the moment of leaving (paused).
- The clock reaching **0:00 while running persists the state** (same as a manual pause).
- An unknown quick-stat id is **ignored** (with a debug log) instead of silently recording a Turnover.
- The game-import file picker filters to **JSON files** like every other picker in the app.
- Season export file names are **sanitized** (`MakeFileSafe`) so a season name with `/` or `:` can't fail the export.

**Technical notes**
Crash root cause: `TeamDetailViewModel.DeletePlayerAsync` → `PlayerRepository.DeleteAsync` with `StatEvent→Player` configured `DeleteBehavior.Restrict` (`ScoutDbContext`) — any player with even a lineup `SubIn` event trips an unhandled `DbUpdateException`. Check `StatEvents.AnyAsync(e => e.PlayerId == id)` first; offer `IsActive = false` as the alternative. Timer leak: `GameScoringPage.OnDisappearing` calls `SaveStateAsync` but never `StopClock()`; an enabled `System.Timers.Timer` is GC-rooted, so the abandoned VM keeps firing every second. Also save state in `OnClockTick` when it hits zero. Small fixes: `RecordStatAsync` `_ =>` fallback arm; `PickOptions.FileTypes` in `SeasonStatsViewModel.ImportGameAsync`; `MakeFileSafe` in `SeasonDetailViewModel.ExportSeasonAsync`.

---

## US-18 — Fix stat correctness (OT minutes, averages, undo desync) 📐
**Priority:** High · **Size:** M · **Type:** Bug

**As a** scorer, **I want** minutes, +/- and season averages to be exactly right — including overtime games and after corrections — **so that** the reports I share are trustworthy.

**Acceptance criteria**
- **Overtime games credit correct minutes and +/-**: no phantom +5:00 per OT period for players on court across the regulation→OT boundary. Verified with a test game that goes to OT1/OT2.
- The PDF **game-flow chart** labels periods correctly for OT games (dividers no longer assume exactly 4 quarters).
- **Season averages only aggregate Finished games** — an in-progress game no longer drags down PPG/RPG/etc. of everyone who appears in it.
- **Undo removes the matching play-by-play row**, not blindly the top one — undoing a stat that happened before a substitution leaves the SUB line in place.
- Undo picks a deterministic event when two share a timestamp (tie-break by id).
- **Deleting a made shot (stat editor or corrections drawer) handles its linked assist** — and a deleted miss its linked rebound: either removed along with it after a prompt, or the user is clearly warned. No phantom assists for baskets that no longer exist.

**Technical notes**
OT bug: `ToAbsoluteSeconds` in **both** `GameStatsService` and `PdfReportService` assumes 600s periods; OT clocks start at 5:00, leaving a 300s gap at each boundary. Make the mapping period-length-aware (regulation 600s, OT 300s — mirror `GameScoringViewModel.PeriodLengthSeconds`) and compute period offsets cumulatively. Deduplicate the two copies into one shared helper while at it. Averages: filter `game.Status == GameStatus.Finished` in `GetSeasonStatsAsync` (and decide explicitly for `GetPlayerShotChartAsync`). Undo desync: `UndoAsync` deletes the latest non-sub event but does `PlayLog.RemoveAt(0)` — find the log row matching the event instead (or rebuild the top of the log); add `.ThenByDescending(e => e.Id)`. Linked events: on delete of an event that others link to (`LinkedEventId` — DB is `SetNull` so no crash), look up dependents and prompt "also remove the linked assist?" in `GameEditViewModel` and the corrections drawer.

---

## US-19 — Import/export integrity: season parity, transactions, preview 📦
**Priority:** High · **Size:** M · **Type:** Bug / Feature

**As a** user, **I want** every import to be all-or-nothing, previewable, and lifecycle-correct, **so that** moving data between devices never leaves half-imported or mislabeled games.

**Acceptance criteria**
- **Season import produces Finished games** — imported games appear in the completed-matches list with W/L badges, not as "● RESUME" (today every imported game defaults to InProgress).
- Season export/import reaches **parity with the game bundle (US-14)**: game `Status`/clock/period fields and `LinkedEventId` assist/rebound links survive the round-trip.
- **Imports are transactional** (game bundle and season): any failure rolls back everything — no partial teams/players/games left behind.
- Before committing, an import shows a **preview/confirmation**: "This will add 1 game, create 2 teams and 14 players (3 matched). Continue?"
- **Duplicate detection**: re-importing the same game file is detected (stable per-game GUID stamped at creation/export) and offers "skip" or "import as copy".
- Existing valid export files (both formats) still import correctly (versioned DTOs — bump version, accept v1).

**Technical notes**
The season DTOs predate US-10/US-14: `ImportSeasonAsync` creates `Game` without `Status` (model default = InProgress) and `StatEventExport` lacks `LinkedEventId` — port the US-14 bundle techniques (LocalId link remapping, lifecycle fields) into the season path, or rebuild season export as a list of game bundles + season/team metadata. Wrap `ImportGameAsync`/`ImportSeasonAsync` bodies in `Database.BeginTransactionAsync()` (pattern already in `SeasonRepository.DeleteAsync`). Preview: deserialize + resolve matches first (dry run of `ResolveTeamAsync` without writes), show counts via `DisplayAlert`, then commit. Duplicate GUID: add nullable `ExportGuid` column on `Games` via the `EnsureGameColumns` additive-migration pattern; stamp at game creation; carry in both bundle formats.

---

## US-20 — Per-quarter team fouls, bonus & foul-trouble warnings 🚨
**Priority:** High · **Size:** M · **Type:** Feature

**As a** scorer, **I want** team fouls per period with a bonus indicator and player foul-trouble warnings, **so that** I see the game the way a coach reads it.

**Acceptance criteria**
- The scoreboard shows **team fouls for the current period** (resetting each quarter/OT), not a game-long total.
- At **5 team fouls** in a period, a clear **BONUS** indicator appears for the opposing team (FIBA rule).
- A player reaching **4 personal fouls** is visually flagged (e.g. amber) on their roster card; at **5 fouls** they're flagged red as fouled out and the substitution drawer nudges a replacement.
- Undo/corrections keep the per-period counts correct (foul counters derive from events, not incremented shadows).
- Resume (US-10) restores the correct per-period team-foul state.
- Box score and PDF still show game-total fouls per player (unchanged).

**Technical notes**
`HomeFouls`/`AwayFouls` in `GameScoringViewModel` are game-long running counters. Replace with derived per-period counts: `events.Count(e => foul && e.Quarter == Quarter)` recomputed on record/undo/correction/period-change (cheap — events are in memory during a game). Player flags: per-player foul counts already computed in `RefreshCorrectionsAsync` — surface them on the roster cards (`PlayerFoulRow`-style binding or highlight pass like `UpdatePlayerHighlighting`). Technical fouls count toward player foul-out per FIBA; keep that in the count.

---

## US-21 — Configurable game format (period length & count) ⚙️
**Priority:** High · **Size:** M · **Type:** Feature

**As a** scorer, **I want** to set the period length (and number of periods) per season, **so that** the app matches my league — U14 plays 8-minute quarters, FIBA seniors 10.

**Acceptance criteria**
- A season has a **game format setting**: minutes per period (default 10) and periods (default 4); OT length (default 5) optionally configurable.
- New games in that season start the clock at the configured length; **Q+ resets to the right value** for regulation vs OT.
- **Minutes / +/- math uses the configured lengths** — no hardcoded 600s anywhere.
- Existing games keep working (their format is derivable or stored per game so a mid-season format change doesn't corrupt history).
- Resume clamps the restored clock against the correct period length.

**Technical notes**
Add `PeriodLengthMinutes`, `PeriodCount`, `OvertimeLengthMinutes` to `Season` (additive `ALTER TABLE` via the `EnsureGameColumns` pattern, defaults 10/4/5). Store a **copy on `Game`** at creation so history is immune to later season edits. Replace the four hardcoded sites: `GameScoringViewModel` (`QuarterLengthSeconds`, `OvertimeLengthSeconds`, `PeriodLengthSeconds`, initial `GameClock`), `GameStatsService.ToAbsoluteSeconds`/`ParseClockSeconds` clamp, `PdfReportService` equivalents, `GameEditViewModel` "0:00" additions (unaffected). Depends on US-18's shared time-mapping helper — do US-18 first.

---

## US-22 — Linked possession flows: foul→FT, and-1, steal→turnover, block→miss 🔗
**Priority:** Medium · **Size:** M · **Type:** Feature

**As a** scorer, **I want** the app to chain the events that belong together, **so that** a foul flows into free throws and a steal auto-records the opponent's turnover — fewer taps, cleaner data.

**Acceptance criteria**
- After recording a **personal foul**, an optional follow-up asks **"fouled who?"** (opposing on-court players + SKIP); choosing a player offers a **free-throw sequence** (1, 2 or 3 attempts) recorded to that player, each linked to the foul.
- After a **made shot**, the assist prompt gains an **"+1 (fouled)"** path: records the foul on a chosen defender and flows into one linked FT for the shooter.
- After a **steal**, an optional prompt **"turnover by?"** (opposing on-court players + SKIP) records the linked turnover.
- After a **block**, the blocked player's **missed shot** can be recorded in one flow (block links to the miss).
- Every follow-up stays **optional** — SKIP everywhere, never more than one extra tap to dismiss.
- Undo of a chained sequence walks back one link at a time (newest first), consistent with today's behaviour.

**Technical notes**
The `LinkedEventId` field and the US-13 two-team follow-up prompt are the building blocks — extend `SetFollowUp`/`HandleFollowUpAsync` from two types ("assist"/"rebound") to a small state machine (enum + optional next-step). FT sequence UI: reuse the existing FT made/miss buttons in a modal strip ("FT 1 of 2"). Keep each stored event a plain `StatEvent` with `LinkedEventId` pointing at the trigger, so box score/undo/US-14 export all work unchanged. Scope carefully — this is the biggest UX change of the batch; consider shipping foul→FT first and steal/block chains as a follow-up PR.

---

## US-23 — Zone-based shot analytics & heat map 🗺️
**Priority:** High · **Size:** L · **Type:** Feature

**As a** coach, **I want** FG% by court zone with a visual heat map, **so that** I can see where a player or team actually scores from — the core scouting question.

**Acceptance criteria**
- The court is divided into standard zones (paint, left/right mid-range, top-of-key, left/right corner 3, left/right wing 3, center 3).
- Player season view and game box-score view offer a **zone chart**: per zone, makes/attempts and FG%, color-graded (cold → hot).
- Toggle between **player / team / opponent** scope, and filter by **period** within a game.
- Zone stats match the raw dot chart exactly (same underlying events; no drift).
- The zone chart exports into the **player/season PDF**.

**Technical notes**
All shots already store normalized `CourtX/CourtY` — this is pure aggregation + rendering. Add a `CourtZones` helper in `BasketballScout.Services` mapping (X, Y) → zone id (reuse the 3PT-arc math from `GameScoringPage.OnCourtTapped` — extract it into Core/Services so the live 2/3 suggestion and zone mapping can never disagree). Rendering: a `ZoneChartView` sibling of `ShotChartView` drawing tinted zone polygons + labels; PDF gets a mirrored drawing routine in `PdfReportService` (court-drawing primitives already exist there). No schema impact.

---

## US-24 — Opponent tendency scout report 🕵️
**Priority:** Medium · **Size:** L · **Type:** Feature

**As a** coach preparing for a rematch, **I want** a one-page scout sheet per opponent, **so that** I walk into the game knowing their top scorers and tendencies.

**Acceptance criteria**
- Per opponent team (within a season): **top scorers** (PPG, shooting splits), **zone preferences** (from US-23), rebounding/turnover profile, and per-game team scoring.
- A **"Scout Report" PDF** (one page) exportable from the team's page, styled like the existing reports.
- Data covers **all games against that opponent** in the season; shows game count so small samples are obvious.
- Works offline from recorded events only (no manual data entry).

**Technical notes**
Aggregation is a variant of `GetSeasonStatsAsync` filtered to games involving one team; zone data comes from US-23's `CourtZones` helper (hard dependency). New `ScoutReportService` method in `PdfReportService` or alongside it; entry point on `TeamDetailPage` ("Scout Report" button, visible when the team has finished games). Purely derived — no schema impact.

---

## US-25 — Season standings & scoring-run detection 📊
**Priority:** Medium · **Size:** M · **Type:** Feature

**As a** user, **I want** a standings table and visible scoring runs, **so that** the season page tells the story at a glance.

**Acceptance criteria**
- The season page gains a **standings table**: per team W-L, points for/against, point differential — computed from Finished games only.
- During live scoring and in the play-by-play log, an unanswered **scoring run ≥ 8-0** is flagged (e.g. "12-0 run"); the PDF game-flow chart annotates the biggest run of each half.
- Ties and forfeits don't break the table (ties shown as T).
- Standings respect the team filter UX already on the season stats page.

**Technical notes**
Standings: derive from `GetSeasonGameSummariesAsync` (already computes final scores + status) — group by team, no new queries. Run detection: single pass over scoring events ordered by the US-18 shared time helper, tracking consecutive unanswered points; expose as `List<ScoringRun>` from `GameStatsService` for the live log, and annotate `DrawGameFlowChart` in the PDF. Do after US-18 so the time mapping is OT-safe.

---

## US-26 — One-tap backup & restore 💾
**Priority:** High · **Size:** M · **Type:** Feature

**As a** user whose entire season lives on one device, **I want** a one-tap full backup and a restore path, **so that** a lost or broken iPad doesn't mean a lost season.

**Acceptance criteria**
- Settings/About offers **"Back up all data"**: produces a single file (shareable via the native share sheet) containing every season, team, player, game and stat event.
- **"Restore from backup"** imports such a file onto a fresh install, reproducing everything (verified: match lists, box scores, shot charts, PDFs identical).
- Restore onto a device **with existing data** asks explicitly: merge-as-new-seasons or cancel (never silently overwrites).
- The backup notes app/schema version and restore fails gracefully on an incompatible file.
- A reminder nudge (subtle, e.g. on the seasons page) if no backup was made in 30+ days.

**Technical notes**
Two viable routes: (a) copy the SQLite file itself (`basketballscout.db` in `FileSystem.AppDataDirectory`) — trivial export, but restore must close/reopen the connection and trust schema patching; (b) full-DB JSON bundle reusing US-14/US-19 serialization — slower but version-tolerant and merge-friendly. Recommend (b), as US-19 already brings season-bundle parity: a backup = all seasons as bundles + a manifest. Track `LastBackupDate` in `Preferences`.

---

## US-27 — Phone-portrait scoring layout 📱
**Priority:** Medium · **Size:** L · **Type:** Feature

**As a** scorer using a phone, **I want** a portrait scoring layout, **so that** I can track a game one-handed when I don't have the iPad.

**Acceptance criteria**
- On phone (or narrow portrait window), the scoring screen switches to the V2 design: scoreboard top, active-five player bar below it, half court center, quick-stat bar bottom.
- **Team switching** by tapping the score area (both rosters can't fit side-by-side).
- All flows work identically: court-first scoring, follow-ups (US-13 groups), corrections, substitutions, undo, leave/finish.
- The existing 3-column layout remains for tablet/landscape; both bind to the **same `GameScoringViewModel`** (no logic duplication).
- No regression on iPad.

**Technical notes**
This is the CLAUDE.md V2/V3 split that was never built — today's single `GameScoringPage` is the 3-column tablet shape with 140px side columns (unusable on a phone). Add `GameScoringPortraitPage.xaml` sharing the ViewModel; route by `DeviceInfo.Idiom` (and/or width via `OnSizeAllocated`) in the navigation call. The VM already exposes everything needed (`SwitchTeam`, `CurrentOnCourt`, `IsHomeSelected`). Big XAML job, near-zero VM work; extract shared templates (player card, stat bar, follow-up popup) into reusable `ContentView`s first so the two pages don't drift.

---

## US-28 — Edit or delete any event from the play-by-play log ✏️
**Priority:** Medium · **Size:** S · **Type:** Feature

**As a** scorer, **I want** to act directly on a play-log entry, **so that** I can fix a mistake I spot in the log without hunting through the corrections drawer.

**Acceptance criteria**
- Tapping/long-pressing a play-log row offers **Delete** (with the same reversal semantics as the corrections drawer) and, for shots, **toggle made/miss**.
- Works for any event in the game, not just the 25 most recent.
- Score, fouls, shot dots and the log itself update immediately; linked-event handling follows US-18's rules.
- Sub log lines are view-only (subs are corrected via the substitution drawer).

**Technical notes**
`PlayLogEntry` doesn't carry the event id — add `EventId` (populated in `AddLog` calls and `RebuildFromEvents`). Reuse `ApplyReversal` + `DeleteEventAsync` from the corrections drawer (US-5); made/miss toggle = update `ShotResult` + score/dot adjustments. Mostly wiring; the reversal logic already exists.

---

## US-29 — DbContext lifetime hardening 🧱
**Priority:** Medium · **Size:** M · **Type:** Tech debt

**As a** developer, **I want** short-lived database contexts, **so that** the app can't hit "second operation on this context" races or unbounded change-tracker growth in long sessions.

**Acceptance criteria**
- No shared app-lifetime `DbContext`: concurrent page loads (e.g. fast navigation during `OnAppearing` reloads) can no longer throw `InvalidOperationException`.
- Memory stays flat across a long session (scoring several games without restart).
- All existing repository behaviour is preserved (verified by exercising every page).

**Technical notes**
MAUI resolves scoped services from the root provider, so today's `AddDbContext` yields one effectively-singleton `ScoutDbContext` shared by every repository and ViewModel. Switch to `AddDbContextFactory<ScoutDbContext>` and have repositories create a context per operation (`await using var db = await _factory.CreateDbContextAsync()`), or make repositories transient with per-call contexts. Watch the two multi-step flows that must share one context/transaction (`SeasonRepository.DeleteAsync`, `GameRepository.DeleteAsync`, US-19 imports) — give those explicit factory-created contexts with transactions. Read paths become `AsNoTracking` for free wins. Do this **before** US-22/US-23 add more concurrent readers.

---

## US-30 — Readability & tap-target accessibility pass 👓
**Priority:** Low · **Size:** S · **Type:** UI

**As a** scorer in a noisy gym (often at a distance, often in a hurry), **I want** a text-size setting and generous contrast, **so that** the app stays readable courtside.

**Acceptance criteria**
- A simple **text size** setting (Normal / Large) scales the scoring screen's stat labels, play log and roster names.
- Contrast audit: the dim grays (`#555` on `#0a0a0a` etc.) are lifted where they fall below WCAG AA for essential info (scores, clock, fouls).
- Respects the OS dynamic-font setting where feasible (MAUI `FontAutoScalingEnabled`).
- No layout overflow in either orientation at the Large setting.
- (Localization is explicitly **out of scope**.)

**Technical notes**
Mostly a XAML sweep: introduce `DynamicResource`-based font sizes for the handful of hardcoded values on `GameScoringPage`/`SeasonStatsPage`, backed by a `Preferences`-stored setting on a new Settings section (About page already exists as a host). Verify `MinimumHeightRequest=44+` on all interactive elements (US-3 covered most).

---

## Status

- ✅ **US-1** — Fix PDF generation on iOS (PR #21, merged).
- ✅ **US-2** — Live shot-placement marker (PR #22, merged).
- ✅ **US-3** — Larger in-game buttons (PR #22, merged).
- ✅ **US-4** — Prominent general undo with feedback (PR #22, merged).
- ✅ **US-5** — In-match quick corrections (PR #25, merged).
- ✅ **US-6** — Completed-matches overview per season (PR #27, merged).
- ✅ **US-7** — Overtime support (PR #26, merged).
- ✅ **US-8** — Quick-action confirmation feedback (merged).
- ✅ **US-9** — Reposition a pending shot (merged).
- ✅ **US-10** — Exit & resume an in-progress game (PR #28, merged).
- ✅ **US-11** — Edit recorded stats of a finished game (PR #29, merged).
- ✅ **US-12** — Delete games & seasons with confirmation (PR #30, merged).
- ✅ **US-13** — Tell home from away in the rebound prompt (PR #31, merged).
- ✅ **US-14** — Import & export a single game (PR #33, merged).
- ✅ **US-15** — Shot chart shows only the selected player (PR #32, merged).
- ✅ **US-16** — Explicit "leave game" button on the scoring screen (PR #31, merged).
- 🔄 **US-17** — Fix crashes & leaks (implemented; PR open).
- 🔄 **US-18** — Fix stat correctness: OT minutes, averages, undo desync (implemented; PR open).
- 🔄 **US-19** — Import/export integrity: season parity, transactions, preview (implemented; PR open).
- 🔄 **US-20** — Per-quarter team fouls, bonus & foul-trouble warnings (implemented; PR open).
- 🔄 **US-21** — Configurable game format (implemented; PR open).
- 📋 **US-22** — Linked possession flows (planned).
- 🔄 **US-23** — Zone-based shot analytics & heat map (core implemented — `CourtZones` + aggregation + player zone heat panel; PR open. Follow-ups: box-score team/opponent zone charts, in-game period filter, PDF export).
- 📋 **US-24** — Opponent tendency scout report (planned).
- 📋 **US-25** — Season standings & scoring-run detection (planned).
- 🔄 **US-26** — One-tap backup & restore (implemented; PR open).
- 📋 **US-27** — Phone-portrait scoring layout (planned).
- 📋 **US-28** — Edit/delete any event from the play-by-play log (planned).
- 🔄 **US-29** — DbContext lifetime hardening (implemented; PR open).
- 📋 **US-30** — Readability & tap-target accessibility pass (planned).

## Suggested implementation order (remaining)

**Phase 1 — hardening (fix before the next real game):**
1. **US-17** — crashes & leaks. The player-delete crash is user-facing today; the timer leak hits every hardware-back exit. ✅ *done*
2. **US-18** — stat correctness. OT minutes, Finished-only averages, undo desync, linked-event phantoms. Also creates the shared period-time helper US-21/US-25 build on. ✅ *done*
3. **US-19** — import/export integrity. Season-import lifecycle bug is a data-corruption class; transactions + preview + duplicate GUID round it out. ✅ *done*

**Phase 2 — courtside trust:**
4. **US-20** — per-quarter team fouls, bonus & foul trouble. Highest-value scoring-screen upgrade. ✅ *done*
5. **US-21** — configurable game format. Depends on US-18's time helper. ✅ *done*
6. **US-26** — backup & restore. Depends on US-19's serialization parity. ✅ *done*

**Phase 3 — the scout differentiators:**
7. **US-29** — DbContext hardening (do before adding more concurrent readers). ✅ *done*
8. **US-23** — zone analytics (foundation for US-24).
9. **US-28** — play-log editing (small, high convenience).
10. **US-25** — standings & runs.
11. **US-24** — opponent scout report (depends on US-23).

**Phase 4 — reach:**
12. **US-22** — linked possession flows (biggest UX change; ship foul→FT first).
13. **US-27** — phone-portrait layout.
14. **US-30** — readability pass.

**Dependencies / sequencing rationale**
- US-18 before US-21/US-25: both need the OT-safe absolute-time helper it introduces.
- US-19 before US-26: backup format reuses the season-bundle parity work.
- US-23 before US-24: the scout report is built on zone aggregation.
- US-29 before US-22/US-23: those features add concurrent data readers; fix the context lifetime first.
