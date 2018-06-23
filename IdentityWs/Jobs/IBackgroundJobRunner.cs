using System;

namespace IdentityWs.Jobs
{
    // Do an IBackgroundJob repeatedly, waiting for a fixed interval between between invocations,
    // unless Nudge() is called. If Nudge() is called while waiting, the job is invoked immediately;
    // if it is called while the job is running, the job will be run again immediately afterwards.
    // The interval is configured per 'T' in appsettings.json.
    public interface IBackgroundJobRunner<T> where T : IBackgroundJob
    {
        void Start();
        void Nudge();
    }
}
