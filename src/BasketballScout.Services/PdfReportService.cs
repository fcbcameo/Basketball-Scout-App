using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;

namespace BasketballScout.Services;

public class PdfReportService
{
    private readonly GameStatsService _statsService;
    private readonly IGameRepository _gameRepository;
    private readonly IStatEventRepository _statEventRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ISeasonRepository _seasonRepository;

    // Colors — light theme for readable PDFs
    private static readonly XColor HeaderBg = XColor.FromArgb(232, 93, 38); // #e85d26
    private static readonly XColor AccentColor = XColor.FromArgb(232, 93, 38);
    private static readonly XColor TextPrimary = XColor.FromArgb(30, 30, 30);
    private static readonly XColor TextSecondary = XColor.FromArgb(100, 100, 100);
    private static readonly XColor TextOnHeader = XColors.White;
    private static readonly XColor LineDark = XColor.FromArgb(200, 200, 200);
    private static readonly XColor PageBg = XColors.White;
    private static readonly XColor TableHeaderBg = XColor.FromArgb(240, 240, 240);
    private static readonly XColor TableAltRowBg = XColor.FromArgb(248, 248, 248);

    // Shot marker colors (iScore-style: blue made, red X miss)
    private static readonly XColor MadeFill = XColor.FromArgb(0x55, 0x66, 0xCC, 0xFF);
    private static readonly XColor MadeStroke = XColor.FromArgb(0x22, 0x44, 0xAA);
    private static readonly XColor MissColor = XColor.FromArgb(0xCC, 0x22, 0x22);

    // Court colors
    private static readonly XColor CourtLine = XColor.FromArgb(60, 60, 60);
    private static readonly XColor CourtFaint = XColor.FromArgb(150, 150, 150);
    private static readonly XColor PaintFill = XColor.FromArgb(235, 235, 235);

    // Landscape letter: 11" × 8.5"
    private const double PageWidthInches = 11;
    private const double PageHeightInches = 8.5;
    private const double Margin = 30;

    // Fonts — resolved by EmbeddedFontResolver (embedded OpenSans) so PDF
    // generation works on iOS, where there is no system font directory.
    private static bool _fontResolverSet;
    private static readonly object _fontResolverLock = new();

    /// <summary>
    /// Registers the embedded-font resolver exactly once, before any XFont is
    /// created. Must run before any font operation or PdfSharpCore would fall
    /// back to its built-in resolver and crash on iOS.
    /// </summary>
    private static void EnsureFontResolver()
    {
        if (_fontResolverSet) return;
        lock (_fontResolverLock)
        {
            if (_fontResolverSet) return;
            GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
            _fontResolverSet = true;
        }
    }

    private static XFont? _titleFont;
    private static XFont? _subtitleFont;
    private static XFont? _sectionFont;
    private static XFont? _tableLabelFont;
    private static XFont? _headerFont;
    private static XFont? _cellFont;
    private static XFont? _cellBoldFont;
    private static XFont? _smallFont;
    private static XFont? _tinyFont;
    private static XFont? _teamHeadingFont;

    private static XFont TitleFont => _titleFont ??= new("Arial", 18, XFontStyle.Bold);
    private static XFont SubtitleFont => _subtitleFont ??= new("Arial", 11, XFontStyle.Regular);
    private static XFont SectionFont => _sectionFont ??= new("Arial", 12, XFontStyle.Bold);
    private static XFont TableLabelFont => _tableLabelFont ??= new("Arial", 9, XFontStyle.Bold);
    private static XFont HeaderFont => _headerFont ??= new("Arial", 7, XFontStyle.Bold);
    private static XFont CellFont => _cellFont ??= new("Arial", 7.5, XFontStyle.Regular);
    private static XFont CellBoldFont => _cellBoldFont ??= new("Arial", 7.5, XFontStyle.Bold);
    private static XFont SmallFont => _smallFont ??= new("Arial", 6.5, XFontStyle.Regular);
    private static XFont TinyFont => _tinyFont ??= new("Arial", 6, XFontStyle.Regular);
    private static XFont TeamHeadingFont => _teamHeadingFont ??= new("Arial", 20, XFontStyle.Bold);

    public PdfReportService(
        GameStatsService statsService,
        IGameRepository gameRepository,
        IStatEventRepository statEventRepository,
        ITeamRepository teamRepository,
        ISeasonRepository seasonRepository)
    {
        _statsService = statsService;
        _gameRepository = gameRepository;
        _statEventRepository = statEventRepository;
        _teamRepository = teamRepository;
        _seasonRepository = seasonRepository;
    }

    // ── Game Report ──────────────────────────────────────────────────────────

    public async Task<byte[]> GenerateGameReportAsync(int gameId)
    {
        EnsureFontResolver();
        var box = await _statsService.GetGameBoxScoreAsync(gameId);
        var events = await _statEventRepository.GetByGameIdAsync(gameId);
        var game = await _gameRepository.GetByIdAsync(gameId);

        var homeTeam = game is null ? null : await _teamRepository.GetByIdAsync(game.HomeTeamId);
        var awayTeam = game is null ? null : await _teamRepository.GetByIdAsync(game.AwayTeamId);
        var homePlayers = (homeTeam?.Players ?? []).OrderBy(p => p.JerseyNumber).ToList();
        var awayPlayers = (awayTeam?.Players ?? []).OrderBy(p => p.JerseyNumber).ToList();

        var homeIds = homePlayers.Select(p => p.Id).ToHashSet();
        var awayIds = awayPlayers.Select(p => p.Id).ToHashSet();

        int[] homeQ = ComputeQuarterScores(events, homeIds);
        int[] awayQ = ComputeQuarterScores(events, awayIds);

        var flow = ComputeGameFlow(events, homeIds, awayIds);

        var homeColor = XColor.FromArgb(0xE8, 0x5D, 0x26); // accent
        var awayColor = XColor.FromArgb(0x30, 0x70, 0xD0); // blue

        var doc = new PdfDocument();
        doc.Info.Title = $"Game Report - {box.HomeTeamName} vs {box.AwayTeamName}";

        var dateShort = game?.GameDate.ToString("M/d/yy") ?? "";
        var matchup = $"{dateShort} {box.AwayTeamName} at {box.HomeTeamName}";

        // Page 1: Away team
        DrawTeamPage(doc,
            teamName: box.AwayTeamName,
            matchup: matchup,
            lines: box.AwayLines,
            roster: awayPlayers,
            events: events,
            homeQuarterScores: homeQ,
            awayQuarterScores: awayQ,
            flow: flow,
            awayTeamName: box.AwayTeamName,
            homeTeamName: box.HomeTeamName,
            homeColor: homeColor,
            awayColor: awayColor);

        // Page 2: Home team
        DrawTeamPage(doc,
            teamName: box.HomeTeamName,
            matchup: matchup,
            lines: box.HomeLines,
            roster: homePlayers,
            events: events,
            homeQuarterScores: homeQ,
            awayQuarterScores: awayQ,
            flow: flow,
            awayTeamName: box.AwayTeamName,
            homeTeamName: box.HomeTeamName,
            homeColor: homeColor,
            awayColor: awayColor);

        // Page 3: Summary comparison
        DrawSummaryPage(doc, box, events, homeIds, awayIds);

        // Pages 4–5: Turnovers & Fouls per team
        DrawTurnoversFoulsPage(doc, box.HomeTeamName, homePlayers, events);
        DrawTurnoversFoulsPage(doc, box.AwayTeamName, awayPlayers, events);

        using var stream = new MemoryStream();
        doc.Save(stream, false);
        return stream.ToArray();
    }

    // ── Per-team page ─────────────────────────────────────────────────────────

