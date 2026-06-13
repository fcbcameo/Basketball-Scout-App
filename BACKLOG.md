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
- 📋 **US-14** — Import & export a single game (planned).
- 🔄 **US-15** — Shot chart shows only the selected player (implemented; PR open).
- ✅ **US-16** — Explicit "leave game" button on the scoring screen (PR #31, merged).

## Suggested implementation order (remaining)

1. **US-13** — rebound-prompt team distinction. Smallest, highest-value courtside fix; pure presentation, no schema or data risk. ✅ *done*
2. **US-16** — leave-game button. Tiny UX fix that removes a real dead-end on iPad; reuses US-10's save/resume plumbing. ✅ *done*
3. **US-15** — selected-player shot chart. Presentation-only; tiny `ShotDot.PlayerId` addition, no migration. ✅ *done*
4. **US-14** — game import/export. Largest: stands up the `ImportExportService` and the FK-remapping logic; worth doing once the quick UX wins are in.

**Dependencies / sequencing rationale**
- US-13 and US-15 are independent, low-risk UI changes touching only `GameScoringViewModel` / `GameScoringPage` — ship them first to improve live scouting immediately.
- US-14 is self-contained but the heaviest: the self-contained-bundle format and the `LinkedEventId`/`PlayerId`/team FK remapping on import are the real work, and it introduces the import/export plumbing the rest of Sprint 4 (season export, CSV) will reuse.
