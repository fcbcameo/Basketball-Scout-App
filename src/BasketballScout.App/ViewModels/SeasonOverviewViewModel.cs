using System.Collections.ObjectModel;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

public partial class SeasonOverviewViewModel : ObservableObject
{
    private readonly ISeasonRepository _seasonRepository;

    public ObservableCollection<Season> Seasons { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public SeasonOverviewViewModel(ISeasonRepository seasonRepository)
    {
        _seasonRepository = seasonRepository;
    }

    [RelayCommand]
    private async Task LoadSeasonsAsync()
    {
        IsLoading = true;
        try
        {
            var seasons = await _seasonRepository.GetAllAsync();
            Seasons.Clear();
            foreach (var season in seasons)
            {
                Seasons.Add(season);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddSeasonAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.SeasonDetailPage));
    }

    [RelayCommand]
    private async Task SelectSeasonAsync(Season season)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.SeasonDetailPage)}?seasonId={season.Id}");
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.AboutPage));
    }

    /// <summary>
    /// US-12: deleting a season is the most destructive action in the app, so it
    /// requires typing the season's name — a single tap is never enough. The delete
    /// cascades to the season's games, stats, teams and players.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSeasonAsync(Season season)
    {
        string? typed = await Shell.Current.DisplayPromptAsync(
            "Delete Season",
            $"This permanently deletes \"{season.Name}\" with ALL its matches, stats, teams and players.\n\nType the season name to confirm:",
            "Delete", "Cancel",
            placeholder: season.Name);

        if (typed is null) return; // cancelled

        if (!string.Equals(typed.Trim(), season.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            await Shell.Current.DisplayAlertAsync(
                "Not Deleted", "The name didn't match — nothing was deleted.", "OK");
            return;
        }

        await _seasonRepository.DeleteAsync(season.Id);
        Seasons.Remove(season);
    }
}
