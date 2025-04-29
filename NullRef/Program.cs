using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Xdg.Directories;


var AppName = "NullRef";
var LogsFolder = Path.Combine(BaseDirectory.StateHome, AppName, "Logs");

var date = DateTime.Now;

var logFilePath = Path.Combine(LogsFolder, date.Year.ToString(), date.Month.ToString("D2"), $"{AppName}-{date.Day}.log");

var loggerConfig = new LoggerConfiguration()
#if DEBUG
    .MinimumLevel.Debug()
#endif
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithThreadName()
    .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
    .WriteTo.Async(a => a.File(logFilePath, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}]: {Message:lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day, buffered: true));

var logger = loggerConfig.CreateLogger();
Log.Logger = logger;


var builder = Host.CreateApplicationBuilder(args);


#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
builder.Services
    .AddLogging(loggingBuilder =>
        loggingBuilder.ClearProviders().AddSerilog(dispose: true))
    .AddDiscordGateway(options =>
    {
        options.Intents = GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent;
    })

    .AddGatewayEventHandlers(typeof(Program).Assembly);
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code

var host = builder.Build()
                  .UseGatewayEventHandlers();

SentrySdk.Init(options =>
{
    // This allows end users to test themselves what data is sent to Sentry
    var sentryDsnEnv = Environment.GetEnvironmentVariable("SENTRY_DSN");
    options.Dsn = !string.IsNullOrWhiteSpace(sentryDsnEnv) ? sentryDsnEnv : "https://e0a07df30c8b96560f93b10cf4338eba@o4504136997928960.ingest.us.sentry.io/4508785180737536";

    // When debug is enabled, the Sentry client will emit detailed debugging information to the console.
    options.Debug = Environment.GetEnvironmentVariable("SENTRY_DEBUG") == "1";
    // VLCException includes multiple paths with username
    // For full transparency, I discovered this issue on my computer.
    // No other users are effected to my knowledge.
    options.SetBeforeSend((sentryEvent, hint) =>
    {
        if (sentryEvent.Exception != null
            && !string.IsNullOrEmpty(sentryEvent.Exception.Message))
        {
            if (sentryEvent.Exception.Message.Contains(Environment.UserName)) return null;
        }

        return sentryEvent;
    });

    // Enabling this option is recommended for client applications only. It ensures all threads use the same global scope.
    options.IsGlobalModeEnabled = true;

    // This option is recommended. It enables Sentry's "Release Health" feature.
    options.AutoSessionTracking = true;

    // Set TracesSampleRate to 1.0 to capture 100%
    // of transactions for tracing.
    options.TracesSampleRate = 0.2;

    // Sample rate for profiling, applied on top of the TracesSampleRate,
    // e.g. 0.5 means we want to profile 50 % of the captured transactions.
    // We recommend adjusting this value in production.
    options.ProfilesSampleRate = 0.5;
    options.AddIntegration(new ProfilingIntegration());

    // This saves events for later when internet connectivity is poor/not working.
    options.CacheDirectoryPath = Path.Combine(BaseDirectory.CacheHome, AppName);
});
await host.RunAsync();
Log.CloseAndFlush();
