using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Logging;
using OnIT.Smtp.Core.Mail;
using OnIT.Smtp.Service;
using Serilog;

ConfigPaths.EnsureDirectoriesExist();

var configStore = new ConfigStore();
var initialConfig = configStore.Load();

var liveLogSink = new LiveLogSink();
var serilogLogger = LoggingSetup.CreateLogger(initialConfig.Logging, liveLogSink);
Log.Logger = serilogLogger;

try
{
    Log.Information("Starting OnIT-SMTP service.");

    var host = Host.CreateDefaultBuilder(args)
        .UseWindowsService(options => options.ServiceName = "OnIT-SMTP")
        .UseSerilog()
        .ConfigureServices(services =>
        {
            services.AddSingleton(liveLogSink);
            services.AddSingleton(configStore);
            services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
            services.AddSingleton<GraphCredentialFactory>();
            services.AddSingleton<GraphMailService>();
            services.AddHostedService<RelayWorker>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OnIT-SMTP service terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
