using System.Runtime.CompilerServices;
using IdentityWs.Models;
using Microsoft.EntityFrameworkCore;

namespace Tests
{
    public abstract class EfTestBase
    {
        // Return a DB context for an in-memory database which is scoped to the calling method.
        protected IdentityWsDbContext CreateEf([CallerMemberName] string caller = null) =>
            new IdentityWsDbContext(new DbContextOptionsBuilder<IdentityWsDbContext>()
                .UseInMemoryDatabase($"{GetType().Name}.{caller}")
                .Options);
    }
}
