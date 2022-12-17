using NEAT;
using System.IO;
using System.Text;
using System.Text.Json;
using Tetris;

namespace Tetris_NEAT_AI;

public struct BotConfig
{
    public string NNPath;
    public int ThinkTime;
    public int MoveDelay;
}

public struct GameConfig
{
    public static readonly string DefaultPath = AppContext.BaseDirectory + "Config.json";
    public static readonly GameConfig Default = new GameConfig()
    {
        HasPlayer = true,
        Bots = Array.Empty<BotConfig>(),
    };

    public bool HasPlayer;
    public BotConfig[] Bots;

    public static GameConfig LoadSettings(string path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return Default;

        string jsonString = File.ReadAllText(path);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, AllowTrailingCommas = true };
        return JsonSerializer.Deserialize<GameConfig>(jsonString, options);
    }

    public static void SaveConfig(GameConfig config, string path = null)
    {
        path ??= DefaultPath;

        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, AllowTrailingCommas = true };
        string json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }
}

sealed class Program
{
    static void Main()
    {
        Game.InitWindow();

        GameConfig config = GameConfig.LoadSettings();
        GameConfig.SaveConfig(config);
        int player_count = (config.HasPlayer ? 1 : 0) + config.Bots.Length;
        int seed = Guid.NewGuid().GetHashCode();
        Game[] games = new Game[player_count].Select(x => new Game(Game.Settings.LookAheads, seed)).ToArray();

        Game.IsPaused = true;

        Game.SetGames(games);
        
        if (config.HasPlayer)
        {
            games[0].SetupPlayerInput();
            games[0].Name = "You";
        }
        if (config.Bots.Length > 0)
        {
            for (int i = 0; i < config.Bots.Length; i++)
            {
                NN nn = NN.LoadNN(AppContext.BaseDirectory + config.Bots[i].NNPath);
                Bot bot = nn.OutputCount == 1 ?
                    new BotOld(nn, games[i + (config.HasPlayer ? 1 : 0)]) :
                    new BotByScore(nn, games[i + (config.HasPlayer ? 1 : 0)]);
                bot.Start(config.Bots[i].ThinkTime, config.Bots[i].MoveDelay);
            }
        }

        Game.SetGames(games);
        
        Game.IsPaused = false;
    }
}
