using FastConsole;
using NEAT;
using System.Windows.Input;
using Tetris;

// Inputs: standard height, caves, pillars, row transitions, col transitions, trash, cleared, intent
// Outputs: score of state, intent(don't search further if it drops below a treshold)
//TODO:
//-add RemoveAllListeners method that uses a predicate
//-move PieceColors to Piece class
//-handle games' widths and heights chaning mid-game
//-change stuff cuz piece coords got reverted
//-fix volume
static class Program
{
    static void Main()
    {
        Game.InitWindow();

        int seed = new Random().Next();
        Game[] games = new Game[2].Select(x => new Game(24, seed)).ToArray();
        Game.SetGames(games);
        FConsole.Set(FConsole.Width, FConsole.Height + 2);
        Game.IsPaused = true;
        Bot left = new BotOld(NN.LoadNN(AppContext.BaseDirectory + @"NNs\plan2.txt"), games[0]);
        //left.Start(300, 0);
        games[1].SetupPlayerInput();
        Bot right = new BotByScore(NN.LoadNN(AppContext.BaseDirectory + @"NNs\Temare.txt"), games[1]);
        //right.Start(300, 0);
        Game.IsPaused = false;
        for (int i = 0; i < 15; i++) games[0].Play(Moves.HardDrop);

        PCFinder pc = new PCFinder();
        FConsole.AddOnPressListener(Key.S, () => pc.ShowMode = !pc.ShowMode);
        //FConsole.AddOnPressListener(Key.W, () => pc.Wait = !pc.Wait);
        FConsole.AddOnPressListener(Key.N, () => pc.GoNext = true);
        FConsole.AddOnHoldListener(Key.N, () => pc.GoNext = true, 400, 25);
        FConsole.AddOnPressListener(Key.P, () => new Thread(() =>
        {
            for (int i = 0; i < 20; i++)
                games[0].MatrixColors[i] = new ConsoleColor[10].Select(x => ConsoleColor.Black).ToArray();
            games[0].DrawAll();
            pc.TryFindPC(games[1], out _);
        }).Start());

        
        games[0].DrawAll();
    }

    static void HalfHeight()
    {
        ConsoleColor[] bedrock_row = new ConsoleColor[10].Select(x => Game.PieceColors[Piece.Bedrock]).ToArray();
        foreach (Game g in Game.Games)
        {
            for (int j = 0; j < 10; j++)
            {
                g.Matrix <<= 10;
                g.Matrix |= new MatrixMask() { LowLow = MatrixMask.FULL_LINE };
                bedrock_row.CopyTo(g.MatrixColors[j], 0);
            }
            g.CheckHeight();
            g.DrawAll();
        }
    }
}
