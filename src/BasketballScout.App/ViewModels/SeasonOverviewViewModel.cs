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
    private async Task DeleteSeasonAsync(Season season)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Season",
            $"Delete \"{season.Name}\" and all its teams and games?",
            "Delete", "Cancel");

        if (confirm)
        {
            await _seasonRepository.DeleteAsync(season.Id);
            Seasons.Remove(season);
        }
    }
}
