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

        if (IsExistingSeason)
        {
            var season = await _seasonRepository.GetByIdAsync(SeasonId);
            if (season is not null)
            {
                season.Name = Name.Trim();
                season.StartDate = StartDate;
                season.EndDate = EndDate;
                await _seasonRepository.UpdateAsync(season);
            }
        }
        else
        {
            var season = new Season
            {
                Name = Name.Trim(),
                StartDate = StartDate,
                EndDate = EndDate
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
            var fileName = $"BasketballScout_{Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.json";
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
