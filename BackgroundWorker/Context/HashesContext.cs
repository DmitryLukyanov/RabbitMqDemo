using Microsoft.EntityFrameworkCore;

namespace Context
{
    public class HashesContext : DbContext
    {
        private readonly IConfiguration _configuration;
        private const string DefaultSchema = "dbo";

        public HashesContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DbSet<HashesDto> Hashes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            var connectionString = _configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Default");
            }
            optionsBuilder.EnableSensitiveDataLogging(true);
            optionsBuilder.UseSqlServer(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<HashesDto>().ToTable(nameof(Hashes), DefaultSchema).HasKey(c => c.Id);
        }
    }
}
