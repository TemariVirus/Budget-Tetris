using FastConsole;
using NEAT;
using System.Windows.Input;
using System.Windows.Threading;
using Tetris;

// Inputs: standard height, caves, pillars, row transitions, col transitions, trash, cleared, intent
// Outputs: score of state, intent(don't search further if it drops below a treshold)
//TODO:
//-add RemoveAllListeners method that uses a predicate
static class Program
{
    static void Main()
    {
        GameManager.InitWindow();

        int seed = new Random().Next();
        Game[] games = new Game[2].Select(x => new Game(24, seed)).ToArray();
        Game.SetGames(games);
        FConsole.Set(FConsole.Width, FConsole.Height + 2);
        Game.IsPaused = true;
        Bot left = new BotOld(NN.LoadNN(GameManager.BaseDirectory + @"NNs\plan2.txt"), games[0]);
        left.Start(300, 0);
        SetupPlayerInput(games[1]);
        //Bot right = new BotByScore(NN.LoadNN(BaseDirectory + @"NNs\Temare.txt"), games[1]);
        //right.Start(100, 0);
        Game.IsPaused = false;

        PCFinder pc = new PCFinder();
        FConsole.AddOnPressListener(Key.S, () => pc.ShowMode = !pc.ShowMode);
        //FConsole.AddOnPressListener(Key.W, () => pc.Wait = !pc.Wait);
        FConsole.AddOnPressListener(Key.N, () => pc.GoNext = true);
        FConsole.AddOnHoldListener(Key.N, () => pc.GoNext = true, 400, 25);
        FConsole.AddOnPressListener(Key.P, () => new Thread(() => pc.TryFindPC(games[1], out _)).Start());
    }

    static void SetupPlayerInput(Game player_game)
    {
        player_game.SoftG = 40;
        FConsole.AddOnPressListener(Key.Left, () => player_game.Play(Moves.Left));
        //FastConsole.AddOnHoldListener(Key.Left, () => main.Play(Moves.Left), 133, 0);
        FConsole.AddOnHoldListener(Key.Left, () => player_game.Play(Moves.DASLeft), 133, 15);

        FConsole.AddOnPressListener(Key.Right, () => player_game.Play(Moves.Right));
        //FastConsole.AddOnHoldListener(Key.Right, () => main.Play(Moves.Right), 133, 0);
        FConsole.AddOnHoldListener(Key.Right, () => player_game.Play(Moves.DASRight), 133, 15);

        FConsole.AddOnPressListener(Key.Up, () => player_game.Play(Moves.RotateCW));
        FConsole.AddOnPressListener(Key.Z, () => player_game.Play(Moves.RotateCCW));
        FConsole.AddOnPressListener(Key.A, () => player_game.Play(Moves.Rotate180));

        FConsole.AddOnHoldListener(Key.Down, () => player_game.Play(Moves.SoftDrop), 0, 15);
        FConsole.AddOnPressListener(Key.Space, () => player_game.Play(Moves.HardDrop));

        FConsole.AddOnPressListener(Key.C, () => player_game.Play(Moves.Hold));
        FConsole.AddOnPressListener(Key.R, () => player_game.Restart());
        FConsole.AddOnPressListener(Key.Escape, () => Game.IsPaused = !Game.IsPaused);
        FConsole.AddOnPressListener(Key.M, () =>
        {
            Dispatcher bgmDp = Dispatcher.FromThread(BGMThread);
            bgmDp.Invoke(() => BGM.IsMuted = !BGM.IsMuted);
            player_game.IsMuted = !player_game.IsMuted;
        });
    }
    
    static void HalfHeight()
    {
        ConsoleColor[] bedrock_row = new ConsoleColor[10].Select(x => Game.PieceColors[Piece.Bedrock]).ToArray();
        foreach (Game g in Game.Games)
        {
            for (int j = 0; j < 10; j++)
            {
                g.Matrix |= new MatrixMask() { LowLow = MatrixMask.FULL_LINE };
                g.Matrix <<= 10;
                bedrock_row.CopyTo(g.MatrixColors[j], 0);
            }
            g.CheckHeight();
            g.DrawAll();
        }
    }
}