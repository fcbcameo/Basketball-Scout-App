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

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScoutDbContext>();
            db.Database.EnsureCreated();
        }

        return app;
    }
}