    private static void DrawTeamPage(
        PdfDocument doc,
        string teamName,
        string matchup,
        IReadOnlyList<PlayerBoxLine> lines,
        IReadOnlyList<Player> roster,
        IReadOnlyList<StatEvent> events,
        int[] homeQuarterScores,
        int[] awayQuarterScores,
        List<FlowPoint> flow,
        string awayTeamName,
        string homeTeamName,
        XColor homeColor,
        XColor awayColor)
    {
        var page = AddPage(doc);
        var gfx = XGraphics.FromPdfPage(page);
        double W = page.Width.Point;
        double H = page.Height.Point;

        // Fill bg
        gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, W, H);

        // ── Top banner: team heading (left), quarter score box (center), game flow (right) ──
        double bannerY = Margin;
        gfx.DrawString(teamName, TeamHeadingFont, new XSolidBrush(TextPrimary),
            Margin, bannerY + 20);
        gfx.DrawString(matchup, SubtitleFont, new XSolidBrush(TextSecondary),
            Margin, bannerY + 38);

        // Quarter score box
        double qBoxW = 180;
        double qBoxX = Margin + 240;
        double qBoxY = bannerY;
        DrawQuarterScoreBox(gfx, qBoxX, qBoxY, qBoxW,
            awayTeamName, awayQuarterScores,
            homeTeamName, homeQuarterScores);

        // Game flow chart
        double flowX = qBoxX + qBoxW + 20;
        double flowW = W - Margin - flowX;
        DrawGameFlowChart(gfx, flowX, qBoxY, flowW, 55,
            flow, awayTeamName, homeTeamName, awayColor, homeColor);

        // ── Box score table ──
        double tableY = bannerY + 65;
        tableY = DrawGameBoxScoreTable(gfx, tableY, W, lines);

