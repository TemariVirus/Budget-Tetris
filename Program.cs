namespace TetrisAI;

using FastConsole;
using System.IO;
using System.Windows.Input;
using Tetris;

// Inputs: standard height, caves, pillars, row transitions, col transitions, trash
// Outputs: score of state
// TODO:
//- Do timekeeping on a separate thread
//- Separate lines sent and lines cleared
//- add height of trash as a feature
//- add a featrue for t-spins
class Program
{
    const int MAX_LINES = 300;
    const int THINK_TIME_IN_MILLIS = 150, MOVE_DELAY_IN_MILLIS = 0;
    const int PLAY_TIMES = 3;
    const double DELTA_TRESH = 0.5;
    static readonly string Population_path = AppDomain.CurrentDomain.BaseDirectory + @"Pops\rating.txt";
        
    static void Main()
    {
        // Set up console
        Console.Title = "Tetris NEAT AI Training";
        FConsole.Framerate = 2222D / THINK_TIME_IN_MILLIS;
        FConsole.CursorVisible = false;
        FConsole.SetFont("Consolas", 18);
        FConsole.Initialise(FrameEndCallback);

        //Game[] games = new Game[2].Select(x => new Game()).ToArray();
        //Game.SetGames(games);
        //new Bot(NN.LoadNNNew(AppContext.BaseDirectory + @"NNs\plan2 new.txt"), games[0]).Start(100, 0);
        //new Bot(NN.LoadNN(AppContext.BaseDirectory + @"NNs\fixedhold vs.txt"), games[1]).Start(100, 0);

        // Train NNs
        NN.Train(Population_path, FitnessFunction, 6, 1, 30);
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

        FConsole.AddOnHoldListener(Key.Down, () => player_game.Play(Moves.SoftDrop), 0, 16);
        FConsole.AddOnPressListener(Key.Space, () => player_game.Play(Moves.HardDrop));

        FConsole.AddOnPressListener(Key.C, () => player_game.Play(Moves.Hold));
        FConsole.AddOnPressListener(Key.R, () => player_game.Restart());
        FConsole.AddOnPressListener(Key.Escape, () => Game.IsPaused = !Game.IsPaused);
    }

    static void FrameEndCallback()
    {
        if (Game.IsPaused || Game.Games == null) return;
        foreach (Game g in Game.Games)
        {
            g.Tick();
        }
    }

