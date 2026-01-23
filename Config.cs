using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace TgChannelSearch;

[method:JsonConstructor]
public class Config(string token)
{
    public string Token { get; } = token;

    private static Config Default { get; } = new Config("123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11");

    public static bool TryLoad(long channelId, string configDir, out Config config)
    {
        var configPath = Path.Combine(configDir, $"config_{channelId}.json");

        if (!File.Exists(configPath))
        {
            using var @default = File.Create(configPath);
            JsonSerializer.Serialize(@default, Default, new JsonSerializerOptions() { WriteIndented = true });
            Log.Logger.Warning("No config file found. Created default config at {path}", configPath);
            config = Default;
            return true;
        }

        using var file = File.OpenRead(configPath);
        try
        {
            config = JsonSerializer.Deserialize<Config>(file, new JsonSerializerOptions() { AllowTrailingCommas = true });
        }
        catch (Exception ex)
        {
            Log.Logger.Error("Failed to deserialize config: {message}", ex.Message);
            config = null;
            return false;
        }

        return true;
    }
}

