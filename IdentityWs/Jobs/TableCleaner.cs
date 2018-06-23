using System;
using System.Linq;
using System.Reflection;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityWs.Jobs
{
    // Delete old rows from the entity 'T' in IdentityWsDbContext.
    public class TableCleaner<T> : IBackgroundJob where T : class, ICleanable
    {
        IUtcNow now;
        string entityName;
        PropertyInfo tableProperty;

        public TableCleaner(IUtcNow now)
        {
            this.now = now;
            this.entityName = typeof(T).Name;
            this.tableProperty = typeof(IdentityWsDbContext)
                .GetProperties()
                .First(p => p.PropertyType == typeof(DbSet<T>));
        }

        public void Run(IServiceProvider services, IConfigurationSection section)
        {
            // Work out when to delete before (from the config).
            int days = section.GetSection("DeleteCreatedBeforeDays").GetValue<int>(entityName);
            DateTime delete_before = now.UtcNow.AddDays(-days);

            // Delete any older rows.
            IdentityWsDbContext ef = services.GetRequiredService<IdentityWsDbContext>();
            DbSet<T> table = (DbSet<T>)tableProperty.GetValue(ef);
            table.RemoveRange(table.Where(e => e.DateCreated < delete_before).ToList());
            ef.SaveChanges();
        }
    }
}
