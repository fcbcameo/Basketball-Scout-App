using BasketballScout.Core.Interfaces;
using BasketballScout.Data;
using BasketballScout.Data.Repositories;
using BasketballScout.Services;
using BasketballScout.App.Views;
using BasketballScout.App.ViewModels;
using CommunityToolkit.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BasketballScout.App;

public static class MauiProgram
{
    /// <summary>Captures a fatal startup error so the UI can show it instead of crashing.</summary>
    public static Exception? StartupError { get; private set; }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Database
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "basketballscout.db");
        builder.Services.AddDbContext<ScoutDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repositories
        builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
        builder.Services.AddScoped<ITeamRepository, TeamRepository>();
        builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
        builder.Services.AddScoped<IGameRepository, GameRepository>();
        builder.Services.AddScoped<IStatEventRepository, StatEventRepository>();

        // Services
        builder.Services.AddScoped<GameStatsService>();
        builder.Services.AddScoped<PdfReportService>();
        builder.Services.AddScoped<ImportExportService>();

        // ViewModels
        builder.Services.AddTransient<SeasonOverviewViewModel>();
        builder.Services.AddTransient<SeasonDetailViewModel>();
        builder.Services.AddTransient<TeamDetailViewModel>();
        builder.Services.AddTransient<PlayerDetailViewModel>();
        builder.Services.AddTransient<GameSetupViewModel>();
        builder.Services.AddTransient<GameScoringViewModel>();
        builder.Services.AddTransient<GameBoxScoreViewModel>();
        builder.Services.AddTransient<SeasonStatsViewModel>();
        builder.Services.AddTransient<PlayerStatsViewModel>();

        // Pages
        builder.Services.AddTransient<SeasonOverviewPage>();
        builder.Services.AddTransient<SeasonDetailPage>();
        builder.Services.AddTransient<TeamDetailPage>();
        builder.Services.AddTransient<PlayerDetailPage>();
        builder.Services.AddTransient<GameSetupPage>();
        builder.Services.AddTransient<GameScoringPage>();
        builder.Services.AddTransient<GameBoxScorePage>();
        builder.Services.AddTransient<SeasonStatsPage>();
        builder.Services.AddTransient<PlayerStatsPage>();
        builder.Services.AddTransient<AboutPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Ensure database is created. Capture any failure so the app can
        // surface it on screen instead of crashing silently at launch.
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ScoutDbContext>();
            db.Database.EnsureCreated();
            EnsureGameColumns(db);
        }
        catch (Exception ex)
        {
            StartupError = ex;
        }

        return app;
    }

    /// <summary>
    /// EnsureCreated() builds the schema only when the database file doesn't exist yet;
    /// it never alters an existing database, and this project has no EF migrations. So for
    /// older installs we additively patch the Games table with the lifecycle columns added
    /// in US-10. Each ALTER is guarded by a PRAGMA check, so this is idempotent and safe.
    /// Legacy games predate the feature, so Status backfills to 1 (Finished).
    /// </summary>
    private static void EnsureGameColumns(ScoutDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(Games);";
            using var reader = pragma.ExecuteReader();
            while (reader.Read())
                existing.Add(reader.GetString(1)); // column 1 = name
        }

        void AddColumn(string name, string definition)
        {
            if (existing.Contains(name)) return;
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"ALTER TABLE Games ADD COLUMN {definition};";
            cmd.ExecuteNonQuery();
        }

        AddColumn("Status", "Status INTEGER NOT NULL DEFAULT 1");                       // legacy games → Finished
        AddColumn("ClockSecondsRemaining", "ClockSecondsRemaining INTEGER NOT NULL DEFAULT 600");
        AddColumn("CurrentPeriod", "CurrentPeriod INTEGER NOT NULL DEFAULT 1");
    }
}
