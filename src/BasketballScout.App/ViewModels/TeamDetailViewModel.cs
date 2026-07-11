using System.Collections.ObjectModel;
using BasketballScout.Core.Enums;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using BasketballScout.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

[QueryProperty(nameof(TeamId), "teamId")]
[QueryProperty(nameof(SeasonId), "seasonId")]
public partial class TeamDetailViewModel : ObservableObject
{
    private readonly ITeamRepository _teamRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IStatEventRepository _statEventRepository;
    private readonly IGameRepository _gameRepository;
    private readonly PdfReportService _pdfService;

    /// <summary>Enables the Scout Report button — only meaningful once the team has a
    /// completed game to scout (works regardless of roster size, US-24).</summary>
    [ObservableProperty]
    public partial bool HasFinishedGames { get; set; }

    [ObservableProperty]
    public partial int TeamId { get; set; }

    [ObservableProperty]
    public partial int SeasonId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Abbreviation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Color { get; set; } = "#e85d26";

    [ObservableProperty]
    public partial bool IsExistingTeam { get; set; }

    public ObservableCollection<Player> Players { get; } = new();

    public string[] ColorOptions { get; } = new[]
    {
        "#e85d26", "#2d7dd2", "#4ade80", "#f87171",
        "#fbbf24", "#c084fc", "#f472b6", "#60a5fa",
        "#34d399", "#fb923c", "#818cf8", "#a78bfa"
    };

