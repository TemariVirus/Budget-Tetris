namespace TetrisAI;

using FastConsole;
using System.Windows.Input;
using Tetris;

class Program
{
    static readonly string BaseDirectory = AppContext.BaseDirectory;
        
    static void Main()
    {
        // Set up console
        Console.Title = "Console Tetris Clone With Bots";
        FConsole.Framerate = 12;
        FConsole.CursorVisible = false;
        FConsole.SetFont("Consolas", 18);
        FConsole.Initialise(FrameEndCallback);

        // Set up games
        int seed = new Random().Next();
        Game[] games = { new Game(6, seed), new Game(5, seed) };

        Game main = games[0];
        main.SoftG = 40;
        games[1].SoftG = 40;

        Game.SetGames(games);

        // Set up bots
        new Bot(BaseDirectory + @"NNs\plan2.txt", games[0]).Start(150, 100);
        new Bot(BaseDirectory + @"NNs\plan3.txt", games[1]).Start(150, 100);

        // Set up input handler
        SetupPlayerInput(main);
    }

    static void SetupPlayerInput(Game player_game)
    {
        FConsole.AddOnPressListener(Key.Left, () => player_game.Play(Moves.Left));
        //FastConsole.AddOnHoldListener(Key.Left, () => main.Play(Moves.Left), 133, 0);
        FConsole.AddOnHoldListener(Key.Left, () => player_game.Play(Moves.DASLeft), 133, 15);

        FConsole.AddOnPressListener(Key.Right, () => player_game.Play(Moves.Right));
        //FastConsole.AddOnHoldListener(Key.Right, () => main.Play(Moves.Right), 133, 0);
        FConsole.AddOnHoldListener(Key.Right, () => player_game.Play(Moves.DASRight), 133, 15);

        FConsole.AddOnPressListener(Key.Up, () => player_game.Play(Moves.RotateCW));
        FConsole.AddOnPressListener(Key.Z, () => player_game.Play(Moves.RotateCCW));
        FConsole.AddOnPressListener(Key.A, () => player_game.Play(Moves.Rotate180));

        FConsole.AddOnHoldListener(Key.Down, () => player_game.Play(Moves.SoftDrop), 0, 16);
        FConsole.AddOnPressListener(Key.Space, () => player_game.Play(Moves.HardDrop));

        FConsole.AddOnPressListener(Key.C, () => player_game.Play(Moves.Hold));
        FConsole.AddOnPressListener(Key.R, () => player_game.Restart());
    }

    static void FrameEndCallback()
    {
        // Wait for games to be ready
        while (!Game.AllReady()) Thread.Sleep(0);
        // Aquire lock
        while (Interlocked.Exchange(ref Game.GamesLock, 1) == 1) Thread.Sleep(0);

        foreach (Game game in Game.Games) game.TickAsync();

        // Release lock
        Interlocked.Exchange(ref Game.GamesLock, 0);
    }

}

