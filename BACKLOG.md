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
- 🔄 **US-12** — Delete games & seasons with confirmation (implemented; PR open).

## Suggested implementation order (remaining)

1. **US-10** — exit & resume in-progress game. Foundational: introduces the `GameStatus` field that US-11 and US-12 both lean on, and the persisted clock/period state.
2. **US-11** — edit finished-game stats. Builds on US-5's reversal/adjust logic and on US-10's clean Finished status.
3. **US-12** — delete games & seasons. Cleanest last: cascade rules are simpler to reason about once lifecycle status exists.

**Dependencies / sequencing rationale**
- US-10 first: the `GameStatus` enum + migration unblocks US-11 ("finished" games to edit) and US-12 (a clear In-Progress vs Finished distinction for delete guards), and it replaces the brittle "has events = played" inference behind US-6.
- US-11 reuses US-5's `ApplyReversal` + per-player counter logic; shot **location** stays locked while result/attribution remain editable.
- US-12 last: cascade-delete behavior (`Season → Game → StatEvent/QuarterScore`) is easiest to verify once the model is otherwise stable.
