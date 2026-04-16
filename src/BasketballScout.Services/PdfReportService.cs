using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using PdfSharpCore.Drawing;
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

    // Landscape letter: 11" × 8.5"
    private const double PageWidthInches = 11;
    private const double PageHeightInches = 8.5;
    private const double Margin = 30;

    // Fonts — PdfSharpCore handles font resolution via SixLabors.Fonts
    private static XFont? _titleFont;
    private static XFont? _subtitleFont;
    private static XFont? _sectionFont;
    private static XFont? _tableLabelFont;
    private static XFont? _headerFont;
    private static XFont? _cellFont;
    private static XFont? _cellBoldFont;
    private static XFont? _smallFont;

    private static XFont TitleFont => _titleFont ??= new("Arial", 18, XFontStyle.Bold);
    private static XFont SubtitleFont => _subtitleFont ??= new("Arial", 11, XFontStyle.Regular);
    private static XFont SectionFont => _sectionFont ??= new("Arial", 12, XFontStyle.Bold);
    private static XFont TableLabelFont => _tableLabelFont ??= new("Arial", 9, XFontStyle.Bold);
    private static XFont HeaderFont => _headerFont ??= new("Arial", 7, XFontStyle.Bold);
    private static XFont CellFont => _cellFont ??= new("Arial", 8, XFontStyle.Regular);
    private static XFont CellBoldFont => _cellBoldFont ??= new("Arial", 8, XFontStyle.Bold);
    private static XFont SmallFont => _smallFont ??= new("Arial", 6.5, XFontStyle.Regular);

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
        var boxScore = await _statsService.GetGameBoxScoreAsync(gameId);
        var events = await _statEventRepository.GetByGameIdAsync(gameId);
        var game = await _gameRepository.GetByIdAsync(gameId);

        var doc = new PdfDocument();
        doc.Info.Title = $"Game Report - {boxScore.HomeTeamName} vs {boxScore.AwayTeamName}";

        var ctx = NewPage(doc);
        double pageWidth = ctx.Page.Width.Point - Margin * 2;

        // Title
        ctx.Y = DrawTitle(ctx.Gfx,
            $"{boxScore.HomeTeamAbbr} {boxScore.HomeScore} - {boxScore.AwayScore} {boxScore.AwayTeamAbbr}",
            game?.GameDate.ToString("MMMM d, yyyy") ?? "",
            Margin, ctx.Y, pageWidth);
        ctx.Y += 5;

        // Home team — 3 stacked advanced tables
        ctx.Y = DrawSectionHeader(ctx.Gfx, boxScore.HomeTeamName, Margin, ctx.Y, pageWidth);
        DrawGameBasicTable(ctx, boxScore.HomeLines, pageWidth, doc);
        DrawGameShootingTable(ctx, boxScore.HomeLines, pageWidth, doc);
        DrawGameAdvancedTable(ctx, boxScore.HomeLines, pageWidth, doc);
        ctx.Y += 10;

        // Away team — same layout
        EnsureSpace(ctx, doc, 120);
        ctx.Y = DrawSectionHeader(ctx.Gfx, boxScore.AwayTeamName, Margin, ctx.Y, pageWidth);
        DrawGameBasicTable(ctx, boxScore.AwayLines, pageWidth, doc);
        DrawGameShootingTable(ctx, boxScore.AwayLines, pageWidth, doc);
        DrawGameAdvancedTable(ctx, boxScore.AwayLines, pageWidth, doc);
        ctx.Y += 15;

        // Play-by-play
        var nonSub = events
            .Where(e => e.StatType != StatType.SubIn && e.StatType != StatType.SubOut)
            .ToList();
        if (nonSub.Count > 0)
        {
            EnsureSpace(ctx, doc, 40);
            ctx.Y = DrawSectionHeader(ctx.Gfx, "PLAY-BY-PLAY", Margin, ctx.Y, pageWidth);
            DrawPlayByPlay(ctx, doc, nonSub, pageWidth);
        }

        using var stream = new MemoryStream();
        doc.Save(stream, false);
        return stream.ToArray();
    }

    // ── Season Report ────────────────────────────────────────────────────────

    public async Task<byte[]> GenerateSeasonReportAsync(int seasonId, int? teamId = null)
    {
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

    // Mutates ctx in-place so callers that hold the same PageContext reference
    // (e.g. helpers invoked without `ref`) see the page flip.
    private static void AdvanceToNewPage(PageContext ctx, PdfDocument doc)
    {
        var page = doc.AddPage();
        page.Width = XUnit.FromInch(PageWidthInches);
        page.Height = XUnit.FromInch(PageHeightInches);
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

    // ── Game tables ──────────────────────────────────────────────────────────

    private static void DrawGameBasicTable(PageContext ctx, IReadOnlyList<PlayerBoxLine> lines,
        double pageWidth, PdfDocument doc)
    {
        double[] cols = [22, 140, 40, 40, 40, 40, 40, 40, 40, 40, 40, 40];
        string[] headers = ["#", "PLAYER", "MIN", "PTS", "REB", "AST", "STL", "BLK", "TO", "PF", "TF", "+/-"];

        DrawTable(ctx, doc, "BASIC", headers, cols, lines, line => new[]
        {
            line.JerseyNumber.ToString(),
            line.PlayerName,
            line.MinutesDisplay,
            line.Points.ToString(),
            line.Rebounds.ToString(),
            line.Assists.ToString(),
            line.Steals.ToString(),
            line.Blocks.ToString(),
            line.Turnovers.ToString(),
            line.PersonalFouls.ToString(),
            line.TechnicalFouls.ToString(),
            line.PlusMinusDisplay
        }, highlightCol: 3);
    }

    private static void DrawGameShootingTable(PageContext ctx, IReadOnlyList<PlayerBoxLine> lines,
        double pageWidth, PdfDocument doc)
    {
        double[] cols = [22, 140, 52, 44, 52, 44, 52, 44, 52, 44];
        string[] headers = ["#", "PLAYER", "FGM/A", "FG%", "2PM/A", "2P%", "3PM/A", "3P%", "FTM/A", "FT%"];

        DrawTable(ctx, doc, "SHOOTING", headers, cols, lines, line => new[]
        {
            line.JerseyNumber.ToString(),
            line.PlayerName,
            line.FgDisplay,
            line.FgPct.ToString("F1"),
            line.Fg2Display,
            line.Fg2Pct.ToString("F1"),
            line.Fg3Display,
            line.Fg3Pct.ToString("F1"),
            line.FtDisplay,
            line.FtPct.ToString("F1")
        });
    }

    private static void DrawGameAdvancedTable(PageContext ctx, IReadOnlyList<PlayerBoxLine> lines,
        double pageWidth, PdfDocument doc)
    {
        double[] cols = [22, 140, 46, 46, 44, 50, 44, 44, 40, 46];
        string[] headers = ["#", "PLAYER", "OREB", "DREB", "A/T", "eFG%", "TS%", "TSA", "EFF", "GmSc"];

        DrawTable(ctx, doc, "ADVANCED", headers, cols, lines, line => new[]
        {
            line.JerseyNumber.ToString(),
            line.PlayerName,
            line.OffRebounds.ToString(),
            line.DefRebounds.ToString(),
            line.AssistToTurnover.ToString("F2"),
            line.EFgPct.ToString("F1"),
            line.TsPct.ToString("F1"),
            line.Tsa.ToString("F1"),
            line.Efficiency.ToString(),
            line.GameScore.ToString("F1")
        }, highlightCol: 9);
    }

    // ── Season tables ────────────────────────────────────────────────────────

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

    // ── Play-by-play ─────────────────────────────────────────────────────────

    private static void DrawPlayByPlay(PageContext ctx, PdfDocument doc,
        IReadOnlyList<StatEvent> events, double pageWidth)
    {
        int rowIndex = 0;
        foreach (var e in events.OrderBy(ev => ev.Timestamp))
        {
            EnsureSpace(ctx, doc, 14);
            if (rowIndex % 2 == 1)
                ctx.Gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), Margin, ctx.Y, pageWidth, 12);

            var clock = string.IsNullOrEmpty(e.GameClock) ? "" : $"Q{e.Quarter} {e.GameClock}";
            var desc = FormatStatEvent(e);

            ctx.Gfx.DrawString(clock, SmallFont, new XSolidBrush(TextSecondary),
                Margin + 3, ctx.Y + 8);
            ctx.Gfx.DrawString(desc, CellFont, new XSolidBrush(TextPrimary),
                Margin + 60, ctx.Y + 8);

            ctx.Gfx.DrawLine(new XPen(LineDark, 0.3), Margin, ctx.Y + 11, Margin + pageWidth, ctx.Y + 11);
            ctx.Y += 12;
            rowIndex++;
        }
    }

    private static string FormatStatEvent(StatEvent e)
    {
        var player = e.Player?.Name ?? $"Player #{e.PlayerId}";
        return e.StatType switch
        {
            StatType.Points2 when e.ShotResult == ShotResult.Made => $"{player} — 2PT Made",
            StatType.Points2 => $"{player} — 2PT Missed",
            StatType.Points3 when e.ShotResult == ShotResult.Made => $"{player} — 3PT Made",
            StatType.Points3 => $"{player} — 3PT Missed",
            StatType.FreeThrow when e.ShotResult == ShotResult.Made => $"{player} — FT Made",
            StatType.FreeThrow => $"{player} — FT Missed",
            StatType.OffensiveRebound => $"{player} — Offensive Rebound",
            StatType.DefensiveRebound => $"{player} — Defensive Rebound",
            StatType.Assist => $"{player} — Assist",
            StatType.Steal => $"{player} — Steal",
            StatType.Block => $"{player} — Block",
            StatType.Turnover => $"{player} — Turnover",
            StatType.PersonalFoul => $"{player} — Personal Foul",
            StatType.TechnicalFoul => $"{player} — Technical Foul",
            _ => $"{player} — {e.StatType}"
        };
    }
}
