using FastConsole;
using NEAT;
using Tetris;

using System;
using System.Linq;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

// Inputs: standard height, caves, pillars, row transitions, col transitions, trash, cleared, intent
// Outputs: score of state, intent(don't search further if it drops below a treshold)
//TODO:
//-add RemoveAllListeners method that uses a predicate
//-move PieceColors to Piece class
//-handle games' widths and heights chaning mid-game
namespace Tetris_NEAT_AI
{
    [DataContract]
    public struct BotConfig
    {
        [DataMember]
        public string NNPath { get; private set; }
        [DataMember]
        public int ThinkTime { get; private set; }
        [DataMember]
        public int MoveDelay { get; private set; }
    }

    [DataContract]
    public struct GameConfig
    {
        public static readonly string DefaultPath = AppContext.BaseDirectory + "Config.json";

        public static readonly GameConfig Default = new GameConfig()
        {
            HasPlayer = true,
            Bots = Array.Empty<BotConfig>(),
        };

        [DataMember]
        public bool HasPlayer { get; private set; }
        [DataMember]
        public BotConfig[] Bots { get; private set; }
        
        
        public static GameConfig LoadSettings(string path = null)
        {
            if (path == null)
                path = DefaultPath;
            if (!File.Exists(path))
            {
                if (path == DefaultPath)
                    SaveSettings(Default, DefaultPath);

                return Default;
            }

            var mem_stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path)));
            var serializer = new DataContractJsonSerializer(typeof(GameConfig));
            GameConfig settings;
            try
            {
                settings = (GameConfig)serializer.ReadObject(mem_stream);
            }
            catch
            {
                settings = Default;
            }
            mem_stream.Dispose();
            return settings;
        }

        public static void SaveSettings(GameConfig settings, string path = null)
        {
            if (path == null)
                path = DefaultPath;

            var mem_stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(GameConfig));
            serializer.WriteObject(mem_stream, settings);
            mem_stream.Position = 0;
            var sr = new StreamReader(mem_stream);

            File.WriteAllText(path, sr.ReadToEnd());

            sr.Dispose();
            mem_stream.Dispose();
        }
    }

    sealed class Program
    {
        static void Main()
        {
            Game.InitWindow();

            GameConfig config = GameConfig.LoadSettings();
            int player_count = (config.HasPlayer ? 1 : 0) + config.Bots.Length;
            int seed = Guid.NewGuid().GetHashCode();
            Game[] games = new Game[player_count].Select(x => new Game(Game.Settings.LookAheads, seed)).ToArray();
            Game.SetGames(games);

            Game.IsPaused = true;

            if (config.HasPlayer)
            {
                games[0].SetupPlayerInput();
                games[0].Name = "You";
            }
            if (config.Bots.Length > 0)
            {
                FConsole.Set(FConsole.Width, FConsole.Height + 2);
                for (int i = 0; i < config.Bots.Length; i++)
                {
                    NN nn = NN.LoadNN(AppContext.BaseDirectory + config.Bots[i].NNPath);
                    Bot bot = nn.OutputCount == 1 ?
                        (Bot)new BotOld(nn, games[i + (config.HasPlayer ? 1 : 0)]) :
                        (Bot)new BotByScore(nn, games[i + (config.HasPlayer ? 1 : 0)]);
                    bot.Start(config.Bots[i].ThinkTime, config.Bots[i].MoveDelay);
                }
            }

            Game.IsPaused = false;
        }
    }
}
