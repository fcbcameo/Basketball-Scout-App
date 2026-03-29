using BasketballScout.Core.Models;

namespace BasketballScout.Core.Interfaces;

public interface IPdfReportService
{
    Task<byte[]> GenerateGameReportAsync(Game game);
    Task<byte[]> GenerateSeasonReportAsync(Season season);
}
