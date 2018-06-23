using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityWs.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using IdentityWs.Utils;
using System.Threading;
using IdentityWs.Jobs;

namespace IdentityWs
{
    public class Startup
    {
        IConfiguration configuration;

        public Startup(IConfiguration configuration) => this.configuration = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<IdentityWsDbContext>(options => options.UseMySql(configuration.GetConnectionString("DefaultConnection")));
            services.AddMvc();
            services.AddSingleton<IUtcNow, DateTimeTestable>();
            services.AddScoped<IEmailSender, EmailSender>();
            services.AddSingleton<EmailQueueProcessor>();
            services.AddSingleton<IBackgroundJobRunner<EmailQueueProcessor>, BackgroundJobRunner<EmailQueueProcessor>>();
            services.AddSingleton<TableCleaner<LoginAttempt>>();
            services.AddSingleton<IBackgroundJobRunner<TableCleaner<LoginAttempt>>, BackgroundJobRunner<TableCleaner<LoginAttempt>>>();
            services.AddSingleton<TableCleaner<Email>>();
            services.AddSingleton<IBackgroundJobRunner<TableCleaner<Email>>, BackgroundJobRunner<TableCleaner<Email>>>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            using (IServiceScope serviceScope = app.ApplicationServices.CreateScope())
                // Apply DB migrations, if any.
                serviceScope.ServiceProvider.GetRequiredService<IdentityWsDbContext>().Database.Migrate();

            app.UseMvc(routes => {
                routes.MapRoute("aliases", "aliases/{email_address}/{action}", defaults: new
                {
                    controller = "Aliases",
                    action = "Index"
                });
                routes.MapRoute("clients", "aliases/{email_address}/clients/{client}/{action}", defaults: new
                {
                    controller = "Clients",
                    action = "Index"
                });
            });

            // Start background jobs.
            app.ApplicationServices.GetRequiredService<IBackgroundJobRunner<EmailQueueProcessor>>().Start();
            app.ApplicationServices.GetRequiredService<IBackgroundJobRunner<TableCleaner<LoginAttempt>>>().Start();
            app.ApplicationServices.GetRequiredService<IBackgroundJobRunner<TableCleaner<Email>>>().Start();
        }
    }
}
