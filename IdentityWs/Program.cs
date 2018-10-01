using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentityWs
{
    public class Program
    {
        public static void Main(string[] args) => BuildWebHost(args).Run();

        // This must exist as a separate method otherwise 'dotnet ef' won't work. Cudos to
        // https://wildermuth.com/2017/07/06/Program-cs-in-ASP-NET-Core-2-0
        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseUrls("http://0.0.0.0:5003")
                .UseStartup<Startup>()
                .Build();
    }
}
