using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WFI.Infrastructure;
public sealed class DesignTimeDb : IDesignTimeDbContextFactory<WfiDb>
{
    public WfiDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<WfiDb>().UseNpgsql("Host=localhost;Database=wfi;Username=wfi").Options);
}
