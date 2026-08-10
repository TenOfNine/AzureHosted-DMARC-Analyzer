using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Infrastructure.Data;

public static class SingletonEntityExtensions
{
    /// <summary>
    /// Loads the single row of a singleton-keyed settings entity (primary key == a well-known
    /// constant), creating and tracking a new instance via <paramref name="factory"/> if none
    /// exists yet. The caller mutates the returned instance's fields and calls SaveChangesAsync —
    /// this removes the repeated "FindAsync -&gt; null check -&gt; new -&gt; conditionally Add"
    /// ceremony duplicated across the Setup wizard's singleton-settings pages.
    /// </summary>
    public static async Task<TEntity> GetOrCreateSingletonAsync<TEntity, TKey>(
        this DbContext db,
        TKey singletonId,
        Func<TKey, TEntity> factory,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TKey : notnull
    {
        var existing = await db.Set<TEntity>().FindAsync([singletonId], cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = factory(singletonId);
        db.Set<TEntity>().Add(created);
        return created;
    }
}