        // ── Mini shot charts grid ──
        tableY += 8;
        DrawMiniShotChartsGrid(gfx, tableY, W, H - Margin - tableY, roster, events);
    }

    // ── Quarter score box ─────────────────────────────────────────────────────

    private static void DrawQuarterScoreBox(
        XGraphics gfx, double x, double y, double width,
        string awayName, int[] awayQ,
        string homeName, int[] homeQ)
    {
        // Columns: Name, <one per period: 1..4 then OT1, OT2, …>, T
        int periods = Math.Max(awayQ.Length, homeQ.Length);
        double nameColW = width * 0.42;
        double qColW = (width - nameColW) / (periods + 1);
        double rowH = 14;
        double headerH = 14;

        static string PeriodHeader(int i) => i < 4 ? (i + 1).ToString() : $"OT{i - 3}";

        // Header row (period labels)
        gfx.DrawRectangle(new XSolidBrush(AccentColor), x, y, width, headerH);
        double cx = x + nameColW;
        for (int i = 0; i < periods; i++)
        {
            gfx.DrawString(PeriodHeader(i), HeaderFont, new XSolidBrush(TextOnHeader),
                new XRect(cx, y, qColW, headerH), XStringFormats.Center);
            cx += qColW;
        }
        gfx.DrawString("T", HeaderFont, new XSolidBrush(TextOnHeader),
            new XRect(cx, y, qColW, headerH), XStringFormats.Center);

        // Away row
        double ry = y + headerH;
        gfx.DrawRectangle(new XSolidBrush(TableHeaderBg), x, ry, width, rowH);
        gfx.DrawString(Truncate(awayName, 18), CellFont, new XSolidBrush(TextPrimary),
            new XRect(x + 4, ry, nameColW - 6, rowH), XStringFormats.CenterLeft);
        cx = x + nameColW;
        int awayTotal = 0;
        for (int i = 0; i < periods; i++)
        {
            int v = i < awayQ.Length ? awayQ[i] : 0;
            awayTotal += v;
            gfx.DrawString(v.ToString(), CellFont, new XSolidBrush(TextPrimary),
                new XRect(cx, ry, qColW, rowH), XStringFormats.Center);
            cx += qColW;
        }
        gfx.DrawString(awayTotal.ToString(), CellBoldFont, new XSolidBrush(TextPrimary),
            new XRect(cx, ry, qColW, rowH), XStringFormats.Center);

        // Home row
        ry += rowH;
        gfx.DrawRectangle(new XSolidBrush(PageBg), x, ry, width, rowH);
        gfx.DrawString(Truncate(homeName, 18), CellFont, new XSolidBrush(TextPrimary),
            new XRect(x + 4, ry, nameColW - 6, rowH), XStringFormats.CenterLeft);
        cx = x + nameColW;
        int homeTotal = 0;
        for (int i = 0; i < periods; i++)
        {
            int v = i < homeQ.Length ? homeQ[i] : 0;
            homeTotal += v;
            gfx.DrawString(v.ToString(), CellFont, new XSolidBrush(TextPrimary),
                new XRect(cx, ry, qColW, rowH), XStringFormats.Center);
            cx += qColW;
        }
        gfx.DrawString(homeTotal.ToString(), CellBoldFont, new XSolidBrush(TextPrimary),
            new XRect(cx, ry, qColW, rowH), XStringFormats.Center);

        // Vertical separators between period columns + before the total
        var grid = new XPen(LineDark, 0.4);
        cx = x + nameColW;
        for (int i = 0; i <= periods; i++)
        {
            gfx.DrawLine(grid, cx, y, cx, y + headerH + rowH * 2);
            cx += qColW;
        }

        // Outline
        gfx.DrawRectangle(new XPen(LineDark, 0.5), x, y, width, headerH + rowH * 2);
    }

    // ── Game flow line chart ──────────────────────────────────────────────────

    private static void DrawGameFlowChart(
        XGraphics gfx, double x, double y, double width, double height,
        List<FlowPoint> flow, string awayName, string homeName,
        XColor awayColor, XColor homeColor)
    {
        // Legend + title
        gfx.DrawString("Game Flow", SmallFont, new XSolidBrush(TextSecondary),
            x, y + 8);

        // Plot area
        double plotX = x + 30;
        double plotY = y + 10;
        double plotW = width - 40;
        double plotH = height - 15;

        // Frame
        gfx.DrawRectangle(new XPen(LineDark, 0.5), plotX, plotY, plotW, plotH);

        if (flow.Count == 0) return;

        // Compute max score for Y-axis
        int maxScore = 20;
        foreach (var p in flow)
            maxScore = Math.Max(maxScore, Math.Max(p.HomeScore, p.AwayScore));
        // Round up to nearest 20
        maxScore = ((maxScore / 20) + 1) * 20;

        // Y axis labels (20, 40, 60, 80)
        for (int v = 20; v <= maxScore; v += 20)
        {
            double yy = plotY + plotH - (v / (double)maxScore) * plotH;
            gfx.DrawString(v.ToString(), TinyFont, new XSolidBrush(TextSecondary),
                new XRect(x, yy - 4, 28, 8), XStringFormats.CenterRight);
            gfx.DrawLine(new XPen(XColor.FromArgb(0xEE, 0xEE, 0xEE), 0.3),
                plotX, yy, plotX + plotW, yy);
        }

        // Period vertical dividers + labels, positioned at each period boundary as a
        // fraction of the game's true length — OT-aware (labels 1–4, then OT1, OT2, …).
        int maxSec = flow[^1].AbsSec <= 0 ? GameTiming.RegulationLengthSeconds : flow[^1].AbsSec;
        int boundary = 0;
        int period = 1;
        while (boundary < maxSec)
        {
            double fx = plotX + (boundary / (double)maxSec) * plotW;
            if (period > 1)
            {
                gfx.DrawLine(new XPen(XColor.FromArgb(0xBB, 0xBB, 0xBB), 0.4)
                { DashStyle = XDashStyle.Dash },
                    fx, plotY, fx, plotY + plotH);
            }
            int periodLen = GameTiming.PeriodLengthSeconds(period);
            double sliceW = (periodLen / (double)maxSec) * plotW;
            string label = period <= 4 ? period.ToString() : $"OT{period - 4}";
            gfx.DrawString(label, TinyFont, new XSolidBrush(TextSecondary),
                new XRect(fx, plotY - 10, sliceW, 8), XStringFormats.Center);
            boundary += periodLen;
            period++;
        }

        // Draw both lines
        DrawFlowLine(gfx, plotX, plotY, plotW, plotH, maxSec, maxScore,
            flow, p => p.AwayScore, awayColor);
        DrawFlowLine(gfx, plotX, plotY, plotW, plotH, maxSec, maxScore,
            flow, p => p.HomeScore, homeColor);

        // Legend (top-left of plot)
        gfx.DrawString(awayName, TinyFont, new XSolidBrush(awayColor),
            plotX + 3, plotY + 8);
        gfx.DrawString(homeName, TinyFont, new XSolidBrush(homeColor),
            plotX + 3, plotY + 16);
    }

    private static void DrawFlowLine(
        XGraphics gfx, double plotX, double plotY, double plotW, double plotH,
        int maxSec, int maxScore,
        List<FlowPoint> flow, Func<FlowPoint, int> value, XColor color)
    {
        if (flow.Count == 0) return;
        var pen = new XPen(color, 1.2);
        XPoint? prev = null;
        foreach (var p in flow)
        {
            double fx = plotX + (maxSec <= 0 ? 0 : (p.AbsSec / (double)maxSec) * plotW);
            double fy = plotY + plotH - (value(p) / (double)maxScore) * plotH;
            var cur = new XPoint(fx, fy);
            if (prev is XPoint pp) gfx.DrawLine(pen, pp, cur);
            prev = cur;
        }
    }

    // ── Box score table (game) ────────────────────────────────────────────────

    private static double DrawGameBoxScoreTable(
        XGraphics gfx, double startY, double pageWidth, IReadOnlyList<PlayerBoxLine> lines)
    {
        // 28 columns. Widths sum must fit in (pageWidth - 2*Margin) ≈ 732pt.
        double[] cols = [
            15, 90, 16, 24, 22,       // # Name G MIN PTS
            20, 20, 28,                // FGM FGA FG%
            20, 20, 26,                // 2PM 2PA 2P%
            20, 20, 26,                // 3PM 3PA 3P%
            20, 20, 26,                // FTM FTA FT%
            24, 24, 22,                // OREB DREB REB
            22, 22, 22, 22, 22,        // AST STL BLK TO PF
            26,                        // PM
            24, 26, 24                 // A/T GmSc EFF
        ];
        string[] headers = [
            "#", "Name", "G", "MIN", "PTS",
            "FGM", "FGA", "FG%",
            "2PM", "2PA", "2P%",
            "3PM", "3PA", "3P%",
            "FTM", "FTA", "FT%",
            "OREB", "DREB", "REB",
            "AST", "STL", "BLK", "TO", "PF",
            "PM",
            "A/T", "GmSc", "EFF"
        ];

        double x = Margin;
        double tableWidth = cols.Sum();
        double y = startY;

        // Header row
        gfx.DrawRectangle(new XSolidBrush(AccentColor), x, y, tableWidth, 12);
        double cx = x;
        for (int i = 0; i < headers.Length; i++)
        {
            var fmt = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
            var rect = i <= 1
                ? new XRect(cx + 3, y, cols[i] - 3, 12)
                : new XRect(cx, y, cols[i], 12);
            gfx.DrawString(headers[i], HeaderFont, new XSolidBrush(TextOnHeader), rect, fmt);
            cx += cols[i];
        }
        y += 12;

        // Player rows + TEAM + TOTALS
        var sorted = lines.OrderBy(l => l.JerseyNumber).ToList();

        int rowIndex = 0;
        foreach (var line in sorted)
        {
            y = DrawBoxRow(gfx, x, y, cols, BoxRowValues(line),
                altBg: rowIndex % 2 == 1, isFooter: false);
            rowIndex++;
        }

        // TEAM row (no player-specific stats — we don't track team stats separately)
        y = DrawBoxRow(gfx, x, y, cols, TeamRowValues(),
            altBg: rowIndex % 2 == 1, isFooter: false, italic: true);
        rowIndex++;

        // TOTALS row
        var totals = new PlayerBoxLine { PlayerName = "TOTALS", JerseyNumber = 0 };
        foreach (var l in lines)
        {
            totals.Fg2Made += l.Fg2Made;
            totals.Fg2Attempted += l.Fg2Attempted;
            totals.Fg3Made += l.Fg3Made;
            totals.Fg3Attempted += l.Fg3Attempted;
            totals.FtMade += l.FtMade;
            totals.FtAttempted += l.FtAttempted;
            totals.OffRebounds += l.OffRebounds;
            totals.DefRebounds += l.DefRebounds;
            totals.Assists += l.Assists;
            totals.Steals += l.Steals;
            totals.Blocks += l.Blocks;
            totals.Turnovers += l.Turnovers;
            totals.PersonalFouls += l.PersonalFouls;
            totals.TechnicalFouls += l.TechnicalFouls;
            totals.SecondsOnCourt = Math.Max(totals.SecondsOnCourt, l.SecondsOnCourt);
        }
        int playedCount = lines.Count(l => l.HasStats || l.SecondsOnCourt > 0) > 0 ? 1 : 0;
        y = DrawBoxRow(gfx, x, y, cols, TotalsRowValues(totals, playedCount),
            altBg: false, isFooter: true);

        // Outline
        gfx.DrawRectangle(new XPen(LineDark, 0.5), x, startY, tableWidth, y - startY);

        return y;
    }

    private static string[] BoxRowValues(PlayerBoxLine l)
    {
        int gp = (l.HasStats || l.SecondsOnCourt > 0) ? 1 : 0;
        return [
            l.JerseyNumber.ToString(),
            l.PlayerName,
            Dash(gp),
            l.SecondsOnCourt > 0 ? (l.SecondsOnCourt / 60).ToString() : "-",
            Dash(l.Points),
            Dash(l.FgMade), Dash(l.FgAttempted), Pct(l.FgPct),
            Dash(l.Fg2Made), Dash(l.Fg2Attempted), Pct(l.Fg2Pct),
            Dash(l.Fg3Made), Dash(l.Fg3Attempted), Pct(l.Fg3Pct),
            Dash(l.FtMade), Dash(l.FtAttempted), Pct(l.FtPct),
            Dash(l.OffRebounds), Dash(l.DefRebounds), l.Rebounds.ToString(),
            Dash(l.Assists), Dash(l.Steals), Dash(l.Blocks), Dash(l.Turnovers), Dash(l.PersonalFouls),
            l.SecondsOnCourt > 0 ? (l.PlusMinus >= 0 ? $"+{l.PlusMinus}" : l.PlusMinus.ToString()) : "-",
            (l.Assists > 0 || l.Turnovers > 0) ? l.AssistToTurnover.ToString("F1") : "-",
            (l.HasStats || l.SecondsOnCourt > 0) ? l.GameScore.ToString("F1") : "-",
            (l.HasStats || l.SecondsOnCourt > 0) ? l.Efficiency.ToString() : "-"
        ];
    }

    private static string[] TeamRowValues()
    {
        var result = new string[29];
        result[0] = "";
        result[1] = "TEAM";
        for (int i = 2; i < 29; i++) result[i] = "-";
        result[19] = "0"; // REB placeholder
        return result;
    }

    private static string[] TotalsRowValues(PlayerBoxLine totals, int gp)
    {
        return [
            "",
            "TOTALS",
            gp.ToString(),
            totals.SecondsOnCourt > 0 ? (totals.SecondsOnCourt / 60).ToString() : "-",
            totals.Points.ToString(),
            totals.FgMade.ToString(), totals.FgAttempted.ToString(), Pct(totals.FgPct),
            totals.Fg2Made.ToString(), totals.Fg2Attempted.ToString(), Pct(totals.Fg2Pct),
            totals.Fg3Made.ToString(), totals.Fg3Attempted.ToString(), Pct(totals.Fg3Pct),
            totals.FtMade.ToString(), totals.FtAttempted.ToString(), Pct(totals.FtPct),
            totals.OffRebounds.ToString(), totals.DefRebounds.ToString(), totals.Rebounds.ToString(),
            totals.Assists.ToString(), totals.Steals.ToString(), totals.Blocks.ToString(),
            totals.Turnovers.ToString(), totals.PersonalFouls.ToString(),
            "-",
            (totals.Turnovers > 0 || totals.Assists > 0) ? totals.AssistToTurnover.ToString("F1") : "-",
            totals.GameScore.ToString("F1"),
            totals.Efficiency.ToString()
        ];
    }

    private static double DrawBoxRow(
        XGraphics gfx, double x, double y, double[] cols, string[] values,
        bool altBg, bool isFooter, bool italic = false)
    {
        double tableWidth = cols.Sum();
        double rowH = 11;

        if (isFooter)
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xE0, 0xE0, 0xE0)), x, y, tableWidth, rowH);
        else if (altBg)
            gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), x, y, tableWidth, rowH);

        var font = isFooter ? CellBoldFont : (italic ? CellFont : CellFont);
        var brush = new XSolidBrush(TextPrimary);

        double cx = x;
        for (int i = 0; i < values.Length && i < cols.Length; i++)
        {
            var fmt = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
            var rect = i <= 1
                ? new XRect(cx + 3, y, cols[i] - 3, rowH)
                : new XRect(cx, y, cols[i], rowH);
            string text = values[i] ?? "-";
            // Truncate name if needed
            if (i == 1 && text.Length > 24) text = text[..23] + "…";
            gfx.DrawString(text, font, brush, rect, fmt);
            cx += cols[i];
        }

        gfx.DrawLine(new XPen(LineDark, 0.3), x, y + rowH, x + tableWidth, y + rowH);
        return y + rowH;
    }

    private static string Pct(double p) => p > 0 ? (p / 100.0).ToString("F3") : "-";
    private static string Dash(int v) => v > 0 ? v.ToString() : "-";

    // ── Mini shot charts grid ─────────────────────────────────────────────────

    private static void DrawMiniShotChartsGrid(
        XGraphics gfx, double y, double pageWidth, double availableHeight,
        IReadOnlyList<Player> roster,
        IReadOnlyList<StatEvent> events)
    {
        if (roster.Count == 0 || availableHeight < 40) return;

        // Grid layout: up to 6 columns, rows as needed
        int cols = 6;
        int rows = (int)Math.Ceiling(roster.Count / (double)cols);
        double cellW = (pageWidth - Margin * 2) / cols;
        double cellH = Math.Min(110, (availableHeight - 4) / rows);
        double courtW = cellW - 8;
        double courtH = cellH - 14; // leave room for label

        int idx = 0;
        foreach (var player in roster)
        {
            int r = idx / cols;
            int c = idx % cols;
            double cellX = Margin + c * cellW;
            double cellY = y + r * cellH;

            var court = new XRect(cellX + 4, cellY + 2, courtW, courtH);
            DrawMiniCourt(gfx, court);

            // Get this player's shots
            var shots = events
                .Where(e => e.PlayerId == player.Id
                    && (e.StatType == StatType.Points2 || e.StatType == StatType.Points3)
                    && e.CourtX.HasValue && e.CourtY.HasValue && e.ShotResult.HasValue)
                .ToList();
            DrawShotMarkers(gfx, court, shots, makeRadius: 2.2, missRadius: 2.2);

            // Player name label
            gfx.DrawString($"{player.JerseyNumber} {player.Name}", TinyFont,
                new XSolidBrush(TextPrimary),
                new XRect(cellX, cellY + courtH + 2, cellW, 10),
                XStringFormats.Center);

            idx++;
        }
    }

    // ── Summary page ──────────────────────────────────────────────────────────

    private static void DrawSummaryPage(
        PdfDocument doc,
        GameBoxScore box,
        IReadOnlyList<StatEvent> events,
        HashSet<int> homeIds,
        HashSet<int> awayIds)
    {
        var page = AddPage(doc);
        var gfx = XGraphics.FromPdfPage(page);
        double W = page.Width.Point;
        double H = page.Height.Point;

        gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, W, H);

        // Title
        gfx.DrawString("Game Summary Comparison", SectionFont, new XSolidBrush(TextPrimary),
            new XRect(0, Margin, W, 18), XStringFormats.Center);

        // Comparison table (centered)
        double tableW = 280;
        double tableX = (W - tableW) / 2;
        double tableY = Margin + 30;
        tableY = DrawComparisonTable(gfx, tableX, tableY, tableW, box);

        // Two big shot charts side by side
        double chartY = tableY + 30;
        double chartMargin = 40;
        double chartW = (W - chartMargin * 3) / 2;
        double chartH = H - Margin - chartY - 20;

        var homeShots = events.Where(e =>
            homeIds.Contains(e.PlayerId)
            && (e.StatType == StatType.Points2 || e.StatType == StatType.Points3)
            && e.CourtX.HasValue && e.CourtY.HasValue && e.ShotResult.HasValue).ToList();
        var awayShots = events.Where(e =>
            awayIds.Contains(e.PlayerId)
            && (e.StatType == StatType.Points2 || e.StatType == StatType.Points3)
            && e.CourtX.HasValue && e.CourtY.HasValue && e.ShotResult.HasValue).ToList();

        // Away court on the LEFT (matches reference which shows the visiting team first)
        double awayX = chartMargin;
        double homeX = chartMargin * 2 + chartW;

        gfx.DrawString(box.AwayTeamName, TableLabelFont, new XSolidBrush(TextPrimary),
            new XRect(awayX, chartY, chartW, 14), XStringFormats.Center);
        gfx.DrawString(box.HomeTeamName, TableLabelFont, new XSolidBrush(TextPrimary),
            new XRect(homeX, chartY, chartW, 14), XStringFormats.Center);

        var awayCourt = new XRect(awayX, chartY + 16, chartW, chartH - 16);
        var homeCourt = new XRect(homeX, chartY + 16, chartW, chartH - 16);

        DrawLargeCourt(gfx, awayCourt);
        DrawShotMarkers(gfx, awayCourt, awayShots, makeRadius: 4, missRadius: 4);

        DrawLargeCourt(gfx, homeCourt);
        DrawShotMarkers(gfx, homeCourt, homeShots, makeRadius: 4, missRadius: 4);
    }

    private static double DrawComparisonTable(
        XGraphics gfx, double x, double y, double width, GameBoxScore box)
    {
        double labelW = width * 0.50;
        double valW = (width - labelW) / 2;
        double rowH = 14;

        // Header
        gfx.DrawRectangle(new XSolidBrush(AccentColor), x, y, width, rowH);
        gfx.DrawString("Statistic", HeaderFont, new XSolidBrush(TextOnHeader),
            new XRect(x + 4, y, labelW - 4, rowH), XStringFormats.CenterLeft);
        gfx.DrawString(Truncate(box.AwayTeamName, 12), HeaderFont, new XSolidBrush(TextOnHeader),
            new XRect(x + labelW, y, valW, rowH), XStringFormats.Center);
        gfx.DrawString(Truncate(box.HomeTeamName, 12), HeaderFont, new XSolidBrush(TextOnHeader),
            new XRect(x + labelW + valW, y, valW, rowH), XStringFormats.Center);
        y += rowH;

        var away = AggregateTotals(box.AwayLines);
        var home = AggregateTotals(box.HomeLines);

        var rows = new List<(string, string, string, bool)>
        {
            ("Points", away.Points.ToString(), home.Points.ToString(), false),
            ("Field Goals", $"{away.FgMade} / {away.FgAttempted}", $"{home.FgMade} / {home.FgAttempted}", true),
            ("  2 Point", $"{away.Fg2Made} / {away.Fg2Attempted}", $"{home.Fg2Made} / {home.Fg2Attempted}", false),
            ("  3 Point", $"{away.Fg3Made} / {away.Fg3Attempted}", $"{home.Fg3Made} / {home.Fg3Attempted}", true),
            ("  Free Throws", $"{away.FtMade} / {away.FtAttempted}", $"{home.FtMade} / {home.FtAttempted}", false),
            ("Assists", DashInt(away.Assists), DashInt(home.Assists), true),
            ("Rebounds", away.Rebounds.ToString(), home.Rebounds.ToString(), false),
            ("  Offensive", DashInt(away.OffRebounds), DashInt(home.OffRebounds), true),
            ("  Defensive", DashInt(away.DefRebounds), DashInt(home.DefRebounds), false),
            ("Blocks", DashInt(away.Blocks), DashInt(home.Blocks), true),
            ("Steals", DashInt(away.Steals), DashInt(home.Steals), false),
            ("Turnovers", DashInt(away.Turnovers), DashInt(home.Turnovers), true),
            ("Personal Fouls", DashInt(away.PersonalFouls), DashInt(home.PersonalFouls), false),
            ("Technical Fouls", DashInt(away.TechnicalFouls), DashInt(home.TechnicalFouls), true),
        };

        foreach (var (label, v1, v2, alt) in rows)
        {
            if (alt)
                gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), x, y, width, rowH);
            bool indented = label.StartsWith("  ");
            var labelFont = indented ? CellFont : CellBoldFont;
            gfx.DrawString(label, labelFont, new XSolidBrush(TextPrimary),
                new XRect(x + 4, y, labelW - 4, rowH), XStringFormats.CenterLeft);
            gfx.DrawString(v1, CellFont, new XSolidBrush(TextPrimary),
                new XRect(x + labelW, y, valW, rowH), XStringFormats.Center);
            gfx.DrawString(v2, CellFont, new XSolidBrush(TextPrimary),
                new XRect(x + labelW + valW, y, valW, rowH), XStringFormats.Center);
            y += rowH;
        }

        gfx.DrawRectangle(new XPen(LineDark, 0.5), x, y - (rows.Count + 1) * rowH,
            width, (rows.Count + 1) * rowH);

        return y;
    }

    private static string DashInt(int v) => v > 0 ? v.ToString() : "-";

    private class TeamTotals
    {
        public int Points, FgMade, FgAttempted, Fg2Made, Fg2Attempted, Fg3Made, Fg3Attempted;
        public int FtMade, FtAttempted, OffRebounds, DefRebounds, Rebounds;
        public int Assists, Steals, Blocks, Turnovers, PersonalFouls, TechnicalFouls;
    }

    private static TeamTotals AggregateTotals(IEnumerable<PlayerBoxLine> lines)
    {
        var t = new TeamTotals();
        foreach (var l in lines)
        {
            t.Points += l.Points;
            t.FgMade += l.FgMade;
            t.FgAttempted += l.FgAttempted;
            t.Fg2Made += l.Fg2Made;
            t.Fg2Attempted += l.Fg2Attempted;
            t.Fg3Made += l.Fg3Made;
            t.Fg3Attempted += l.Fg3Attempted;
            t.FtMade += l.FtMade;
            t.FtAttempted += l.FtAttempted;
            t.OffRebounds += l.OffRebounds;
            t.DefRebounds += l.DefRebounds;
            t.Rebounds += l.Rebounds;
            t.Assists += l.Assists;
            t.Steals += l.Steals;
            t.Blocks += l.Blocks;
            t.Turnovers += l.Turnovers;
            t.PersonalFouls += l.PersonalFouls;
            t.TechnicalFouls += l.TechnicalFouls;
        }
        return t;
    }

    // ── Turnovers & Fouls page ────────────────────────────────────────────────

    private static void DrawTurnoversFoulsPage(
        PdfDocument doc, string teamName,
        IReadOnlyList<Player> roster,
        IReadOnlyList<StatEvent> events)
    {
        var playerIds = roster.Select(p => p.Id).ToHashSet();
        var playerLookup = roster.ToDictionary(p => p.Id);

        var turnovers = events
            .Where(e => playerIds.Contains(e.PlayerId) && e.StatType == StatType.Turnover)
            .OrderBy(e => e.Quarter).ThenByDescending(e => GameTiming.ParseClockSeconds(e.GameClock, GameTiming.PeriodLengthSeconds(e.Quarter)))
            .ToList();
        var personalFouls = events
            .Where(e => playerIds.Contains(e.PlayerId) && e.StatType == StatType.PersonalFoul)
            .OrderBy(e => e.Quarter).ThenByDescending(e => GameTiming.ParseClockSeconds(e.GameClock, GameTiming.PeriodLengthSeconds(e.Quarter)))
            .ToList();
        var technicalFouls = events
            .Where(e => playerIds.Contains(e.PlayerId) && e.StatType == StatType.TechnicalFoul)
            .OrderBy(e => e.Quarter).ThenByDescending(e => GameTiming.ParseClockSeconds(e.GameClock, GameTiming.PeriodLengthSeconds(e.Quarter)))
            .ToList();
        var allFouls = personalFouls.Concat(technicalFouls)
            .OrderBy(e => e.Quarter).ThenByDescending(e => GameTiming.ParseClockSeconds(e.GameClock, GameTiming.PeriodLengthSeconds(e.Quarter)))
            .ToList();

        var page = AddPage(doc);
        var gfx = XGraphics.FromPdfPage(page);
        double W = page.Width.Point;

        gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, W, page.Height.Point);

        // Two tables side by side
        double halfW = (W - Margin * 2 - 30) / 2;
        double leftX = Margin;
        double rightX = Margin + halfW + 30;
        double y = Margin;

        // Titles
        gfx.DrawString($"{teamName} Turnovers", SectionFont, new XSolidBrush(TextPrimary),
            new XRect(leftX, y, halfW, 18), XStringFormats.Center);
        gfx.DrawString($"{teamName} Fouls", SectionFont, new XSolidBrush(TextPrimary),
            new XRect(rightX, y, halfW, 18), XStringFormats.Center);
        y += 22;

        DrawEventsTable(gfx, leftX, y, halfW,
            ["Period", "Clock", "Player", "Turnover Type"],
            turnovers.Select(e => new[]
            {
                PeriodName(e.Quarter),
                e.GameClock,
                PlayerLabel(e, playerLookup),
                "Other"
            }).ToList());

        DrawEventsTable(gfx, rightX, y, halfW,
            ["Period", "Clock", "Player", "Foul"],
            allFouls.Select(e => new[]
            {
                PeriodName(e.Quarter),
                e.GameClock,
                PlayerLabel(e, playerLookup),
                e.StatType == StatType.TechnicalFoul ? "Technical" : "Personal"
            }).ToList());
    }

    private static string PeriodName(int q) => q switch
    {
        1 => "1st Period",
        2 => "2nd Period",
        3 => "3rd Period",
        4 => "4th Period",
        _ => $"OT{q - 4}"
    };

    private static string PlayerLabel(StatEvent e, Dictionary<int, Player> lookup)
        => lookup.TryGetValue(e.PlayerId, out var p) ? $"#{p.JerseyNumber} {p.Name}" : $"#{e.PlayerId}";

    private static void DrawEventsTable(
        XGraphics gfx, double x, double y, double width,
        string[] headers, List<string[]> rows)
    {
        double[] colFracs = [0.20, 0.13, 0.37, 0.30];
        double[] cols = colFracs.Select(f => f * width).ToArray();
        double rowH = 14;

        // Header
        gfx.DrawRectangle(new XSolidBrush(AccentColor), x, y, width, rowH);
        double cx = x;
        for (int i = 0; i < headers.Length; i++)
        {
            gfx.DrawString(headers[i], HeaderFont, new XSolidBrush(TextOnHeader),
                new XRect(cx + 4, y, cols[i] - 4, rowH), XStringFormats.CenterLeft);
            cx += cols[i];
        }
        y += rowH;

        double startY = y;
        for (int r = 0; r < rows.Count; r++)
        {
            if (r % 2 == 1)
                gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), x, y, width, rowH);
            cx = x;
            for (int i = 0; i < rows[r].Length && i < cols.Length; i++)
            {
                gfx.DrawString(rows[r][i] ?? "", CellFont, new XSolidBrush(TextPrimary),
                    new XRect(cx + 4, y, cols[i] - 4, rowH), XStringFormats.CenterLeft);
                cx += cols[i];
            }
            y += rowH;
        }

        if (rows.Count == 0)
        {
            gfx.DrawString("— none —", CellFont, new XSolidBrush(TextSecondary),
                new XRect(x, y, width, rowH), XStringFormats.Center);
            y += rowH;
        }

        gfx.DrawRectangle(new XPen(LineDark, 0.5), x, startY - rowH, width, y - startY + rowH);
    }

    // ── Shared helpers: quarter scores, game flow, clock parsing ──────────────

    private static int[] ComputeQuarterScores(IReadOnlyList<StatEvent> events, HashSet<int> teamPlayerIds)
    {
        // At least 4 periods (Q1–Q4); extend for any overtime periods present so
        // both teams' arrays come out the same length.
        int periods = 4;
        foreach (var e in events)
            periods = Math.Max(periods, e.Quarter);

        int[] q = new int[periods];
        foreach (var e in events)
        {
            if (!teamPlayerIds.Contains(e.PlayerId)) continue;
            if (e.ShotResult != ShotResult.Made) continue;
            int idx = Math.Clamp(e.Quarter - 1, 0, periods - 1);
            int pts = e.StatType switch
            {
                StatType.Points2 => 2,
                StatType.Points3 => 3,
                StatType.FreeThrow => 1,
                _ => 0
            };
            q[idx] += pts;
        }
        return q;
    }

    private static List<FlowPoint> ComputeGameFlow(
        IReadOnlyList<StatEvent> events,
        HashSet<int> homeIds, HashSet<int> awayIds)
    {
        int maxPeriod = 4;
        foreach (var e in events) maxPeriod = Math.Max(maxPeriod, e.Quarter);
        int totalSeconds = GameTiming.TotalSecondsForMaxPeriod(maxPeriod);

        var ordered = events
            .Where(e => e.ShotResult == ShotResult.Made &&
                (e.StatType == StatType.Points2 || e.StatType == StatType.Points3 || e.StatType == StatType.FreeThrow))
            .Select(e => new { Event = e, AbsSec = GameTiming.ToAbsoluteSeconds(e.Quarter, e.GameClock) })
            .OrderBy(x => x.AbsSec).ThenBy(x => x.Event.Id)
            .ToList();

        var pts = new List<FlowPoint> { new(0, 0, 0) };
        int h = 0, a = 0;
        foreach (var x in ordered)
        {
            int p = x.Event.StatType switch
            {
                StatType.Points2 => 2,
                StatType.Points3 => 3,
                StatType.FreeThrow => 1,
                _ => 0
            };
            if (homeIds.Contains(x.Event.PlayerId)) h += p;
            else if (awayIds.Contains(x.Event.PlayerId)) a += p;
            pts.Add(new FlowPoint(x.AbsSec, h, a));
        }
        // End at the game's true total length (regulation, or later if it went to OT)
        // so the X-axis spans the whole game.
        if (pts.Count == 0 || pts[^1].AbsSec < totalSeconds)
            pts.Add(new FlowPoint(totalSeconds, h, a));
        return pts;
    }

    private record FlowPoint(int AbsSec, int HomeScore, int AwayScore);

    // ── Court drawing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Draw a mini half-court with basket at TOP (iScore-style).
    /// </summary>
    private static void DrawMiniCourt(XGraphics gfx, XRect r) => DrawCourt(gfx, r, isLarge: false);

    private static void DrawLargeCourt(XGraphics gfx, XRect r) => DrawCourt(gfx, r, isLarge: true);

    private static void DrawCourt(XGraphics gfx, XRect r, bool isLarge)
    {
        // Court proportions (FIBA official): 15m wide × 14m deep (half-court).
        // Basket at TOP of the drawn rectangle (iScore/scouting orientation).
        // Y grows DOWN, so distances "from baseline" map directly to y_draw.
        const double CourtWidthM = 15.0;
        const double CourtDepthM = 14.0;
        const double PaintWidthM = 4.9;
        const double PaintDepthM = 5.8;   // free throw line = 5.8m from baseline
        const double FtCircleRM = 1.8;
        const double RimDistFromBaselineM = 1.575; // basket center
        const double RimDiameterM = 0.46;
        const double BackboardDistFromBaselineM = 1.2;
        const double BackboardWidthM = 1.8;
        const double ThreePtRadiusM = 6.75;
        const double CornerThreeInsetM = 0.9;      // 0.9m from each sideline
        const double RestrictedAreaRM = 1.25;

        double x = r.X, y = r.Y, w = r.Width, h = r.Height;
        double sx = w / CourtWidthM;   // pt per metre, x
        double sy = h / CourtDepthM;   // pt per metre, y
        double lineThick = isLarge ? 1.0 : 0.55;

        var linePen = new XPen(CourtLine, lineThick);
        var thickPen = new XPen(CourtLine, lineThick * 1.8);
        var dashedPen = new XPen(CourtLine, lineThick) { DashStyle = XDashStyle.Dash };

        // Outer boundary
        gfx.DrawRectangle(linePen, x, y, w, h);

        // Key reference points (in draw coords)
        double basketX = x + w * 0.5;
        double basketY = y + RimDistFromBaselineM * sy;
        double bbY = y + BackboardDistFromBaselineM * sy;
        double bbHalfW = (BackboardWidthM / 2) * sx;

        // Paint (rectangular key) — from baseline (top) down to FT line
        double paintHalfW = (PaintWidthM / 2) * sx;
        double paintX = basketX - paintHalfW;
        double paintW = paintHalfW * 2;
        double paintY = y;
        double paintH = PaintDepthM * sy;

        gfx.DrawRectangle(new XSolidBrush(PaintFill), paintX, paintY, paintW, paintH);
        gfx.DrawRectangle(linePen, paintX, paintY, paintW, paintH);

        // Free throw line is the bottom edge of the paint (already drawn).

        // Free throw circle (radius 1.8m), centered on FT line.
        // Upper semi-circle inside paint = solid.
        // Lower semi-circle outside paint = dashed.
        double ftCX = basketX;
        double ftCY = paintY + paintH;
        double ftRx = FtCircleRM * sx;
        double ftRy = FtCircleRM * sy;
        // In PDF, Y grows down, so sin(angle) of +angle points DOWN.
        // Lower half (outside paint): angles 0 → π.
        // Upper half (inside paint): angles π → 2π.
        DrawEllipseArc(gfx, dashedPen, ftCX, ftCY, ftRx, ftRy, 0, Math.PI);
        DrawEllipseArc(gfx, linePen, ftCX, ftCY, ftRx, ftRy, Math.PI, 2 * Math.PI);

        // 3-point line: corner straights + arc
        double arcRx = ThreePtRadiusM * sx;
        double arcRy = ThreePtRadiusM * sy;
        double cornerInset = CornerThreeInsetM * sx;
        double leftCornerX = x + cornerInset;
        double rightCornerX = x + w - cornerInset;

        // Find y where the arc meets each corner line.
        // At x = leftCornerX: dx = leftCornerX - basketX (negative).
        // (dx/arcRx)² + (dy/arcRy)² = 1  ⇒  dy = arcRy · √(1 − (dx/arcRx)²)
        double dxCorner = (leftCornerX - basketX) / arcRx;  // in [-1, 1]
        double dyCorner = arcRy * Math.Sqrt(Math.Max(0, 1 - dxCorner * dxCorner));
        double cornerEndY = basketY + dyCorner;

        // Corner straight lines from baseline (top) down to where arc starts.
        gfx.DrawLine(linePen, leftCornerX, y, leftCornerX, cornerEndY);
        gfx.DrawLine(linePen, rightCornerX, y, rightCornerX, cornerEndY);

        // Arc sweeps from the right corner, through the top of the 3pt line
        // (which in draw coords is the BOTTOM/farther edge because basket is at top),
        // to the left corner.
        double startAngle = Math.Atan2(
            (cornerEndY - basketY) / arcRy,
            (rightCornerX - basketX) / arcRx);
        double endAngle = Math.PI - startAngle;
        DrawEllipseArc(gfx, linePen, basketX, basketY, arcRx, arcRy, startAngle, endAngle);

        // Backboard — thick line, behind the rim (closer to baseline).
        gfx.DrawLine(thickPen, basketX - bbHalfW, bbY, basketX + bbHalfW, bbY);

        // Rim — circle of 0.46m diameter at basket position.
        double rimRx = (RimDiameterM / 2) * sx;
        double rimRy = (RimDiameterM / 2) * sy;
        double rimR = Math.Max(rimRx, Math.Max(1.0, isLarge ? 3.0 : 1.5));
        gfx.DrawEllipse(linePen, basketX - rimR, basketY - rimR, rimR * 2, rimR * 2);

        // Tiny stem connecting backboard to rim (makes the basket read clearly).
        gfx.DrawLine(linePen, basketX, bbY, basketX, basketY - rimR);

        // Restricted area / no-charge semi-circle (1.25m radius from basket).
        // Only on large courts — omit on minis to reduce clutter.
        if (isLarge)
        {
            double raRx = RestrictedAreaRM * sx;
            double raRy = RestrictedAreaRM * sy;
            DrawEllipseArc(gfx, linePen, basketX, basketY, raRx, raRy, 0, Math.PI);
        }
    }

    /// <summary>
    /// Approximates an elliptical arc as a polyline.
    /// Angles in radians, standard math convention (0 = +X, +π/2 = +Y).
    /// Because PDF's Y grows down, +π/2 draws BELOW the centre.
    /// </summary>
    private static void DrawEllipseArc(
        XGraphics gfx, XPen pen,
        double cx, double cy, double rx, double ry,
        double startAngle, double endAngle,
        int segments = 64)
    {
        if (endAngle == startAngle || rx <= 0 || ry <= 0) return;
        double prevX = cx + rx * Math.Cos(startAngle);
        double prevY = cy + ry * Math.Sin(startAngle);
        for (int i = 1; i <= segments; i++)
        {
            double t = startAngle + (endAngle - startAngle) * i / segments;
            double curX = cx + rx * Math.Cos(t);
            double curY = cy + ry * Math.Sin(t);
            gfx.DrawLine(pen, prevX, prevY, curX, curY);
            prevX = curX;
            prevY = curY;
        }
    }

    private static void DrawShotMarkers(
        XGraphics gfx, XRect court,
        IEnumerable<StatEvent> shots,
        double makeRadius, double missRadius)
    {
        var madeFill = new XSolidBrush(MadeFill);
        var madeStroke = new XPen(MadeStroke, 0.6);
        var missPen = new XPen(MissColor, 1.2);

        foreach (var s in shots)
        {
            if (!s.CourtX.HasValue || !s.CourtY.HasValue) continue;

            // The app records CourtX,CourtY with basket at bottom (Y ≈ 0.9).
            // PDF court has basket at top, so flip Y.
            double px = court.X + Math.Clamp(s.CourtX.Value, 0f, 1f) * court.Width;
            double py = court.Y + (1 - Math.Clamp(s.CourtY.Value, 0f, 1f)) * court.Height;

            if (s.ShotResult == ShotResult.Made)
            {
                double r = makeRadius;
                gfx.DrawEllipse(madeFill, px - r, py - r, r * 2, r * 2);
                gfx.DrawEllipse(madeStroke, px - r, py - r, r * 2, r * 2);
            }
            else if (s.ShotResult == ShotResult.Missed)
            {
                double r = missRadius;
                gfx.DrawLine(missPen, px - r, py - r, px + r, py + r);
                gfx.DrawLine(missPen, px - r, py + r, px + r, py - r);
            }
        }
    }

    // ── Season Report (UNCHANGED) ────────────────────────────────────────────

    public async Task<byte[]> GenerateSeasonReportAsync(int seasonId, int? teamId = null)
    {
        EnsureFontResolver();
        var season = await _seasonRepository.GetByIdAsync(seasonId);
        var stats = await _statsService.GetSeasonStatsAsync(seasonId);
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);

        // Optional single-team filter
        List<Team> reportTeams = teams.ToList();
        List<Game> reportGames = games.ToList();
        if (teamId is int tid && tid > 0)
        {
            reportTeams = reportTeams.Where(t => t.Id == tid).ToList();
            reportGames = reportGames
                .Where(g => g.HomeTeamId == tid || g.AwayTeamId == tid)
                .ToList();
        }

        var doc = new PdfDocument();
        var singleTeamName = reportTeams.Count == 1 ? reportTeams[0].Name : null;
        doc.Info.Title = singleTeamName is not null
            ? $"Season Report - {singleTeamName} - {season?.Name ?? "Season"}"
            : $"Season Report - {season?.Name ?? "Season"}";

        var ctx = NewPage(doc);
        double pageWidth = ctx.Page.Width.Point - Margin * 2;

        // Title
        var titleText = singleTeamName is not null
            ? $"{season?.Name ?? "Season"} — {singleTeamName}"
            : season?.Name ?? "Season Report";
        ctx.Y = DrawTitle(ctx.Gfx,
            titleText,
            $"{reportGames.Count} games played",
            Margin, ctx.Y, pageWidth);
        ctx.Y += 5;

        // One set of 3 tables per team
        foreach (var team in reportTeams)
        {
            var teamStats = stats.Where(s => s.TeamName == team.Name).ToList();
            if (teamStats.Count == 0) continue;

            EnsureSpace(ctx, doc, 140);
            ctx.Y = DrawSectionHeader(ctx.Gfx, team.Name.ToUpper(), Margin, ctx.Y, pageWidth);

            DrawSeasonBasicTable(ctx, teamStats, pageWidth, doc);
            DrawSeasonShootingTable(ctx, teamStats, pageWidth, doc);
            DrawSeasonAdvancedTable(ctx, teamStats, pageWidth, doc);
            ctx.Y += 12;
        }

        // Games list
        if (reportGames.Count > 0)
        {
            EnsureSpace(ctx, doc, 80);
            ctx.Y = DrawSectionHeader(ctx.Gfx, "GAMES", Margin, ctx.Y, pageWidth);
            var teamLookup = teams.ToDictionary(t => t.Id);
            foreach (var g in reportGames.OrderByDescending(g => g.GameDate))
            {
                EnsureSpace(ctx, doc, 20);
                var home = teamLookup.GetValueOrDefault(g.HomeTeamId);
                var away = teamLookup.GetValueOrDefault(g.AwayTeamId);
                var text = $"{g.GameDate:MMM d}  —  {home?.Abbreviation ?? "?"} vs {away?.Abbreviation ?? "?"}";
                ctx.Gfx.DrawString(text, CellFont, new XSolidBrush(TextSecondary), Margin + 5, ctx.Y + 10);
                ctx.Y += 14;
            }
        }

        using var stream = new MemoryStream();
        doc.Save(stream, false);
        return stream.ToArray();
    }

    // ── Page helpers ─────────────────────────────────────────────────────────

    private class PageContext
    {
        public PdfPage Page = null!;
        public XGraphics Gfx = null!;
        public double Y;
    }

    private static PageContext NewPage(PdfDocument doc)
    {
        var ctx = new PageContext();
        AdvanceToNewPage(ctx, doc);
        return ctx;
    }

    private static PdfPage AddPage(PdfDocument doc)
    {
        var page = doc.AddPage();
        page.Width = XUnit.FromInch(PageWidthInches);
        page.Height = XUnit.FromInch(PageHeightInches);
        return page;
    }

    // Mutates ctx in-place so callers that hold the same PageContext reference
    // (e.g. helpers invoked without `ref`) see the page flip.
    private static void AdvanceToNewPage(PageContext ctx, PdfDocument doc)
    {
        var page = AddPage(doc);
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, page.Width.Point, page.Height.Point);
        ctx.Page = page;
        ctx.Gfx = gfx;
        ctx.Y = 30;
    }

    private static void EnsureSpace(PageContext ctx, PdfDocument doc, double needed)
    {
        if (ctx.Y + needed > ctx.Page.Height.Point - Margin)
        {
            AdvanceToNewPage(ctx, doc);
        }
    }

    // ── Title + section header ───────────────────────────────────────────────

    private static double DrawTitle(XGraphics gfx, string title, string subtitle,
        double x, double y, double width)
    {
        gfx.DrawRectangle(new XSolidBrush(HeaderBg), x, y, width, 50);
        gfx.DrawString(title, TitleFont, new XSolidBrush(TextOnHeader),
            new XRect(x, y + 8, width, 25), XStringFormats.Center);
        gfx.DrawString(subtitle, SubtitleFont, new XSolidBrush(XColor.FromArgb(200, 255, 230, 210)),
            new XRect(x, y + 30, width, 15), XStringFormats.Center);
        return y + 55;
    }

    private static double DrawSectionHeader(XGraphics gfx, string text, double x, double y, double width)
    {
        gfx.DrawLine(new XPen(AccentColor, 2), x, y + 16, x + width, y + 16);
        gfx.DrawString(text, SectionFont, new XSolidBrush(TextPrimary), x + 2, y + 13);
        return y + 22;
    }

    private static void DrawTableLabel(PageContext ctx, string text)
    {
        ctx.Gfx.DrawString(text, TableLabelFont, new XSolidBrush(TextSecondary),
            Margin + 2, ctx.Y + 10);
        ctx.Y += 14;
    }

    // ── Generic table drawing ────────────────────────────────────────────────

    private delegate string[] RowValues<T>(T item);

    private static void DrawTable<T>(
        PageContext ctx, PdfDocument doc,
        string label,
        string[] headers, double[] cols,
        IReadOnlyList<T> rows,
        RowValues<T> getValues,
        int highlightCol = -1)
    {
        double tableWidth = cols.Sum();
        double x = Margin;

        EnsureSpace(ctx, doc, 30 + rows.Count * 13);

        // Label
        DrawTableLabel(ctx, label);

        // Header row
        ctx.Gfx.DrawRectangle(new XSolidBrush(TableHeaderBg), x, ctx.Y, tableWidth, 14);
        double cx = x;
        for (int i = 0; i < headers.Length; i++)
        {
            var format = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
            ctx.Gfx.DrawString(headers[i], HeaderFont, new XSolidBrush(TextSecondary),
                new XRect(cx + 2, ctx.Y, cols[i] - 2, 14), format);
            cx += cols[i];
        }
        ctx.Y += 14;

        // Data rows
        int rowIndex = 0;
        foreach (var row in rows)
        {
            EnsureSpace(ctx, doc, 15);
            if (rowIndex % 2 == 1)
                ctx.Gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), x, ctx.Y, tableWidth, 13);

            var values = getValues(row);
            cx = x;
            for (int i = 0; i < values.Length && i < cols.Length; i++)
            {
                var font = i == highlightCol ? CellBoldFont : CellFont;
                var brush = i == highlightCol
                    ? new XSolidBrush(AccentColor)
                    : new XSolidBrush(TextPrimary);
                var format = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
                ctx.Gfx.DrawString(values[i], font, brush,
                    new XRect(cx + 2, ctx.Y, cols[i] - 2, 13), format);
                cx += cols[i];
            }

            ctx.Gfx.DrawLine(new XPen(LineDark, 0.5), x, ctx.Y + 13, x + tableWidth, ctx.Y + 13);
            ctx.Y += 13;
            rowIndex++;
        }

        if (rows.Count == 0)
        {
            ctx.Gfx.DrawString("No stats recorded", CellFont, new XSolidBrush(TextSecondary),
                new XRect(x, ctx.Y, tableWidth, 14), XStringFormats.Center);
            ctx.Y += 14;
        }

        ctx.Y += 6;
    }

    // ── Season tables (UNCHANGED) ─────────────────────────────────────────────

    private static void DrawSeasonBasicTable(PageContext ctx, IReadOnlyList<PlayerSeasonStats> stats,
        double pageWidth, PdfDocument doc)
    {
        double[] cols = [22, 140, 36, 36, 40, 40, 40, 40, 40, 40, 40, 40, 40];
        string[] headers = ["#", "PLAYER", "GP", "MPG", "PPG", "RPG", "APG", "SPG", "BPG", "TOV", "PF", "TF", "+/-"];

        DrawTable(ctx, doc, "BASIC", headers, cols, stats, s => new[]
        {
            s.JerseyNumber.ToString(),
            s.PlayerName,
            s.GamesPlayed.ToString(),
            s.MpgDisplay,
            s.Ppg,
            s.Rpg,
            s.Apg,
            s.Spg,
            s.Bpg,
            s.Topg,
            s.PfPg,
            s.TfPg,
            s.PlusMinusDisplay
        }, highlightCol: 4);
    }

    private static void DrawSeasonShootingTable(PageContext ctx, IReadOnlyList<PlayerSeasonStats> stats,
        double pageWidth, PdfDocument doc)
    {
        double[] cols = [22, 140, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44];
        string[] headers = ["#", "PLAYER", "FG%", "FGM/G", "FGA/G", "2P%", "2PM/G", "2PA/G", "3P%", "3PM/G", "3PA/G", "FT%"];

        DrawTable(ctx, doc, "SHOOTING (PER GAME)", headers, cols, stats, s => new[]
        {
            s.JerseyNumber.ToString(),
            s.PlayerName,
            s.FgPct.ToString("F1"),
            s.FgmPg,
            s.FgaPg,
            s.Fg2Pct.ToString("F1"),
            s.Fg2MPg,
            s.Fg2APg,
            s.Fg3Pct.ToString("F1"),
            s.Fg3MPg,
            s.Fg3APg,
            s.FtPct.ToString("F1")
        });
    }

    private static void DrawSeasonAdvancedTable(PageContext ctx, IReadOnlyList<PlayerSeasonStats> stats,
        double pageWidth, PdfDocument doc)
    {
        double[] cols = [22, 140, 46, 46, 44, 50, 44, 46, 44, 46];
        string[] headers = ["#", "PLAYER", "ORPG", "DRPG", "A/T", "eFG%", "TS%", "TSA/G", "EFF", "GmSc"];

        DrawTable(ctx, doc, "ADVANCED (PER GAME)", headers, cols, stats, s => new[]
        {
            s.JerseyNumber.ToString(),
            s.PlayerName,
            s.Orpg,
            s.Drpg,
            s.AtDisplay,
            s.EFgPct.ToString("F1"),
            s.TsPct.ToString("F1"),
            s.TsaPg,
            s.EffDisplay,
            s.GmScDisplay
        }, highlightCol: 8);
    }

    // ── Misc helpers ──────────────────────────────────────────────────────────

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..(max - 1)] + "…");
}
