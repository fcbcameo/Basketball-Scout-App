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
}
