using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityWs.Jobs
{
    // Do an IBackgroundJob repeatedly. Up to 'interval' may pass between invocations, unless
    // Nudge() is called. If Nudge() is called while waiting, the job is invoked immediately; if
    // it is called while the job is running, the job will be run again immediately afterwards.
    public class BackgroundJobRunner<T> : IBackgroundJobRunner<T> where T : IBackgroundJob
    {
        ILogger<BackgroundJobRunner<T>> log;
        T job;
        IServiceScopeFactory factory;
        EventWaitHandle trigger;
        string name;
        IConfigurationSection jobSection;

        public BackgroundJobRunner(ILogger<BackgroundJobRunner<T>> log, IConfiguration config, T job, IServiceScopeFactory factory)
        {
            this.log = log;
            this.job = job;
            this.factory = factory;
            this.trigger = new EventWaitHandle(false, EventResetMode.AutoReset);

            // Get the name, less generics info.
            string raw_name = typeof(T).Name;
            int generic_idx = raw_name.IndexOf('`');
            this.name = generic_idx == -1 ? raw_name : raw_name.Substring(0, generic_idx);

            this.jobSection = config.GetSection("BackgroundJobs").GetSection(name);
        }

        public void Start()
        {
            TimeSpan interval = TimeSpan.FromMinutes(jobSection.GetValue<double>("MinsBetweenInvocations"));
            Thread t = new Thread(() => {
                try {
                    while (true) {
                        trigger.WaitOne(interval);
                        try {
                            using (IServiceScope scope = factory.CreateScope())
                                job.Run(scope.ServiceProvider, jobSection);
                            log.LogInformation("Successfully completed job {name}", name);
                        } catch (Exception e) {
                            log.LogError(e, "Exception during invocation of job {name}", name);
                        }
                    }
                } catch (Exception e) {
                    log.LogCritical(e, "An exception has terminated the top-level loop for job {name}", name);
                }
            });
            t.IsBackground = true;
            t.Name = name;
            t.Start();
        }

        public void Nudge()
        {
            trigger.Set();
        }
    }
}
