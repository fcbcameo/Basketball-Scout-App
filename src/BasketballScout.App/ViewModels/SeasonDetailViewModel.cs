using System.Collections.ObjectModel;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using BasketballScout.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(SeasonId), "seasonId")]
public partial class SeasonDetailViewModel : ObservableObject
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ImportExportService _importExportService;

    [ObservableProperty]
    public partial int SeasonId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime StartDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial DateTime? EndDate { get; set; }

    [ObservableProperty]
    public partial bool IsExistingSeason { get; set; }

    // ── Game format (US-21) ── minutes per period, number of periods, OT length.
    [ObservableProperty]
    public partial int PeriodLengthMinutes { get; set; } = 10;

    [ObservableProperty]
    public partial int PeriodCount { get; set; } = 4;

    [ObservableProperty]
    public partial int OvertimeLengthMinutes { get; set; } = 5;

    public ObservableCollection<Team> Teams { get; } = new();

    public SeasonDetailViewModel(
        ISeasonRepository seasonRepository,
        ITeamRepository teamRepository,
        ImportExportService importExportService)
    {
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
        _importExportService = importExportService;
    }

    partial void OnSeasonIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadSeasonAsync(value);
        }
    }

    private async Task LoadSeasonAsync(int id)
    {
        var season = await _seasonRepository.GetByIdAsync(id);
        if (season is null) return;

        IsExistingSeason = true;
        Name = season.Name;
        StartDate = season.StartDate;
        EndDate = season.EndDate;
        PeriodLengthMinutes = season.PeriodLengthMinutes > 0 ? season.PeriodLengthMinutes : 10;
        PeriodCount = season.PeriodCount > 0 ? season.PeriodCount : 4;
        OvertimeLengthMinutes = season.OvertimeLengthMinutes > 0 ? season.OvertimeLengthMinutes : 5;

        var teams = await _teamRepository.GetBySeasonIdAsync(id);
        Teams.Clear();
        foreach (var team in teams)
        {
            Teams.Add(team);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Season name is required.", "OK");
            return;
        }

        // Clamp the format to sensible ranges so a typo can't produce a broken clock.
        int periodMinutes = Math.Clamp(PeriodLengthMinutes, 1, 20);
        int periods = Math.Clamp(PeriodCount, 1, 8);
        int otMinutes = Math.Clamp(OvertimeLengthMinutes, 1, 20);
        PeriodLengthMinutes = periodMinutes;
        PeriodCount = periods;
        OvertimeLengthMinutes = otMinutes;

        if (IsExistingSeason)
        {
            var season = await _seasonRepository.GetByIdAsync(SeasonId);
            if (season is not null)
            {
                season.Name = Name.Trim();
                season.StartDate = StartDate;
                season.EndDate = EndDate;
                season.PeriodLengthMinutes = periodMinutes;
                season.PeriodCount = periods;
                season.OvertimeLengthMinutes = otMinutes;
                await _seasonRepository.UpdateAsync(season);
            }
        }
        else
        {
            var season = new Season
            {
                Name = Name.Trim(),
                StartDate = StartDate,
                EndDate = EndDate,
                PeriodLengthMinutes = periodMinutes,
                PeriodCount = periods,
                OvertimeLengthMinutes = otMinutes
            };
            var created = await _seasonRepository.AddAsync(season);
            SeasonId = created.Id;
            IsExistingSeason = true;
        }

        await Shell.Current.DisplayAlertAsync("Saved", $"Season \"{Name}\" saved.", "OK");
    }

    [RelayCommand]
    public async Task RefreshTeamsAsync()
    {
        if (SeasonId > 0)
        {
            var teams = await _teamRepository.GetBySeasonIdAsync(SeasonId);
            Teams.Clear();
            foreach (var team in teams)
            {
                Teams.Add(team);
            }
        }
    }

    [RelayCommand]
    private async Task AddTeamAsync()
    {
        if (!IsExistingSeason)
        {
            await Shell.Current.DisplayAlertAsync("Save First", "Save the season before adding teams.", "OK");
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(Views.TeamDetailPage)}?seasonId={SeasonId}");
    }

    [RelayCommand]
    private async Task SelectTeamAsync(Team team)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.TeamDetailPage)}?teamId={team.Id}&seasonId={SeasonId}");
    }

    [RelayCommand]
    private async Task ViewStatsAsync()
    {
        await Shell.Current.GoToAsync($"{nameof(Views.SeasonStatsPage)}?seasonId={SeasonId}");
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        if (Teams.Count < 2)
        {
            await Shell.Current.DisplayAlertAsync("Need Teams", "Add at least 2 teams before starting a game.", "OK");
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(Views.GameSetupPage)}?seasonId={SeasonId}");
    }

    [RelayCommand]
    private async Task ExportSeasonAsync()
    {
        try
        {
            var json = await _importExportService.ExportSeasonAsync(SeasonId);
            var fileName = $"BasketballScout_{MakeFileSafe(Name.Replace(" ", "_"))}_{DateTime.Now:yyyyMMdd}.json";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Export: {Name}",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Export failed: {ex.Message}", "OK");
        }
    }

    /// <summary>Strips characters that are invalid in a file name (e.g. a season named
    /// "2025/26"), so the export never fails building its path.</summary>
    private static string MakeFileSafe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    [RelayCommand]
    private async Task ImportSeasonAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a BasketballScout JSON file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.WinUI, new[] { ".json" } }
                })
            });

            if (result is null) return;

            var json = await File.ReadAllTextAsync(result.FullPath);

            // Preview before committing (season import creates a fresh season copy).
            var preview = ImportExportService.AnalyzeSeasonImport(json);
            bool proceed = await Shell.Current.DisplayAlertAsync(
                "Import Season?",
                $"This will create a new season \"{preview.SeasonName} (imported)\" with:\n\n" +
                $"Teams: {preview.TeamCount}\n" +
                $"Players: {preview.PlayerCount}\n" +
                $"Games: {preview.GameCount}",
                "Import", "Cancel");
            if (!proceed) return;

            var newSeasonId = await _importExportService.ImportSeasonAsync(json);

            await Shell.Current.DisplayAlertAsync("Imported",
                "Season imported successfully. Navigate to the Seasons list to find it.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Import failed: {ex.Message}", "OK");
        }
    }
}
