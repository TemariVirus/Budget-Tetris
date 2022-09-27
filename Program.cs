﻿namespace TetrisAI;

using FastConsole;
using System;
using System.Windows.Input;
using Tetris;

// Inputs: standard height, caves, pillars, row transitions, col transitions, trash, cleared, intent
// Outputs: score of state, intent(don't search further if it drops below a treshold)
// TODO:
//- add height of trash as a feature
//- add a featrue for t-spins
class Program
{
    const int MAX_LINES = 250;
    const int THINK_TIME_IN_MILLIS = 150, MOVE_DELAY_IN_MILLIS = 0;
    const int PLAY_TIMES = 4;
    const double DELTA_TRESH = 0.04;
    static readonly string Population_path = AppDomain.CurrentDomain.BaseDirectory + @"Pops\double out.json";
        
    static void Main()
    {
        // Set up console
        Console.Title = "Tetris NEAT AI Training";
        FConsole.Framerate = 12;
        FConsole.CursorVisible = false;
        FConsole.SetFont("Consolas", 16);
        //FConsole.SetFont("Consolas", 40); // Biggest
        FConsole.Initialise();

        //int seed = new Random().Next();
        //Game[] games = new Game[2].Select(x => new Game(5, seed)).ToArray();
        //Game.SetGames(games);
        //FConsole.Set(FConsole.Width, FConsole.Height + 2);
        //new Bot(NN.LoadNN(AppContext.BaseDirectory + @"NNs\plan2.txt"), games[1]).Start(150, 0);
        //new Bot(NN.LoadNN(AppContext.BaseDirectory + @"NNs\plan3.txt"), games[0]).Start(150, 0);

        // Train NNs
        NN.Train(Population_path, FitnessFunction, 8, 2, 50);
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

    static void FitnessFunctionAPL(NN[] networks, int gen, double compat_tresh)
    {
        int seed = new Random().Next();
        foreach (NN network in networks)
        {
            if (network.Played) continue;

            // Prepare games
            Game game = new Game(5, seed);
            Game.SetGames(new Game[] { game });

            // Show info on console
            FConsole.Set(FConsole.Width, FConsole.Height + 8);
            FConsole.CursorVisible = false;
            FConsole.WriteAt($" Gen: {gen}\n", 0, 28);
            NN best_nn = networks.Aggregate((max, current) => (current.Played && current.Fitness > max.Fitness) ? current : max);
            FConsole.WriteLine($" Best: {best_nn.Fitness} by {best_nn.Name}");
            double avg = 0;
            if (networks.Any(x => x.Played))
                avg = networks.Where(x => x.Played).Average(x => x.Fitness);
            FConsole.WriteLine($" Average Fitness: {avg}");
            FConsole.WriteLine($" Average Size: {networks.Average(x => x.GetSize())}");
            FConsole.WriteLine($" No. of Species: {NN.Speciate(networks, compat_tresh).Count}");

            // Start bots
            NewBot bot = new NewBot(network, game);
            game.Name = network.Name;
            bot.Start(THINK_TIME_IN_MILLIS, MOVE_DELAY_IN_MILLIS);

            // Wait bot dies or game takes too long
            while (!game.IsDead && game.Lines < MAX_LINES)
                Thread.Sleep(THINK_TIME_IN_MILLIS / 2);

            // End game
            bot.Stop();
            network.Fitness = game.Sent;
            network.Played = true;

            // Save progress
            NN.SaveNNs(Population_path, networks, gen, compat_tresh);
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
    //  -Start with mean of 0, and sd of 8/3 (Chess ratings range from around 0-2800, which in our scale is roughly 0-16. With an sd of 8/3 we can expect that 99.7% of the ratings are within 3 sd)
    static void FitnessFunction(NN[] networks, int gen, double compat_tresh)
    {
        const double MinMu = -700, MaxMu = 700;
        const double K = 1, A = 2, C = 2;

        // If it's the start of a new gen, reset mu and delta
        if (networks.All(x => !x.Played))
        {
            foreach (NN network in networks)
            {
                network.Mu = 0;
                network.Delta = 8D / 3D;
            }
            networks[0].Played = true;
            NN.SaveNNs(Population_path, networks, gen, compat_tresh);
        }

        // Play until all NNs reach delta treshold
        while (networks.Any(x => x.Delta > DELTA_TRESH))
        {
            double left_score = 0;
            // First NN is the one with highest delta
            networks = networks.OrderByDescending(x => x.Delta).ToArray();
            NN left = networks[0];
            // Opponent is more likely to be of similar rating
            //NN[] yet_to_play = networks.Skip(1).Where(x => x.Delta > DELTA_TRESH * 0.9).ToArray();
            //NN right = yet_to_play[WeightedRandom(yet_to_play
            //                                      .Select(x => Math.Exp((x.Mu - left.Mu) * (left.Mu - x.Mu))) // Gaussian distribution (e^-x^2)
            //                                      .ToArray())];
            NN right = networks[WeightedRandom(networks
                                               .Skip(1) // Skip first NN
                                               .Select(x => Math.Exp((x.Mu - left.Mu) * (left.Mu - x.Mu))) // Gaussian distribution (e^-x^2)
                                               .ToArray()) + 1];

            // Play 3(?) times
            for (int i = 0; i < PLAY_TIMES; i++)
            {
                // Prepare games
                int seed = Guid.NewGuid().GetHashCode();
                Game[] games = new Game[2].Select(x => new Game(5, seed)).ToArray();
                Game.SetGames(games);
                if (gen % 2 == 0) HalfHeight();

                // Show info on console
                FConsole.Set(FConsole.Width, FConsole.Height + 8);
                FConsole.CursorVisible = false;
                FConsole.WriteAt($"Gen: {gen}\n", 1, 28);
                //NN best_nn = networks.Aggregate((max, current) => (current.Mu - current.Delta > max.Mu - max.Delta) ? current : max);
                NN best_nn = networks.Aggregate((max, current) => (current.Mu > max.Mu) ? current : max);
                FConsole.WriteLine($" Best: {best_nn.Mu} +- {best_nn.Delta} by {best_nn.Name}");
                FConsole.WriteLine($" Average Rating: {networks.Average(x => x.Mu - x.Delta)}");
                FConsole.WriteLine($" Average Size: {networks.Average(x => x.GetSize())}");
                FConsole.WriteLine($" No. of Species: {NN.Speciate(networks, compat_tresh).Count}");

                // Start bots
                NewBot left_bot = new NewBot(left, games[0]);
                NewBot right_bot = new NewBot(right, games[1]);
                left_bot.Game.Name = left.Name;
                right_bot.Game.Name = right.Name;
                Game.IsPaused = false;
                left_bot.Start(THINK_TIME_IN_MILLIS, MOVE_DELAY_IN_MILLIS);
                right_bot.Start(THINK_TIME_IN_MILLIS, MOVE_DELAY_IN_MILLIS);

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
                if (left_bot.Game.IsDead ^ right_bot.Game.IsDead)
                {
                    Game alive = left_bot.Game.IsDead ? right_bot.Game : left_bot.Game;
                    int placed = alive.PiecesPlaced;
                    while (alive.PiecesPlaced - placed < 10 && !alive.IsDead)
                        Thread.Sleep(THINK_TIME_IN_MILLIS / 2);
                }

                // End game
                Game.IsPaused = true;
                left_bot.Stop();
                right_bot.Stop();

                if (left_bot.Game.IsDead ^ right_bot.Game.IsDead)
                {
                    // If left is only one surviving, left wins
                    if (!left_bot.Game.IsDead) left_score += 1D;
                }
                else
                {
                    // If both alive or both dead, call it a draw
                    left_score += 0.5D;
                }
            }

            // Update rating mean and std dev
            double P_left = 1 / (1 + Math.Exp(right.Mu - left.Mu));
            left_score /= PLAY_TIMES;
            double error_left = left_score - P_left;
            double error_right = -error_left;
            left.Mu += K * left.Delta * error_left;
            left.Delta *= 1 - Decay(Math.Abs(error_left));
            right.Mu += K * right.Delta * error_right;
            right.Delta *= 1 - Decay(Math.Abs(error_right));

            // Save progress
            NN.SaveNNs(Population_path, networks, gen, compat_tresh);
        }

        // If range of ratings is too big or too small, we'll have to shift them
        if (networks.Min(x => x.Mu) < MinMu || networks.Max(x => x.Mu) > MaxMu)
        {
            // Shift means so that the fittest is exactly the max
            double shift = MaxMu - networks.Max(x => x.Mu);
            foreach (NN network in networks) network.Mu += shift;
            // Any means lower than the min are too small, so they are ignored
            // Of the remaining means, we shift back down so that the least fit is excatly the min
            shift = MinMu - networks.Min(x => (x.Mu < MinMu) ? double.PositiveInfinity : x.Mu);
            foreach (NN network in networks) network.Mu += shift;
        }
        // Fitness = e^mean
        foreach (NN network in networks)
        {
            network.Fitness = Math.Exp(network.Mu);
            network.Played = true;
        }
        return;

        
        static double Decay(double x) => Math.Exp(-x) / (C + Math.Exp(A * x));

        static int WeightedRandom(double[] weights)
        {
            // Make weights culmulative
            for (int i = 1; i < weights.Length; i++)
                weights[i] += weights[i - 1];

            double spin = new Random(Guid.NewGuid().GetHashCode()).NextDouble() * weights[^1];
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > spin) return i;
            }

            return 0;
        }
    }

    static void HalfHeight()
    {
        foreach (Game g in Game.Games)
        {
            int[] full_row = new int[10].Select(x => Piece.Bedrock).ToArray();
            for (int j = 30; j < 40; j++)
            {
                full_row.CopyTo(g.Matrix[j], 0);
            }
        }
    }
}
