using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityWS.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using IdentityWS.Utils;

namespace IdentityWS
{
    public class Startup
    {
        public Startup(IConfiguration configuration) => this.configuration = configuration;

        IConfiguration configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<IdentityWsDbContext>(options => options.UseMySql(configuration.GetConnectionString("DefaultConnection")));

            services.AddMvc();

            services.AddSingleton<IUtcNow, DateTimeTestable>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            using (IServiceScope serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
                // Apply DB migrations, if any.
                serviceScope.ServiceProvider.GetRequiredService<IdentityWsDbContext>().Database.Migrate();

            app.UseMvc(routes => routes.MapRoute("default", "{controller}/{email_address}/{action=Index}/{client?}"));
        }
    }
}
