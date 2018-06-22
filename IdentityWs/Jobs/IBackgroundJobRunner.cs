using System;

namespace IdentityWs.Jobs
{
    // Do an IBackgroundJob repeatedly. Up to 'interval' may pass between invocations, unless
    // Nudge() is called. If Nudge() is called while waiting, the job is invoked immediately; if
    // it is called while the job is running, the job will be run again immediately afterwards.
    public interface IBackgroundJobRunner<T> where T : IBackgroundJob
    {
        void Start(TimeSpan interval);
        void Nudge();
    }
}