    public TeamDetailViewModel(
        ITeamRepository teamRepository,
        IPlayerRepository playerRepository,
        IStatEventRepository statEventRepository,
        IGameRepository gameRepository,
        PdfReportService pdfService)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _statEventRepository = statEventRepository;
        _gameRepository = gameRepository;
        _pdfService = pdfService;
    }

    partial void OnTeamIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadTeamAsync(value);
        }
    }

    private async Task LoadTeamAsync(int id)
    {
        var team = await _teamRepository.GetByIdAsync(id);
        if (team is null) return;

        IsExistingTeam = true;
        Name = team.Name;
        Abbreviation = team.Abbreviation;
        Color = team.Color;
        SeasonId = team.SeasonId;

        // Scout report is available once this team has at least one completed game.
        var games = await _gameRepository.GetBySeasonIdAsync(team.SeasonId);
        HasFinishedGames = games.Any(g => g.Status == GameStatus.Finished
            && (g.HomeTeamId == id || g.AwayTeamId == id));

        var players = await _playerRepository.GetByTeamIdAsync(id);
        Players.Clear();
        foreach (var player in players)
        {
            Players.Add(player);
        }
    }

    [RelayCommand]
    public async Task RefreshPlayersAsync()
    {
        if (TeamId > 0)
        {
            var players = await _playerRepository.GetByTeamIdAsync(TeamId);
            Players.Clear();
            foreach (var player in players)
            {
                Players.Add(player);
            }
        }
    }

    [RelayCommand]
    private void SetColor(string color)
    {
        Color = color;
    }

    /// <summary>US-24: generate a one-page opponent scout report PDF for this team. Works for
    /// any roster size (even a single placeholder player standing in for the whole opponent).</summary>
    [RelayCommand]
    private async Task ScoutReportAsync()
    {
        try
        {
            var pdf = await _pdfService.GenerateScoutReportAsync(TeamId, SeasonId);
            var fileName = $"ScoutReport_{MakeFileSafe(Name)}.pdf";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, pdf);

            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = $"Scout Report — {Name}",
                File = new ReadOnlyFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to generate scout report: {ex.Message}", "OK");
        }
    }

    private static string MakeFileSafe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Team name is required.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(Abbreviation))
        {
            Abbreviation = Name.Length >= 3
                ? Name[..3].ToUpperInvariant()
                : Name.ToUpperInvariant();
        }

        if (IsExistingTeam)
        {
            var team = await _teamRepository.GetByIdAsync(TeamId);
            if (team is not null)
            {
                team.Name = Name.Trim();
                team.Abbreviation = Abbreviation.Trim().ToUpperInvariant();
                team.Color = Color;
                await _teamRepository.UpdateAsync(team);
            }
        }
        else
        {
            var team = new Team
            {
                Name = Name.Trim(),
                Abbreviation = Abbreviation.Trim().ToUpperInvariant(),
                Color = Color,
                SeasonId = SeasonId
            };
            var created = await _teamRepository.AddAsync(team);
            TeamId = created.Id;
            IsExistingTeam = true;
        }

        await Shell.Current.DisplayAlertAsync("Saved", $"Team \"{Name}\" saved.", "OK");
    }

    [RelayCommand]
    private async Task AddPlayerAsync()
    {
        if (!IsExistingTeam)
        {
            await Shell.Current.DisplayAlertAsync("Save First", "Save the team before adding players.", "OK");
            return;
        }

        await Shell.Current.GoToAsync($"{nameof(Views.PlayerDetailPage)}?teamId={TeamId}");
    }

    [RelayCommand]
    private async Task SelectPlayerAsync(Player player)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.PlayerDetailPage)}?playerId={player.Id}&teamId={TeamId}");
    }

    [RelayCommand]
    private async Task DeletePlayerAsync(Player player)
    {
        // A player who has appeared in any game has StatEvents referencing them, and the
        // StatEvent→Player FK is Restrict — a hard delete would throw an unhandled
        // DbUpdateException and crash. Offer deactivation instead so game history stays
        // intact; inactive players drop out of the lineup pickers (which filter IsActive).
        var history = await _statEventRepository.GetByPlayerIdAsync(player.Id);
        if (history.Count > 0)
        {
            bool markInactive = await Shell.Current.DisplayAlertAsync(
                "Player Has Game History",
                $"#{player.JerseyNumber} {player.Name} has recorded stats, so deleting them would lose game history.\n\nMark them inactive instead? They stay in past box scores but won't appear when picking a lineup.",
                "Mark Inactive", "Cancel");
            if (!markInactive) return;

            player.IsActive = false;
            await _playerRepository.UpdateAsync(player);
            await RefreshPlayersAsync();
            return;
        }

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Player",
            $"Delete #{player.JerseyNumber} {player.Name}?",
            "Delete", "Cancel");

        if (confirm)
        {
            await _playerRepository.DeleteAsync(player.Id);
            Players.Remove(player);
        }
    }

    [RelayCommand]
    private async Task ImportRosterAsync()
    {
        if (!IsExistingTeam)
        {
            await Shell.Current.DisplayAlertAsync("Save First", "Save the team before importing a roster.", "OK");
            return;
        }

        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select roster JSON file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/json" } },
                    { DevicePlatform.iOS, new[] { "public.json" } },
                    { DevicePlatform.WinUI, new[] { ".json" } },
                    { DevicePlatform.macOS, new[] { "public.json" } },
                })
            });

            if (result is null) return;

            using var stream = await result.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var importedPlayers = System.Text.Json.JsonSerializer.Deserialize<List<PlayerImportDto>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (importedPlayers is null || importedPlayers.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Import", "No players found in the file.", "OK");
                return;
            }

            int added = 0;
            foreach (var dto in importedPlayers)
            {
                var player = new Player
                {
                    Name = dto.Name?.Trim() ?? "Unknown",
                    JerseyNumber = dto.Number > 0 ? dto.Number : dto.Num,
                    Position = Enum.TryParse<Core.Enums.Position>(dto.Pos ?? dto.Position, true, out var pos)
                        ? pos : Core.Enums.Position.SG,
                    IsActive = dto.Active ?? true,
                    TeamId = TeamId
                };

                var created = await _playerRepository.AddAsync(player);
                Players.Add(created);
                added++;
            }

            await Shell.Current.DisplayAlertAsync("Import Complete", $"Imported {added} players.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Import Error", ex.Message, "OK");
        }
    }
}

public class PlayerImportDto
{
    public string? Name { get; set; }
    public int Number { get; set; }
    public int Num { get; set; }
    public string? Pos { get; set; }
    public string? Position { get; set; }
    public bool? Active { get; set; }
}
