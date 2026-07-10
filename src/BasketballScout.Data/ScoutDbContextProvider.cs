using BasketballScout.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BasketballScout.Data;

/// <summary>
/// Hands out a short-lived <see cref="ScoutDbContext"/> per operation from a pooled factory
/// (US-29), replacing the previous app-lifetime singleton context. Each repository call leases
/// a fresh context and disposes it, so two overlapping loads can't collide on one context and
/// the change-tracker never grows across a long session.
///
/// For multi-step writes that must be atomic (imports, cascade deletes), <see cref="IUnitOfWork"/>
/// runs the work against a single *ambient* context + transaction; while that is active, leases
/// return the ambient context (and don't dispose it), so every repository write inside the unit
/// of work participates in the one transaction. Single-user app: a unit of work is never nested
/// concurrently, so a single ambient slot is sufficient.
/// </summary>
public sealed class ScoutDbContextProvider : IUnitOfWork
{
    private readonly IDbContextFactory<ScoutDbContext> _factory;
    private ScoutDbContext? _ambient;

    public ScoutDbContextProvider(IDbContextFactory<ScoutDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>Leases a context for one operation: the ambient one if a unit of work is active
    /// (not owned — the unit of work disposes it), otherwise a fresh one the caller disposes.</summary>
    public DbLease Lease()
        => _ambient is not null
            ? new DbLease(_ambient, owned: false)
            : new DbLease(_factory.CreateDbContext(), owned: true);

    public async Task ExecuteInTransactionAsync(Func<Task> work)
    {
        // Reentrant: if already inside a unit of work, just run — the outer one owns the tx.
        if (_ambient is not null)
        {
            await work();
            return;
        }

        await using var db = await _factory.CreateDbContextAsync();
        _ambient = db;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await work();          // repository leases return this ambient context
            await tx.CommitAsync(); // dispose-without-commit on exception rolls everything back
        }
        finally
        {
            _ambient = null;
        }
    }
}

/// <summary>A leased context. Disposing only disposes it when this lease owns it (i.e. it isn't
/// the ambient unit-of-work context).</summary>
public sealed class DbLease : IAsyncDisposable
{
    public ScoutDbContext Db { get; }

    /// <summary>True when this lease created the context (so no ambient transaction is active,
    /// and cascade operations must open their own transaction).</summary>
    public bool Owned { get; }

    public DbLease(ScoutDbContext db, bool owned)
    {
        Db = db;
        Owned = owned;
    }

    public async ValueTask DisposeAsync()
    {
        if (Owned) await Db.DisposeAsync();
    }
}
