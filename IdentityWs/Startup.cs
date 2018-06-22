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
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            using (IServiceScope serviceScope = app.ApplicationServices.CreateScope())
                // Apply DB migrations, if any.
                serviceScope.ServiceProvider.GetRequiredService<IdentityWsDbContext>().Database.Migrate();

            app.UseMvc(routes => routes.MapRoute("default", "{controller}/{email_address}/{action=Index}/{client?}"));

            TimeSpan interval = TimeSpan.FromSeconds(configuration.GetValue<double>("SecsBetweenEmailQueueRuns"));
            app.ApplicationServices.GetRequiredService<IBackgroundJobRunner<EmailQueueProcessor>>().Start(interval);
        }
    }
}
