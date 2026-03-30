using System.Collections.ObjectModel;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(SeasonId), "seasonId")]
public partial class SeasonDetailViewModel : ObservableObject
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ITeamRepository _teamRepository;

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

    public SeasonDetailViewModel(ISeasonRepository seasonRepository, ITeamRepository teamRepository)
    {
        _seasonRepository = seasonRepository;
        _teamRepository = teamRepository;
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
}
