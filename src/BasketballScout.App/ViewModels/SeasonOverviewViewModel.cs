using System.Collections.ObjectModel;
using BasketballScout.Core.Interfaces;
using BasketballScout.Core.Models;
using BasketballScout.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BasketballScout.App.ViewModels;

public partial class SeasonOverviewViewModel : ObservableObject
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly ImportExportService _importExportService;

    private const string LastBackupKey = "LastBackupDate";
    private static readonly TimeSpan BackupReminderAfter = TimeSpan.FromDays(30);

    public ObservableCollection<Season> Seasons { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Shown as a subtle nudge when there's data to lose and no backup in 30+ days (US-26).</summary>
    [ObservableProperty]
    public partial bool BackupReminderVisible { get; set; }

    public SeasonOverviewViewModel(ISeasonRepository seasonRepository, ImportExportService importExportService)
    {
        _seasonRepository = seasonRepository;
        _importExportService = importExportService;
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
            UpdateBackupReminder();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateBackupReminder()
    {
        if (Seasons.Count == 0) { BackupReminderVisible = false; return; }

        var lastTicks = Preferences.Default.Get(LastBackupKey, 0L);
        bool overdue = lastTicks == 0
            || (DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc)) > BackupReminderAfter;
        BackupReminderVisible = overdue;
    }

    /// <summary>US-26: one-tap backup of ALL data to a single shareable JSON file.</summary>
    [RelayCommand]
    private async Task BackupAllAsync()
    {
        try
        {
            var json = await _importExportService.ExportAllAsync(AppInfo.Current.VersionString);
            var fileName = $"BasketballScout_Backup_{DateTime.Now:yyyyMMdd_HHmm}.json";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json);

            Preferences.Default.Set(LastBackupKey, DateTime.UtcNow.Ticks);
            UpdateBackupReminder();

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "BasketballScout Backup",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Backup Failed", ex.Message, "OK");
        }
    }

    /// <summary>US-26: restore a backup file. Adds its seasons alongside existing data
    /// (never overwrites); if data already exists, asks first.</summary>
    [RelayCommand]
    private async Task RestoreAllAsync()
    {
        try
        {
            var pick = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a BasketballScout backup (.json)"
            });
            if (pick is null) return;

            var json = await File.ReadAllTextAsync(pick.FullPath);
            var preview = ImportExportService.AnalyzeRestore(json);

            string existingNote = Seasons.Count > 0
                ? "\n\nThis is ADDED alongside your existing seasons — nothing is overwritten."
                : string.Empty;
            bool proceed = await Shell.Current.DisplayAlertAsync(
                "Restore Backup?",
                $"Backed up {preview.ExportDate.ToLocalTime():MMM d, yyyy}.\n\n" +
                $"Seasons: {preview.SeasonCount}\nTeams: {preview.TeamCount}\n" +
                $"Players: {preview.PlayerCount}\nGames: {preview.GameCount}{existingNote}",
                "Restore", "Cancel");
            if (!proceed) return;

            var result = await _importExportService.RestoreAllAsync(json);
            await LoadSeasonsAsync();

            await Shell.Current.DisplayAlertAsync(
                "Restored",
                $"Added {result.SeasonsRestored} season(s) and {result.GamesRestored} game(s).", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Restore Failed", ex.Message, "OK");
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
