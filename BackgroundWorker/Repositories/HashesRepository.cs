using Context;

public interface IHashesRepository
{
    Task EnsureConfiguredAsync();
    Task SaveHashesAsync(IEnumerable<string> hashes);
}

public class SqlHashesRepository : IHashesRepository
{
    private readonly HashesContext _context;

    public SqlHashesRepository(HashesContext context)
    {
        _context = context;
    }

    public Task EnsureConfiguredAsync() => _context.Database.EnsureCreatedAsync();

    public async Task SaveHashesAsync(IEnumerable<string> hashes)
    {
        await _context.Hashes.AddRangeAsync(hashes.Select(h => new HashesDto { Id = Guid.NewGuid(), Date = DateTime.Now.Date, Sha = h }));
        await _context.SaveChangesAsync();
    }
}