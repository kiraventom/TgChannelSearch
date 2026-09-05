using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Telegram.Bot;
using TgChannelLib.Model;
using Microsoft.EntityFrameworkCore;
using TgChannelLib;

namespace TgChannelSearch;

internal class Program
{
    private const string PROJECT_NAME = nameof(TgChannelSearch);

    private static async Task Main(string[] args)
    {
        var appDataDir = CreateAppDataDir();
        var appConfigDir = CreateAppConfigDir();

        var logger = BuildLogger(appDataDir);

        if (!Config.TryLoad(appConfigDir, out var config))
        {
            logger.Fatal("Failed to load config, closing");
            return;
        }

        if (!TryLoadChannel(args, out var channel))
            return;

        var builder = Host.CreateApplicationBuilder();

        builder.Services
            .AddSerilog(logger)
            .AddSingleton(config)
            .AddSingleton<IChannelInfo>(channel)
            .AddSingleton<Channel>(channel)
            .AddSingleton<TelegramBotClient>(static sp =>
            {
                var config = sp.GetRequiredService<Config>();
                return new TelegramBotClient(config.Token);
            })
            .AddDbContext<ChannelContext>(static (sp, o) =>
            {
                var channel = sp.GetRequiredService<Channel>();
                o.UseSqlite($"Data Source={channel.DbPath};");
            })
            .AddScoped<SearchService>()
            .AddHostedService<AppService>();

        var host = builder.Build();

        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            logger.Fatal(ex.ToString());
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static bool TryLoadChannel(string[] args, out Channel channel)
    {
        channel = null;

        if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("Usage: TgChannelSearch /path/to/db @OptionalChannelTag>");
            Console.WriteLine("Examples:");
            Console.WriteLine("\tTgChannelSearch ~/.local/share/TgChannelRecognize/123456789.db");
            Console.WriteLine("\tTgChannelSearch ~/987654321.db @test_channel");
            return false;
        }

        var dbFilePath = Path.Combine("recognize", args[0]);

        if (!File.Exists(dbFilePath))
        {
            Console.Error.WriteLine("Database file {0} does not exist, closing", dbFilePath);
            return false;
        }

        if (!Channel.TryParse(dbFilePath, out channel))
        {
            Console.Error.WriteLine("Failed to figure out channel id, closing");
            return false;
        }

        if (args.Length == 2)
        {
            if (string.IsNullOrWhiteSpace(args[1]) || !args[1].StartsWith('@'))
            {
                Console.WriteLine("Usage: TgChannelSearch /path/to/db @ChannelTag>");
                Console.WriteLine("Example: TgChannelSearch ~/987654321.db @test_channel");
            }

            channel.ChannelTag = args[1].Trim();
        }

        return true;
    }

    public static ILogger BuildLogger(string appDataDir)
    {
        var logDir = Path.Combine(appDataDir, "logs");
        Directory.CreateDirectory(logDir);

        var logFile = Path.Combine(logDir, $"{PROJECT_NAME}-.log");
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .WriteTo.Console(LogEventLevel.Information)
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Logger = logger;

        return logger;
    }

    private static string CreateAppConfigDir()
    {
        var path = Path.Combine(Environment.CurrentDirectory, "config");

        Directory.CreateDirectory(path);
        return path;
    }

    public static string CreateAppDataDir()
    {
        var path = Path.Combine(Environment.CurrentDirectory, "data");

        Directory.CreateDirectory(path);
        return path;
    }
}