    // Own rating system:
    //
    // Goals:
    //  -Rating must not be less than 0
    //  -n times the rating means n times the no. of babies made
    //
    // Intuition:
    //  Babies are distributed on a "on-win" basis, so "1 win" = k babies, where k is a constant.
    //  Using the elo scale, the fitness of a nn is e^rating
    //
    //  The "true" skill of a player over one game is assumed to be random (due to random starting pieces), but normally distributed.
    //  The standard deviation of the distribution is expected to be small as the players play almost deterministically.
    //  However external factors (e.g. stating pieces) may increase the standard deviation.
    //  The player's rating is the current estimated mean of the distribution.
    //
    //  As more games are played, the mean is updated based on the result of the game, the estimated rating of the opponent,
    //  and the standard deviation.
    //  The standard deviation is also updated by an amount inversely proportional to the amount of information provided by the game.
    //  This means more shocking results decrease the standard deviation less.
    //
    // Specification:
    //  -For the following, let r denote the rating, d denote the standard deviation, and s denote the actual result (from 0 to 1).
    //  -The likelyhood of a player A winning against a player B is given by: P(A wins) = 1 / (1 + e^(rB - rA))
    //  -The "predicted" rating of a player A, given the actual result S against a player B is: rA = rB + ln(sA / (1 - sA))
    //  -new rA = rA + k * dA * (sA - P(A wins)); where k is constant and > 0
    //  -new dA = dA * (1 - decay(|sA - P(A wins)|)); where decay(x) = e^-x / (1 + e^ax); where a is constant
    //  -Having the lowest rating be as low as possible will allow for much higher ratings before hitting overflow.
    //   As ln(double.maxvalue) ~ 709.78, we will shift the ratings to make the highest one 700, then shift again to make the lowest one
    //   not smaller than -700 become -700 (ratings under -700 are considered negligible)
    //  -Start with mean of 0, and sd of 100 (~99.9999999997% fall within 7 sd of the mean)
    static void FitnessFunction(NN[] networks, int gen, double compat_tresh)
    {
        const double MinMu = -700, MaxMu = 700;
        const double K = 1, A = 4, C = 3;

        // If it's the start of a new gen, reset mu and delta
        if (!networks[0].Played)
        {
            foreach (NN network in networks)
            {
                network.Mu = 0; // Maybe we can skip resetting this
                network.Delta = 100;
            }
            networks[0].Played = true;
        }

        // Play until all NNs reach delta treshold
        while (networks.All(x => x.Delta > DELTA_TRESH))
        {
            double left_score = 0;
            networks = networks.OrderByDescending(x => x.Delta).ToArray();

            // Play 3(?) times
            for (int i = 0; i < PLAY_TIMES; i++)
            {
                // Prepare games
                int seed = Guid.NewGuid().GetHashCode();
                Game[] games = new Game[2].Select(x => new Game(5, seed)).ToArray();
                Game.SetGames(games);

                // Show info on console
                FConsole.Set(FConsole.Width, FConsole.Height + 8);
                FConsole.CursorVisible = false;
                FConsole.WriteAt($"Gen: {gen}", 1, 28);
                NN best_nn = networks.Aggregate((max, current) => (current.Mu > max.Mu) ? current : max);
                FConsole.WriteAt($"Best: {best_nn.Mu} +- {best_nn.Delta} by {best_nn.Name}", 1, 29);
                FConsole.WriteAt($"Average Fitness: {networks.Average(x => x.Mu)}", 1, 30);
                FConsole.WriteAt($"Average Size: {networks.Average(x => x.GetSize())}", 1, 31);
                FConsole.WriteAt($"No. of Species: {NN.Speciate(networks, compat_tresh).Count}", 1, 32);

                // Start bots
                Bot left = new Bot(networks[0], games[0]);
                Bot right = new Bot(networks[1], games[1]);
                left.Game.Name = networks[0].Name;
                right.Game.Name = networks[1].Name;
                Game.IsPaused = false;
                left.Start(THINK_TIME_IN_MILLIS, MOVE_DELAY_IN_MILLIS);
                right.Start(THINK_TIME_IN_MILLIS, MOVE_DELAY_IN_MILLIS);

                // Wait until one dies or game takes too long
                while (games.All(x => !x.IsDead) && games.All(x => x.Lines < MAX_LINES))
                {
                    foreach (Game g in Game.Games)
                    {
                        g.WriteAt(0, 6, ConsoleColor.White, $"Sent: {g.Sent}".PadRight(11));
                        g.WriteAt(0, 22, ConsoleColor.White, $"APL: {Math.Round(g.APL, 3)}".PadRight(10));
                    }
                    Thread.Sleep((int)(1000 / FConsole.Framerate));
                }
                // If only one is alive, wait a while to see if it kills itself
                if (left.Game.IsDead ^ right.Game.IsDead)
                {
                    Game alive = left.Game.IsDead ? right.Game : left.Game;
                    int placed = alive.PiecesPlaced;
                    while (alive.PiecesPlaced - placed < 10 && !alive.IsDead)
                    {
                        foreach (Game g in Game.Games)
                        {
                            g.WriteAt(0, 6, ConsoleColor.White, $"Sent: {g.Sent}".PadRight(11));
                            g.WriteAt(0, 22, ConsoleColor.White, $"APL: {Math.Round(g.APL, 3)}".PadRight(10));
                        }
                        Thread.Sleep((int)(1000 / FConsole.Framerate));
                    }
                }

                // End game
                Game.IsPaused = true;
                left.Stop();
                right.Stop();

                if (left.Game.IsDead ^ right.Game.IsDead)
                {
                    // If left is only one surviving, left wins
                    if (!left.Game.IsDead) left_score += 1D;
                }
                else
                {
                    // If both alive or both dead, call it a draw
                    left_score += 0.5D;
                }
            }

            // Update rating mean and std dev
            double P_left = 1 / (1 + Math.Exp(networks[1].Mu - networks[0].Mu));
            left_score /= PLAY_TIMES;
            double error_left = left_score - P_left;
            double error_right = -error_left;
            networks[0].Mu += K * networks[0].Delta * error_left;
            networks[0].Delta *= 1 - Decay(Math.Abs(error_left));
            networks[1].Mu += K * networks[1].Delta * error_right;
            networks[1].Delta *= 1 - Decay(Math.Abs(error_right));

            // Save progress
            NN.SaveNNs(Population_path, networks, gen, compat_tresh);
        }

        // Shift means so that the fittest is exactly the max
        double shift = MaxMu - networks.Max(x => x.Mu);
        foreach (NN network in networks) network.Mu += shift;
        // Any means lower than the min are too small, so they are ignored
        // Of the remaining means, we shift back down so that the least fit is excatly the min
        shift = MinMu - networks.Min(x => (x.Mu < MinMu) ? double.PositiveInfinity : x.Mu);
        foreach (NN network in networks) network.Mu += shift;
        // Fitness = e^mean
        foreach (NN network in networks)
        {
            network.Fitness = Math.Exp(network.Mu);
            network.Played = true;
        }
        return;

        
        static double Decay(double x) => Math.Exp(-x) / (C + Math.Exp(A * x));
    }
}
