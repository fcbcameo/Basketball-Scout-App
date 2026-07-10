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
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

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
        builder.Services.AddTransient<GameEditViewModel>();
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
        builder.Services.AddTransient<GameEditPage>();
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
            EnsureAdditiveColumns(db);
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
    /// older installs we additively patch new columns onto existing tables. Each ALTER is
    /// guarded by a PRAGMA check, so this is idempotent and safe. Defaults backfill legacy
    /// rows sensibly (e.g. games → Finished, standard 10:00 × 4 format).
    /// </summary>
    private static void EnsureAdditiveColumns(ScoutDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        void AddColumns(string table, params (string Name, string Definition)[] columns)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = $"PRAGMA table_info({table});";
                using var reader = pragma.ExecuteReader();
                while (reader.Read())
                    existing.Add(reader.GetString(1)); // column 1 = name
            }

            foreach (var (name, definition) in columns)
            {
                if (existing.Contains(name)) continue;
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {definition};";
                cmd.ExecuteNonQuery();
            }
        }

        AddColumns("Games",
            ("Status", "Status INTEGER NOT NULL DEFAULT 1"),                       // legacy games → Finished
            ("ClockSecondsRemaining", "ClockSecondsRemaining INTEGER NOT NULL DEFAULT 600"),
            ("CurrentPeriod", "CurrentPeriod INTEGER NOT NULL DEFAULT 1"),
            ("ExportGuid", "ExportGuid TEXT NULL"),                                 // US-19 duplicate detection
            ("PeriodLengthSeconds", "PeriodLengthSeconds INTEGER NOT NULL DEFAULT 600"),   // US-21 format snapshot
            ("OvertimeLengthSeconds", "OvertimeLengthSeconds INTEGER NOT NULL DEFAULT 300"),
            ("RegulationPeriods", "RegulationPeriods INTEGER NOT NULL DEFAULT 4"));

        AddColumns("Seasons",
            ("PeriodLengthMinutes", "PeriodLengthMinutes INTEGER NOT NULL DEFAULT 10"),    // US-21 format
            ("PeriodCount", "PeriodCount INTEGER NOT NULL DEFAULT 4"),
            ("OvertimeLengthMinutes", "OvertimeLengthMinutes INTEGER NOT NULL DEFAULT 5"));
    }
}
