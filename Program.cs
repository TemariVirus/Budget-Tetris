using FastConsole;
using NEAT;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace Tetris
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
            // Global exception handler
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
            {
                // Terminate sound
                BassFree();

                // Terminate rendering & input
                FConsole.RenderCallback = () => Thread.Sleep(-1);
                FConsole.InputLoopCallback = () => Thread.Sleep(-1);
                Thread.Sleep(50);

                // Print exception
                Console.Clear();
                Console.WriteLine("An unhandled exception has occurred:");
                Console.WriteLine(e.ExceptionObject);
                Console.WriteLine("\nPress any key to close this window . . .");
                Console.ReadKey(true);

                Environment.Exit(Environment.ExitCode);
            };

            Game.InitWindow(18);

            GameConfig config = GameConfig.LoadSettings();
            int player_count = (config.HasPlayer ? 1 : 0) + config.Bots.Length;
            int seed = Guid.NewGuid().GetHashCode();
            Game[] games = new Game[player_count].Select(x => new Game(Game.Settings.LookAheads, seed)).ToArray();
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
                        (Bot)new BotOld(nn, games[i + (config.HasPlayer ? 1 : 0)]) :
                        (Bot)new BotByScore(nn, games[i + (config.HasPlayer ? 1 : 0)]);
                    bot.Start(config.Bots[i].ThinkTime, config.Bots[i].MoveDelay);
                }
            }

            Game.SetGames(games);

            FConsole.InputThread.Join();
        }

        [DllImport("bass", EntryPoint = "BASS_Free")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BassFree();
    }
}
