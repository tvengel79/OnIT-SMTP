using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OnIT.Smtp.Bridge;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Logging;
using OnIT.Smtp.Core.Mail;
using OnIT.Smtp.Core.Runtime;
using Serilog;

ConfigPaths.EnsureDirectoriesExist();

var configStore = new ConfigStore();
var initialConfig = configStore.Load();

var liveLogSink = new LiveLogSink();
var serilogLogger = LoggingSetup.CreateLogger(initialConfig.Logging, liveLogSink, includeConsole: true);
Log.Logger = serilogLogger;

try
{
    Log.Information("Starting OnIT-SMTP bridge.");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices(services =>
        {
            services.AddSingleton(liveLogSink);
            services.AddSingleton(configStore);
            services.AddSingleton<ISecretProtector, PortableSecretProtector>();
            services.AddSingleton<GraphCredentialFactory>();
            services.AddSingleton<GraphMailService>();
            services.AddHostedService<BridgeRelayWorker>();
            services.AddHostedService<SecretExpiryWorker>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OnIT-SMTP bridge terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
