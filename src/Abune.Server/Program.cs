//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace UdpAkkaServer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>The main program launcher.</summary>
    public static class Program
    {
        /// <summary>Defines the entry point of the application.</summary>
        /// <param name="args">The arguments.</param>
        /// <returns>Task for completion.</returns>
        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
             .ConfigureAppConfiguration((hostingContext, config) =>
             {
                 config.AddEnvironmentVariables();
                 if (args != null)
                 {
                     config.AddCommandLine(args);
                 }

                 config.AddJsonFile("appSettings.json", optional: false, reloadOnChange: true);
             })
             .ConfigureServices((hostContext, services) =>
             {
                 services.AddOptions();
                 services.Configure<AkkaNodeServiceConfig>(hostContext.Configuration.GetSection("AkkaNodeService"));

                 services.AddSingleton<IHostedService, AkkaNodeService>();
             })
             .ConfigureLogging((hostingContext, logging) =>
             {
                 logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                 logging.AddConsole();
             });

            await builder.RunConsoleAsync().ConfigureAwait(false);
        }
    }
}
