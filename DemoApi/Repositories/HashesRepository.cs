using Context;
using Microsoft.EntityFrameworkCore;
using System.Linq;

public interface IHashesRepository
{
    Task EnsureConfiguredAsync();
    IEnumerable<(string Sha, DateTime Date)> GetStoredHashes(int? skip = null, int? take = null);
    IEnumerable<(int Count, DateTime Date)> GetGroupedStoredHashes();
    Task ClearAllStoredHashes(CancellationToken cancellationToken = default);
}

public class SqlHashesRepository : IHashesRepository
{
    private readonly HashesContext _context;

    public SqlHashesRepository(HashesContext context)
    {
        _context = context;
    }

    public Task EnsureConfiguredAsync() => _context.Database.EnsureCreatedAsync();

    public IEnumerable<(int Count, DateTime Date)> GetGroupedStoredHashes()
    {
        if (!_context.Database.CanConnect())
        {
            return Enumerable.Empty<(int Count, DateTime Date)>();
        }

        return _context
            .Hashes
            .GroupBy(c => c.Date.Date)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count()
            }).
            ToList()
            .Select(i => (i.Count, i.Date));
    }

    public IEnumerable<(string Sha, DateTime Date)> GetStoredHashes(int? skip = null, int? take = null)
    {
        if (!_context.Database.CanConnect())
        {
            return Enumerable.Empty<(string Sha, DateTime Date)>();
        }

        var hashes = _context.Hashes.Select(h => new { h.Sha, h.Date });
        if (skip.HasValue)
        {
            hashes = hashes.Skip(skip.Value);
        }

        if (take.HasValue)
        {
            hashes = hashes.Take(take.Value);
        }
        
        return hashes.ToList().Select(h => (h.Sha, h.Date));
    }

    public async Task ClearAllStoredHashes(CancellationToken cancellationToken = default)
    {
        if (!_context.Database.CanConnect())
        {
            return;
        }

        await _context
            .Hashes
            .ExecuteDeleteAsync(cancellationToken);
    }

}