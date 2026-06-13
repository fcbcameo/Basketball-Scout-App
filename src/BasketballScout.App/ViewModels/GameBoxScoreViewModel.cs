using System.Collections.ObjectModel;
using BasketballScout.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(GameId), "gameId")]
public partial class GameBoxScoreViewModel : ObservableObject
{
    private readonly GameStatsService _statsService;
    private readonly PdfReportService _pdfService;
    private readonly ImportExportService _importExportService;

    [ObservableProperty]
    public partial int GameId { get; set; }

    [ObservableProperty]
    public partial string HomeTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AwayTeamName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ScoreDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GameDateDisplay { get; set; } = string.Empty;

    public ObservableCollection<PlayerBoxLine> HomeLines { get; } = [];
    public ObservableCollection<PlayerBoxLine> AwayLines { get; } = [];

    public GameBoxScoreViewModel(
        GameStatsService statsService,
        PdfReportService pdfService,
        ImportExportService importExportService)
    {
        _statsService = statsService;
        _pdfService = pdfService;
        _importExportService = importExportService;
    }

    [RelayCommand]
    private async Task EditStatsAsync()
    {
        await Shell.Current.GoToAsync($"{nameof(Views.GameEditPage)}?gameId={GameId}");
    }

    /// <summary>Re-reads the box score — called when returning from the stat editor (US-11).</summary>
    public Task ReloadAsync() => GameId > 0 ? LoadAsync(GameId) : Task.CompletedTask;

    /// <summary>US-14: export this game as a self-contained JSON bundle and offer it via
    /// the native share sheet, so it can be backed up or handed to another scorer.</summary>
    [RelayCommand]
    private async Task ExportGameAsync()
    {
        try
        {
            var json = await _importExportService.ExportGameAsync(GameId);
            var safeHome = MakeFileSafe(HomeTeamName);
            var safeAway = MakeFileSafe(AwayTeamName);
            var safeDate = GameDateDisplay.Replace(" ", "").Replace(",", "");
            var fileName = $"Game_{safeHome}_vs_{safeAway}_{safeDate}.json";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Game — {ScoreDisplay}",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to export game: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task SharePdfAsync()
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateGameReportAsync(GameId);
            var safeHome = MakeFileSafe(HomeTeamName);
            var safeAway = MakeFileSafe(AwayTeamName);
            var fileName = $"GameReport_{safeHome}_vs_{safeAway}_{GameDateDisplay.Replace(" ", "").Replace(",", "")}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, pdfBytes);

            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = $"Game Report - {ScoreDisplay}",
                File = new ReadOnlyFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to generate PDF: {ex.Message}", "OK");
        }
    }

    private static string MakeFileSafe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    partial void OnGameIdChanged(int value)
    {
        if (value > 0) _ = LoadAsync(value);
    }

    private async Task LoadAsync(int gameId)
    {
        var box = await _statsService.GetGameBoxScoreAsync(gameId);

        HomeTeamName = box.HomeTeamName;
        AwayTeamName = box.AwayTeamName;
        ScoreDisplay = $"{box.HomeTeamAbbr} {box.HomeScore} - {box.AwayScore} {box.AwayTeamAbbr}";
        GameDateDisplay = box.GameDate.ToString("MMM d, yyyy");

        HomeLines.Clear();
        foreach (var line in box.HomeLines) HomeLines.Add(line);

        AwayLines.Clear();
        foreach (var line in box.AwayLines) AwayLines.Add(line);
    }
}
