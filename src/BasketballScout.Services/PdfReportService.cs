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

    // Fonts — PdfSharpCore handles font resolution via SixLabors.Fonts
    private static XFont? _titleFont;
    private static XFont? _subtitleFont;
    private static XFont? _sectionFont;
    private static XFont? _headerFont;
    private static XFont? _cellFont;
    private static XFont? _cellBoldFont;
    private static XFont? _smallFont;

    private static XFont TitleFont => _titleFont ??= new("Arial", 18, XFontStyle.Bold);
    private static XFont SubtitleFont => _subtitleFont ??= new("Arial", 11, XFontStyle.Regular);
    private static XFont SectionFont => _sectionFont ??= new("Arial", 12, XFontStyle.Bold);
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

    public async Task<byte[]> GenerateGameReportAsync(int gameId)
    {

        var boxScore = await _statsService.GetGameBoxScoreAsync(gameId);
        var events = await _statEventRepository.GetByGameIdAsync(gameId);
        var game = await _gameRepository.GetByIdAsync(gameId);

        var doc = new PdfDocument();
        doc.Info.Title = $"Game Report - {boxScore.HomeTeamName} vs {boxScore.AwayTeamName}";

        var page = doc.AddPage();
        page.Width = XUnit.FromInch(8.5);
        page.Height = XUnit.FromInch(11);
        var gfx = XGraphics.FromPdfPage(page);

        double y = 30;
        double margin = 30;
        double pageWidth = page.Width.Point - margin * 2;

        // Background
        gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, page.Width.Point, page.Height.Point);

        // Title
        y = DrawTitle(gfx, $"{boxScore.HomeTeamAbbr} {boxScore.HomeScore} - {boxScore.AwayScore} {boxScore.AwayTeamAbbr}",
            game?.GameDate.ToString("MMMM d, yyyy") ?? "", margin, y, pageWidth);
        y += 5;

        // Home team box score
        y = DrawSectionHeader(gfx, boxScore.HomeTeamName, margin, y, pageWidth);
        y = DrawBoxScoreTable(gfx, boxScore.HomeLines, margin, y, pageWidth);
        y += 10;

        // Away team box score
        y = DrawSectionHeader(gfx, boxScore.AwayTeamName, margin, y, pageWidth);
        y = DrawBoxScoreTable(gfx, boxScore.AwayLines, margin, y, pageWidth);
        y += 15;

        // Play-by-play
        if (events.Count > 0)
        {
            y = DrawSectionHeader(gfx, "PLAY-BY-PLAY", margin, y, pageWidth);
            y = DrawPlayByPlay(gfx, doc, events, boxScore, margin, y, pageWidth, page);
        }

        using var stream = new MemoryStream();
        doc.Save(stream, false);
        return stream.ToArray();
    }

    public async Task<byte[]> GenerateSeasonReportAsync(int seasonId)
    {

        var season = await _seasonRepository.GetByIdAsync(seasonId);
        var stats = await _statsService.GetSeasonStatsAsync(seasonId);
        var games = await _gameRepository.GetBySeasonIdAsync(seasonId);
        var teams = await _teamRepository.GetBySeasonIdAsync(seasonId);

        var doc = new PdfDocument();
        doc.Info.Title = $"Season Report - {season?.Name ?? "Season"}";

        var page = doc.AddPage();
        page.Width = XUnit.FromInch(8.5);
        page.Height = XUnit.FromInch(11);
        var gfx = XGraphics.FromPdfPage(page);

        double y = 30;
        double margin = 30;
        double pageWidth = page.Width.Point - margin * 2;

        // Background
        gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, page.Width.Point, page.Height.Point);

        // Title
        y = DrawTitle(gfx, season?.Name ?? "Season Report",
            $"{games.Count} games played", margin, y, pageWidth);
        y += 5;

        // Games list
        if (games.Count > 0)
        {
            var teamLookup = teams.ToDictionary(t => t.Id);
            y = DrawSectionHeader(gfx, "GAMES", margin, y, pageWidth);

            foreach (var game in games.OrderByDescending(g => g.GameDate))
            {
                if (y > page.Height.Point - 50)
                {
                    page = doc.AddPage();
                    page.Width = XUnit.FromInch(8.5);
                    page.Height = XUnit.FromInch(11);
                    gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, page.Width.Point, page.Height.Point);
                    y = 30;
                }

                var home = teamLookup.GetValueOrDefault(game.HomeTeamId);
                var away = teamLookup.GetValueOrDefault(game.AwayTeamId);
                var text = $"{game.GameDate:MMM d}  —  {home?.Abbreviation ?? "?"} vs {away?.Abbreviation ?? "?"}";
                gfx.DrawString(text, CellFont, new XSolidBrush(TextSecondary), margin + 5, y + 10);
                y += 14;
            }
            y += 10;
        }

        // Season stats table
        if (stats.Count > 0)
        {
            if (y > page.Height.Point - 120)
            {
                page = doc.AddPage();
                page.Width = XUnit.FromInch(8.5);
                page.Height = XUnit.FromInch(11);
                gfx = XGraphics.FromPdfPage(page);
                gfx.DrawRectangle(new XSolidBrush(PageBg), 0, 0, page.Width.Point, page.Height.Point);
                y = 30;
            }

            y = DrawSectionHeader(gfx, "SEASON AVERAGES", margin, y, pageWidth);
            y = DrawSeasonStatsTable(gfx, doc, stats, margin, y, pageWidth, page);
        }

        using var stream = new MemoryStream();
        doc.Save(stream, false);
        return stream.ToArray();
    }

    private static double DrawTitle(XGraphics gfx, string title, string subtitle,
        double x, double y, double width)
    {
        gfx.DrawRectangle(new XSolidBrush(HeaderBg),
            x, y, width, 50);

        gfx.DrawString(title, TitleFont, new XSolidBrush(TextOnHeader),
            new XRect(x, y + 8, width, 25), XStringFormats.Center);
        gfx.DrawString(subtitle, SubtitleFont, new XSolidBrush(XColor.FromArgb(200, 255, 230, 210)),
            new XRect(x, y + 30, width, 15), XStringFormats.Center);

        return y + 55;
    }

    private static double DrawSectionHeader(XGraphics gfx, string text, double x, double y, double width)
    {
        gfx.DrawLine(new XPen(AccentColor, 2), x, y + 16, x + width, y + 16);
        gfx.DrawString(text, SectionFont, new XSolidBrush(TextPrimary),
            x + 2, y + 13);
        return y + 22;
    }

    private static double DrawBoxScoreTable(XGraphics gfx, List<PlayerBoxLine> lines,
        double x, double y, double tableWidth)
    {
        double[] cols = [22, 90, 30, 42, 42, 35, 28, 28, 28, 28, 25, 25];
        string[] headers = ["#", "PLAYER", "PTS", "FG", "3PT", "FT", "REB", "AST", "STL", "BLK", "TO", "PF"];

        // Header row
        gfx.DrawRectangle(new XSolidBrush(TableHeaderBg), x, y, tableWidth, 14);
        double cx = x;
        for (int i = 0; i < headers.Length; i++)
        {
            var format = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
            gfx.DrawString(headers[i], HeaderFont, new XSolidBrush(TextSecondary),
                new XRect(cx + 2, y, cols[i] - 2, 14), format);
            cx += cols[i];
        }
        y += 14;

        // Player rows
        int rowIndex = 0;
        foreach (var line in lines)
        {
            // Alternating row background
            if (rowIndex % 2 == 1)
                gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), x, y, tableWidth, 13);

            string[] values =
            [
                line.JerseyNumber.ToString(),
                line.PlayerName,
                line.Points.ToString(),
                line.FgDisplay,
                line.Fg3Display,
                line.FtDisplay,
                line.Rebounds.ToString(),
                line.Assists.ToString(),
                line.Steals.ToString(),
                line.Blocks.ToString(),
                line.Turnovers.ToString(),
                line.Fouls.ToString()
            ];

            cx = x;
            for (int i = 0; i < values.Length; i++)
            {
                var font = i == 2 ? CellBoldFont : CellFont;
                var brush = i == 2 ? new XSolidBrush(AccentColor) : new XSolidBrush(TextPrimary);
                var format = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
                gfx.DrawString(values[i], font, brush,
                    new XRect(cx + 2, y, cols[i] - 2, 13), format);
                cx += cols[i];
            }

            gfx.DrawLine(new XPen(LineDark, 0.5), x, y + 13, x + tableWidth, y + 13);
            y += 13;
            rowIndex++;
        }

        if (lines.Count == 0)
        {
            gfx.DrawString("No stats recorded", CellFont, new XSolidBrush(TextSecondary),
                new XRect(x, y, tableWidth, 14), XStringFormats.Center);
            y += 14;
        }

        return y + 3;
    }

    private static double DrawPlayByPlay(XGraphics gfx, PdfDocument doc,
        IReadOnlyList<StatEvent> events, GameBoxScore boxScore,
        double margin, double y, double pageWidth, PdfPage page)
    {
        int rowIndex = 0;
        foreach (var e in events.OrderBy(ev => ev.Timestamp))
        {
            if (y > page.Height.Point - 40)
            {
                page = doc.AddPage();
                page.Width = XUnit.FromInch(8.5);
                page.Height = XUnit.FromInch(11);
                gfx = XGraphics.FromPdfPage(page);
                y = 30;
            }

            if (rowIndex % 2 == 1)
                gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), margin, y, pageWidth, 12);

            var clock = string.IsNullOrEmpty(e.GameClock) ? "" : $"Q{e.Quarter} {e.GameClock}";
            var desc = FormatStatEvent(e);

            gfx.DrawString(clock, SmallFont, new XSolidBrush(TextSecondary),
                margin + 3, y + 8);
            gfx.DrawString(desc, CellFont, new XSolidBrush(TextPrimary),
                margin + 60, y + 8);

            gfx.DrawLine(new XPen(LineDark, 0.3), margin, y + 11, margin + pageWidth, y + 11);
            y += 12;
            rowIndex++;
        }

        return y;
    }

    private static double DrawSeasonStatsTable(XGraphics gfx, PdfDocument doc,
        List<PlayerSeasonStats> stats, double margin, double y, double pageWidth, PdfPage page)
    {
        double[] cols = [22, 80, 30, 32, 32, 32, 32, 32, 38, 38, 38];
        string[] headers = ["#", "PLAYER", "GP", "PPG", "RPG", "APG", "SPG", "BPG", "FG%", "3P%", "FT%"];

        // Header row
        gfx.DrawRectangle(new XSolidBrush(TableHeaderBg), margin, y, pageWidth, 14);
        double cx = margin;
        for (int i = 0; i < headers.Length; i++)
        {
            var format = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
            gfx.DrawString(headers[i], HeaderFont, new XSolidBrush(TextSecondary),
                new XRect(cx + 2, y, cols[i] - 2, 14), format);
            cx += cols[i];
        }
        y += 14;

        int rowIndex = 0;
        foreach (var s in stats)
        {
            if (y > page.Height.Point - 40)
            {
                page = doc.AddPage();
                page.Width = XUnit.FromInch(8.5);
                page.Height = XUnit.FromInch(11);
                gfx = XGraphics.FromPdfPage(page);
                y = 30;
            }

            if (rowIndex % 2 == 1)
                gfx.DrawRectangle(new XSolidBrush(TableAltRowBg), margin, y, pageWidth, 13);

            string[] values =
            [
                s.JerseyNumber.ToString(),
                s.PlayerName,
                s.GamesPlayed.ToString(),
                s.Ppg,
                s.Rpg,
                s.Apg,
                s.Spg,
                s.Bpg,
                s.FgPct.ToString("F1"),
                s.Fg3Pct.ToString("F1"),
                s.FtPct.ToString("F1")
            ];

            cx = margin;
            for (int i = 0; i < values.Length; i++)
            {
                var font = i == 3 ? CellBoldFont : CellFont;
                var brush = i == 3 ? new XSolidBrush(AccentColor) : new XSolidBrush(TextPrimary);
                var format = i <= 1 ? XStringFormats.CenterLeft : XStringFormats.Center;
                gfx.DrawString(values[i], font, brush,
                    new XRect(cx + 2, y, cols[i] - 2, 13), format);
                cx += cols[i];
            }

            gfx.DrawLine(new XPen(LineDark, 0.5), margin, y + 13, margin + pageWidth, y + 13);
            y += 13;
            rowIndex++;
        }

        return y;
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
