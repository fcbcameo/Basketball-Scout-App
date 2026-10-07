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
    private readonly PlayerCleanupService _playerCleanup;
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
        PlayerCleanupService playerCleanup,
        IGameRepository gameRepository,
        PdfReportService pdfService)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _playerCleanup = playerCleanup;
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

    /// <summary>US-42: flip a player's availability for upcoming matches (IsActive = available).
    /// Only games set up afterwards are affected — each game keeps the roster it started with.</summary>
    [RelayCommand]
    private async Task ToggleAvailabilityAsync(Player player)
    {
        player.IsActive = !player.IsActive;
        await _playerRepository.UpdateAsync(player);
        await RefreshPlayersAsync(); // Player isn't observable, so reload the rows to restyle the badge
    }

    [RelayCommand]
    private async Task DeletePlayerAsync(Player player)
    {
        string label = PlayerLabel(player);
        var (eventCount, gameCount) = await _playerCleanup.GetHistoryAsync(player.Id);

        // No game history: nothing to lose, so a single confirm (unchanged).
        if (eventCount == 0)
        {
            bool confirm = await Shell.Current.DisplayAlertAsync(
                "Delete Player", $"Delete {label}?", "Delete", "Cancel");
            if (!confirm) return;

            await _playerRepository.DeleteAsync(player.Id);
            Players.Remove(player);
            return;
        }

        // US-43: with game history, removing a player erases or moves their stats — irreversible,
        // so it takes an explicit choice plus a type-to-confirm. (Leaving them out of upcoming
        // matches is the Available/Absent badge instead, US-42.)
        string history = $"{label} has {eventCount} recorded {Plural(eventCount, "event")} " +
                         $"in {gameCount} {Plural(gameCount, "game")}.";
        const string deleteAll = "Delete player + all stats";
        const string mergeInto = "Merge into another player…";

        string? choice = await Shell.Current.DisplayActionSheetAsync(
            $"Remove {label}?\n{history}", "Cancel", deleteAll, mergeInto);

        if (choice == deleteAll)
            await DeleteWithStatsAsync(player, label, history);
        else if (choice == mergeInto)
            await MergeIntoAnotherPlayerAsync(player, label, history);
    }

    private async Task DeleteWithStatsAsync(Player player, string label, string history)
    {
        bool confirmed = await ConfirmByTypingNameAsync(player, "Delete Permanently",
            $"{history}\n\nThis deletes {label} and ALL their stats from every game, box score and " +
            "season total. Teammates' assists/rebounds linked to them are kept. This can't be undone.");
        if (!confirmed) return;

        await _playerCleanup.DeletePermanentlyAsync(player.Id);
        await RefreshPlayersAsync();
    }

    private async Task MergeIntoAnotherPlayerAsync(Player player, string label, string history)
    {
        var candidates = Players.Where(p => p.Id != player.Id).OrderBy(p => p.JerseyNumber).ToList();
        if (candidates.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Merge", "There's no other player on this team to merge into.", "OK");
            return;
        }

        // Action-sheet labels must be unique to map the pick back to a player — duplicates share
        // name and jersey, so number any repeats.
        var labels = new List<string>();
        foreach (var candidate in candidates)
        {
            string text = PlayerLabel(candidate), unique = text;
            for (int n = 2; labels.Contains(unique); n++) unique = $"{text} ({n})";
            labels.Add(unique);
        }

        string? picked = await Shell.Current.DisplayActionSheetAsync(
            $"Merge {label} into…", "Cancel", null, labels.ToArray());
        int index = picked is null ? -1 : labels.IndexOf(picked);
        if (index < 0) return;

        var target = candidates[index];
        string targetLabel = PlayerLabel(target);

        var overlap = await _playerCleanup.FindCourtOverlapAsync(player.Id, target.Id);
        if (overlap is not null)
        {
            await Shell.Current.DisplayAlertAsync("Can't Merge",
                $"{label} and {targetLabel} were on court at the same time in the game of " +
                $"{overlap.GameDate:d MMM yyyy}, so they can't be the same player. Nothing was changed.", "OK");
            return;
        }

        bool confirmed = await ConfirmByTypingNameAsync(player, "Merge Permanently",
            $"{history}\n\nAll of it moves to {targetLabel}, then {label} is deleted. " +
            $"{targetLabel}'s stats will include these actions. This can't be undone.");
        if (!confirmed) return;

        await _playerCleanup.MergeAsync(player.Id, target.Id);
        await RefreshPlayersAsync();
    }

    /// <summary>Type-to-confirm (US-43), mirroring the season delete: true only when the scorer
    /// types the player's name.</summary>
    private static async Task<bool> ConfirmByTypingNameAsync(Player player, string title, string message)
    {
        string? typed = await Shell.Current.DisplayPromptAsync(
            title, $"{message}\n\nType the player's name to confirm:",
            "Confirm", "Cancel", placeholder: player.Name);

        if (typed is null) return false; // cancelled

        if (string.Equals(typed.Trim(), player.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        await Shell.Current.DisplayAlertAsync("Not Confirmed", "The name didn't match — nothing was changed.", "OK");
        return false;
    }

    private static string PlayerLabel(Player player) => $"#{player.JerseyNumber} {player.Name}";

    private static string Plural(int count, string word) => count == 1 ? word : word + "s";

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
