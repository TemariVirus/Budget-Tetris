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
    // MAX_LINES = 300
    const int MAX_LINES = 250, THINK_TIME_IN_MILLIS = 100;
    const double PLACE_COE = 0;
    static readonly string Population_path = AppDomain.CurrentDomain.BaseDirectory + @"Pops\fixedhold vs.txt";
        
    static void Main()
    {
        // Set up console
        Console.Title = "Tetris NEAT AI Training";
        FConsole.Framerate = 21;
        FConsole.CursorVisible = false;
        FConsole.SetFont("Consolas", 18);
        FConsole.Initialise(FrameEndCallback);

        //Game[] games = new Game[2].Select(x => new Game()).ToArray();
        //Game.SetGames(games);
        //new Bot(NN.LoadNNNew(AppContext.BaseDirectory + @"NNs\plan2 new.txt"), games[0]).Start(100, 0);
        //new Bot(NN.LoadNN(AppContext.BaseDirectory + @"NNs\fixedhold vs.txt"), games[1]).Start(100, 0);

        // Train NNs
        //NN.Train(Population_path, FitnessFunction, 6, 1, 30);
        
        //NN[] nns = NN.LoadNNs(Population_path, out int gen, out double compat_tresh);
        //NN.SaveNNsNew(AppContext.BaseDirectory + "test.txt", nns, gen, compat_tresh);
        string dict = AppContext.BaseDirectory + @"NNs\";
        foreach (string f in Directory.GetFiles(dict)) NN.SaveNN(f, NN.LoadNNLagacy(f));
        //NN.SaveNN(dict + "plan2.txt", NN.LoadNN(dict + "downstacking vs.txt"));
        
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

    class Player
    {
        public double Mean = 0, StdDev = 100;
        public NN NN;

        public static void Update(Player A, Player B, double result_A)
        {
            // Update eqns:
            // rA += k * dA * (sA - P(A wins)); where k is constant and > 0
            // dA *= 1 - decay(|sA - P(A wins)|) ; where decay(x) = e^-x / (c + e^ax); where a and c are constant
            // P(A wins) = 1 / (1 + e^(rB - rA))
            const double K = 1, N = 4, C = 3;

            double P_A = 1 / (1 + Math.Exp(B.Mean - A.Mean));
            double error_A = result_A - P_A;
            //double error_B = result_B - (1 - P_A);
            double error_B = -error_A; // if result_A + result_B = 1
            A.Mean += K * A.StdDev * error_A;
            A.StdDev *= 1 - Decay(Math.Abs(error_A));
            B.Mean += K * B.StdDev * error_B;
            B.StdDev *= 1 - Decay(Math.Abs(error_B));


            static double Decay(double x) => Math.Exp(-x) / (C + Math.Exp(N * x));
        }
    }

    static void FitnessFunction(NN[] networks, int gen, double compat_tresh)
    {
        // Pick up where we left off
        int current = 0;
        while (networks[current].Played)
        {
            current++;
            if (current == networks.Length) return;
        }

        // Round-robin tournament
        for (int i = current; i < networks.Length - 1; i++)
        {
            // Pick up where we left off
            int rival = i + 1;
            while (networks[rival].Played)
            {
                rival++;
                if (rival == networks.Length) break;
            }
            
            // Play against rest of networks
            for (int j = rival; j < networks.Length; j++)
            {
                // Prepare games
                int seed = Guid.NewGuid().GetHashCode();
                Game[] games = new Game[2].Select(x => new Game(5, seed)).ToArray();
                Game.SetGames(games);

                // Show info on console
                FConsole.Set(FConsole.Width, FConsole.Height + 8);
                FConsole.CursorVisible = false;
                FConsole.WriteAt($"Gen: {gen}", 1, 28);
                double max = 0, average = 0;
                int max_index = -1;
                for (int k = 0; k < i; k++)
                {
                    double fitness = networks[k].Fitness;
                    average += fitness;
                    if (fitness > max)
                    {
                        max = fitness;
                        max_index = k;
                    }
                }
                if (i != 0) average /= i;
                FConsole.WriteAt($"Best: {max} by AI no. {max_index}", 1, 29);
                FConsole.WriteAt($"Average Fitness: {average}", 1, 30);
                FConsole.WriteAt($"Average Size: {networks.Average(x => x.GetSize())}", 1, 31);
                FConsole.WriteAt($"No. of Species: {NN.Speciate(networks, compat_tresh).Count}", 1, 32);

                // Start bots
                Bot left = new Bot(networks[i], games[0]);
                Bot right = new Bot(networks[j], games[1]);
                left.Game.Name = "AI no. " + i;
                right.Game.Name = "AI no. " + j;
                Game.IsPaused = false;
                left.Start(THINK_TIME_IN_MILLIS, 0);
                right.Start(THINK_TIME_IN_MILLIS, 0);

                // Wait until one dies or game takes too long
                while (games.Any(x => !x.IsDead) && games.All(x => x.Lines < MAX_LINES))
                {
                    foreach (Game g in Game.Games)
                    {
                        g.WriteAt(0, 6, ConsoleColor.White, $"Sent: {g.Sent}".PadRight(11));
                        g.WriteAt(0, 22, ConsoleColor.White, $"APL: {Math.Round(g.APL, 3)}".PadRight(10));
                    }
                    Thread.Sleep((int)(1000 / FConsole.Framerate));
                }
                
                // Stop bots
                Game.IsPaused = true;
                left.Stop();
                right.Stop();

                // Experiment with this
                networks[i].Fitness += left.Game.Sent;
                networks[j].Fitness += right.Game.Sent;
                //networks[i].Fitness += Math.Max(0, (left.Game.IsDead ? left.Game.Sent - right.Game.Sent : left.Game.APL * MAX_MOVES - right.Game.Sent) + (left.Game.PiecesPlaced * 0.4 * PLACE_COE));
                //networks[j].Fitness += Math.Max(0, (right.Game.IsDead ? right.Game.Sent - left.Game.Sent : right.Game.APL * MAX_MOVES - left.Game.Sent) + (right.Game.PiecesPlaced * 0.4 * PLACE_COE));
                // Save progress
                networks[j].Played = true;
                NN.SaveNNs(Population_path, networks, gen, compat_tresh);
            }
            
            for (int j = i + 1; j < networks.Length; j++)
                networks[j].Played = false;
            networks[i].Played = true;
            networks[i].Fitness /= networks.Length - 1;
        }
        networks[^1].Played = true;
        networks[^1].Fitness /= networks.Length - 1;

        return;
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
    //static void NewFitnessFunction(NN[] networks, int gen, double compat_tresh)
    //{
    //    // Play until all NNs 
    //    while (true)
    //    {
    //        // Prepare games
    //        int seed = Guid.NewGuid().GetHashCode();
    //        Game[] games = new Game[2].Select(x => new Game(5, seed)).ToArray();
    //        Game.SetGames(games);

    //        // Show info on console
    //        FConsole.Set(FConsole.Width, FConsole.Height + 8);
    //        FConsole.CursorVisible = false;
    //        FConsole.WriteAt($"Gen: {gen}", 1, 28);
    //        double max = 0, average = 0;
    //        int max_index = -1;
    //        for (int k = 0; k < i; k++)
    //        {
    //            double fitness = networks[k].Fitness;
    //            average += fitness;
    //            if (fitness > max)
    //            {
    //                max = fitness;
    //                max_index = k;
    //            }
    //        }
    //        if (i != 0) average /= i;
    //        FConsole.WriteAt($"Best: {max} by AI no. {max_index}", 1, 29);
    //        FConsole.WriteAt($"Average Fitness: {average}", 1, 30);
    //        FConsole.WriteAt($"Average Size: {networks.Average(x => x.GetSize())}", 1, 31);
    //        FConsole.WriteAt($"No. of Species: {NN.Speciate(networks, compat_tresh).Count}", 1, 32);

    //        // Start bots
    //        Bot left = new Bot(networks[i], games[0]);
    //        Bot right = new Bot(networks[j], games[1]);
    //        left.Game.Name = "AI no. " + i;
    //        right.Game.Name = "AI no. " + j;
    //        Game.IsPaused = false;
    //        left.Start(THINK_TIME_IN_MILLIS, 0);
    //        right.Start(THINK_TIME_IN_MILLIS, 0);

    //        // Wait until one dies or game takes too long
    //        while (games.Any(x => !x.IsDead) && games.All(x => x.Lines < MAX_LINES))
    //        {
    //            foreach (Game g in Game.Games)
    //            {
    //                g.WriteAt(0, 6, ConsoleColor.White, $"Sent: {g.Sent}".PadRight(11));
    //                g.WriteAt(0, 22, ConsoleColor.White, $"APL: {Math.Round(g.APL, 3)}".PadRight(10));
    //            }
    //            Thread.Sleep((int)(1000 / FConsole.Framerate));
    //        }

    //        // Stop bots
    //        Game.IsPaused = true;
    //        left.Stop();
    //        right.Stop();

    //        // Experiment with this
    //        networks[i].Fitness += left.Game.Sent;
    //        networks[j].Fitness += right.Game.Sent;
            
    //        // Save progress
    //        NN.SaveNNs(Population_path, networks, gen, compat_tresh);
    //    }

    //    return;
    //}
}
