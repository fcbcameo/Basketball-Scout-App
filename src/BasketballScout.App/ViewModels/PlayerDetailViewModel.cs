using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(PlayerId), "playerId")]
[QueryProperty(nameof(TeamId), "teamId")]
public partial class PlayerDetailViewModel : ObservableObject
{
    private readonly IPlayerRepository _playerRepository;

    [ObservableProperty]
    public partial int PlayerId { get; set; }

    [ObservableProperty]
    public partial int TeamId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int JerseyNumber { get; set; }

    [ObservableProperty]
    public partial Position SelectedPosition { get; set; } = Position.SG;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [ObservableProperty]
    public partial bool IsExistingPlayer { get; set; }

    public Position[] PositionOptions { get; } = Enum.GetValues<Position>();

    public PlayerDetailViewModel(IPlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }

    partial void OnPlayerIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadPlayerAsync(value);
        }
    }

    private async Task LoadPlayerAsync(int id)
    {
        var player = await _playerRepository.GetByIdAsync(id);
        if (player is null) return;

        IsExistingPlayer = true;
        Name = player.Name;
        JerseyNumber = player.JerseyNumber;
        SelectedPosition = player.Position;
        IsActive = player.IsActive;
        TeamId = player.TeamId;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Player name is required.", "OK");
            return;
        }

        if (IsExistingPlayer)
        {
            var player = await _playerRepository.GetByIdAsync(PlayerId);
            if (player is not null)
            {
                player.Name = Name.Trim();
                player.JerseyNumber = JerseyNumber;
                player.Position = SelectedPosition;
                player.IsActive = IsActive;
                await _playerRepository.UpdateAsync(player);
            }
        }
        else
        {
            var player = new Player
            {
                Name = Name.Trim(),
                JerseyNumber = JerseyNumber,
                Position = SelectedPosition,
                IsActive = IsActive,
                TeamId = TeamId
            };
            var created = await _playerRepository.AddAsync(player);
            PlayerId = created.Id;
            IsExistingPlayer = true;
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
