using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.UnitTests.Helpers
{
    public sealed class TestDbContextFactory : IDbContextFactory<KeytietkiemDbContext>
    {
        private readonly DbContextOptions<KeytietkiemDbContext> _options;

        public TestDbContextFactory(DbContextOptions<KeytietkiemDbContext> options)
        {
            _options = options;
        }

        public KeytietkiemDbContext CreateDbContext()
        {
            return new KeytietkiemDbContext(_options);
        }

        public ValueTask<KeytietkiemDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<KeytietkiemDbContext>(CreateDbContext());
        }
    }
}

