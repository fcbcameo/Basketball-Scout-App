using BasketballScout.Core.Interfaces;

namespace BasketballScout.Data;

/// <summary>
/// Transaction wrapper over the shared <see cref="ScoutDbContext"/>. Because the app's
/// repositories all resolve the same scoped context, a transaction begun here spans every
/// repository write made inside the delegate. (US-29 will move to per-operation contexts;
/// this abstraction is the seam that change will refactor behind.)
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ScoutDbContext _db;

    public UnitOfWork(ScoutDbContext db)
    {
        _db = db;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> work)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await work();          // repositories write to the same context, joining this tx
        await tx.CommitAsync(); // dispose-without-commit on exception rolls everything back
    }
}
