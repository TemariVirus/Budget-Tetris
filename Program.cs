using FastConsole;
using NEAT;
using System.Windows.Input;
using Tetris;
using static Tetris.Game;

// Inputs: standard height, caves, pillars, row transitions, col transitions, trash, cleared, intent
// Outputs: score of state, intent(don't search further if it drops below a treshold)
//TODO:
//-add RemoveAllListeners method that uses a predicate
//-move PieceColors to Piece class
//-handle games' widths and heights chaning mid-game
static class Program
{
    static void Main()
    {
        GameManager.InitWindow();

        int seed = new Random().Next();
        Game[] games = new Game[2].Select(x => new Game(24, seed)).ToArray();
        GameManager.SetGames(games);
        FConsole.Set(FConsole.Width, FConsole.Height + 2);
        GameManager.IsPaused = true;
        Bot left = new BotOld(NN.LoadNN(GameManager.BaseDirectory + @"NNs\plan2.txt"), games[0]);
        left.Start(300, 0);
        GameManager.SetupPlayerInput(games[1]);
        //Bot right = new BotByScore(NN.LoadNN(BaseDirectory + @"NNs\Temare.txt"), games[1]);
        //right.Start(100, 0);
        GameManager.IsPaused = false;

        PCFinder pc = new PCFinder();
        FConsole.AddOnPressListener(Key.S, () => pc.ShowMode = !pc.ShowMode);
        //FConsole.AddOnPressListener(Key.W, () => pc.Wait = !pc.Wait);
        FConsole.AddOnPressListener(Key.N, () => pc.GoNext = true);
        FConsole.AddOnHoldListener(Key.N, () => pc.GoNext = true, 400, 25);
        FConsole.AddOnPressListener(Key.P, () => new Thread(() => pc.TryFindPC(games[1], out _)).Start());
    }

    static void HalfHeight()
    {
        ConsoleColor[] bedrock_row = new ConsoleColor[10].Select(x => Game.PieceColors[Piece.Bedrock]).ToArray();
        foreach (Game g in GameManager.Games)
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