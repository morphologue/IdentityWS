using System;
using Microsoft.Extensions.Configuration;

namespace IdentityWs.Jobs
{
    // A job which will be executed periodically in the context of an IServiceScope
    public interface IBackgroundJob
    {
        void Run(IServiceProvider services, IConfigurationSection section);
    }
}
