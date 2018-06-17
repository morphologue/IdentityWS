
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace IdentityWS.Models
{
    public class IdentityWsDbContext : DbContext
    {
        public IdentityWsDbContext(DbContextOptions<IdentityWsDbContext> options) : base(options) { }

        public DbSet<Alias> Aliases { get; set; }
        public DbSet<Being> Beings { get; set; }
        public DbSet<BeingClient> BeingClients { get; set; }
        public DbSet<BeingClientDatum> BeingClientData { get; set; }
        public DbSet<Email> Emails { get; set; }
        public DbSet<LoginAttempt> LoginAttempts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder) {
            base.OnModelCreating(builder);

            builder.Entity<Alias>()
                .HasMany(a => a.Emails)
                .WithOne(e => e.To)
                .HasForeignKey(e => e.AliasID);

            builder.Entity<Alias>()
                .HasMany(a => a.LoginAttempts)
                .WithOne(h => h.Alias)
                .HasForeignKey(h => h.AliasID);

            builder.Entity<Alias>()
                .HasIndex(a => a.EmailAddress)
                .IsUnique();

            builder.Entity<Being>()
                .HasMany(b => b.Aliases)
                .WithOne(a => a.Being)
                .HasForeignKey(a => a.BeingID);

            builder.Entity<Being>()
                .HasMany(b => b.Clients)
                .WithOne(c => c.Being)
                .HasForeignKey(c => c.BeingID);

            builder.Entity<BeingClient>()
                .HasMany(c => c.Data)
                .WithOne(d => d.BeingClient)
                .HasForeignKey(d => d.BeingClientID);

            builder.Entity<BeingClient>()
                .HasIndex(bc => new { bc.BeingID, bc.ClientName })
                .IsUnique();

            builder.Entity<BeingClientDatum>()
                .HasIndex(d => new { d.BeingClientID, d.Key })
                .IsUnique();
        }
    }
}
