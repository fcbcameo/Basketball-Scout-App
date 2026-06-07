# BasketballScout — Backlog

Post-first-device-test backlog, captured 2026-06-06 after on-device (iPad / TestFlight) testing.

Scope decisions confirmed with the product owner:
- **Shot feedback** → live placement marker while choosing made/miss.
- **Matches overview** → cleaner list within the existing **Season** (no new "Competition" entity).
- **In-match corrections** → quick corrections only (delete recent events + adjust per-player counters), not a full play-by-play editor.
- **Overtime** → standard 5:00 periods, unlimited (OT1, OT2, …).

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

## Status

- ✅ **US-1** — Fix PDF generation on iOS (PR #21, merged).
- ✅ **US-2** — Live shot-placement marker (PR #22, merged).
- ✅ **US-3** — Larger in-game buttons (PR #22, merged).
- ✅ **US-4** — Prominent general undo with feedback (PR #22, merged).
- ⬜ **US-5, US-6, US-7, US-8, US-9** — open.

## Suggested implementation order (remaining)

1. **US-8 + US-9** — small scoring-screen follow-ups; both reuse the toast (US-4) and live marker (US-2) just shipped. Quick to batch together.
2. **US-5** — in-match quick corrections (builds on US-4's event-removal logic).
3. **US-7** — overtime (touches box score + the PDF).
4. **US-6** — matches overview.

**Dependencies / sequencing rationale**
- US-8 reuses the `Toast` + `DescribeEvent` from US-4; US-9 extends US-2's marker — both are cheap now that their foundations exist.
- US-4 before US-5: both manipulate `StatEvent`s; undo gives corrections a foundation.
