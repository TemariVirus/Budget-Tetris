using FastConsole;
using NEAT;
using System.Windows.Input;
using Tetris;

// Inputs: standard height, caves, pillars, row transitions, col transitions, trash, cleared, intent
// Outputs: score of state, intent
// TODO:
//- Death animation (lines disappear from bottom up)
//- make FConsole.AddListener methods return the listener, which can then be used to remove specific listeners
//- Make GameBase sealed and use composition?
//- Laser chess?
//- Implement move generator and order moves by intent
//- Add height of trash and incoming trash as features
//- Add a featrue for t-spins
//- Add a feature for B2B (1 for no B2B -> B2B, -1 for B2B -> no B2B, 0 for no change)
//- Seperate trash and cleared into diff categories (combo, tspins, pcs)
static class Program
{
    const int MAX_LINES = 500;
    const int MAX_PIECES = MAX_LINES * 5 / 2;
    const int THINK_TIME_MILLIS = 50, MOVE_DELAY_MILLIS = 0;
    const int PLAY_TIMES = 4;
    const double DELTA_TRESH = 0.0487; // Just less than 5% error (e^0.0487 ~ 1.0499)
    static readonly string Population_path = AppContext.BaseDirectory + @"Pops\speed double out";
    //static readonly string Population_path = AppContext.BaseDirectory + @"Pops\many in.json";
        
    static void Main()
    {
        // Set up console
        Game.InitWindow();
        // Game.InitWindow(40); // Biggest

        //int seed = new Random().Next();
        //Game[] games = new Game[2].Select(x => new Game(6, seed)).ToArray();
        //Game.SetGames(games);
        //FConsole.Set(FConsole.Width, FConsole.Height + 2);
        //Game.IsPaused = true;
        //Bot left = new BotOld(NN.LoadNN(AppContext.BaseDirectory + @"NNs\plan2.txt"), games[0]);
        //left.Start(300, 0);
        //Bot right = new BotFixedTresh(NN.LoadNN(AppContext.BaseDirectory + @"NNs\Temare.txt"), games[1]);
        //right.Start(300, 0);
        //games[1].SetupPlayerInput();
        //Game.IsPaused = false;

        //games[1].G = 0.02;
        //PCFinder pc = new PCFinder();
        //FConsole.AddOnPressListener(Key.S, () => pc.ShowMode = !pc.ShowMode);
        ////FConsole.AddOnPressListener(Key.W, () => pc.Wait = !pc.Wait);
        //FConsole.AddOnPressListener(Key.N, () => pc.GoNext = true);
        //FConsole.AddOnHoldListener(Key.N, () => pc.GoNext = true, 400, 25);
        //FConsole.AddOnPressListener(Key.P, () => new Thread(() => pc.TryFindPC(games[1], out _)).Start());

        // Train NNs
        //NN.Train(Population_path, FitnessFunctionPCless, 14, 2, 50);
        NN.Train(Population_path, FitnessFunctionPCless, 8, 2, 50);
    }

    static void FitnessFunctionTrainer(NN[] networks, int gen, double compat_tresh)
    {
        NN trainer_nn = NN.LoadNN(AppDomain.CurrentDomain.BaseDirectory + @"NNs\plan2.txt");

        foreach (NN trainee_nn in networks)
        {
            if (trainee_nn.Played) continue;
            if (trainee_nn.Age > 0 && trainee_nn.Age < 4) continue;

            trainee_nn.Fitness = 0;
            int total_pieces = 0, total_lines = 0;
            while (total_pieces < MAX_PIECES && total_lines < MAX_LINES)
            {
                // Prepare games
                int seed = Guid.NewGuid().GetHashCode();
                Game[] games = new Game[2].Select(x => new Game(5, seed)).ToArray();
                Game.SetGames(games);

                // Show info on console
                DisplayInfo(networks, gen, compat_tresh);

                // Start bots
                BotOld trainer = new BotOld(trainer_nn, games[0]); // Old trained ai
                BotFixedTresh trainee = new BotFixedTresh(trainee_nn, games[1]); // new ai
                Game.IsPaused = false;
                trainer.Start(THINK_TIME_MILLIS, MOVE_DELAY_MILLIS);
                trainee.Start(THINK_TIME_MILLIS, MOVE_DELAY_MILLIS);

                // Wait until one dies or game takes too long
                while (games.All(x => !x.IsDead)
                    && total_pieces + games[1].PiecesPlaced < MAX_PIECES
                    && total_lines + games[1].Lines < MAX_LINES)
                    Thread.Sleep(THINK_TIME_MILLIS / 2);
                total_pieces += games[1].PiecesPlaced;
                total_lines += games[1].Lines;
                trainee_nn.Fitness += games[1].Sent;
                // If only one is alive, wait a while to see if it kills itself
                Game alive = trainer.Game.IsDead ? trainee.Game : trainer.Game;
                int placed = alive.PiecesPlaced;
                while (alive.PiecesPlaced - placed < 6 && !alive.IsDead)
                    Thread.Sleep(THINK_TIME_MILLIS / 2);

                // End game
                Game.IsPaused = true;
                trainer.Stop();
                trainee.Stop();

                if (total_pieces > 50 && trainee_nn.Fitness / total_pieces < 0.01)
                    break;
            }

            // Save progress
            trainee_nn.Played = true;
            NN.SaveNNs(Population_path, networks, gen, compat_tresh);
        }

        return;
    }

    static void FitnessFunctionTrash(NN[] networks, int gen, double compat_tresh)
    {
        int seed = new Random().Next();

        networks = networks
                   .OrderByDescending(x => x.Played)
                   .ToArray();
        for (int i = 0; i < networks.Length; i++)
        {
            if (networks[i].Played) continue;

            // Prepare games
            Game.SetGames(new Game[Math.Min(4, networks.Length - i) + 1]
                          .Select(x => new Game(16, seed))
                          .ToArray());

            // Create bots
            Bot[] bots = new Bot[Game.Games.Length - 1]
                         .Select((x, index) => new BotByScore(networks[i++], Game.Games[index]))
                         .ToArray();
            Bot trainer = new BotByScore(NN.LoadNN(AppContext.BaseDirectory + @"NNs\Soqyme.json"), Game.Games[^1]);
            Game.SetGames(Game.Games);
            // Start bots
            foreach (Bot b in bots)
            {
                b.Game.TargetMode = TargetModes.None;
                b.Start(THINK_TIME_MILLIS, MOVE_DELAY_MILLIS);
            }
            trainer.Game.TargetMode = TargetModes.AllButSelf;
            trainer.Start(90, 0);

            // Show info on console
            DisplayInfo(networks, gen, compat_tresh);

            // Wait until bot dies or game takes too long
            while (bots.Any(x => !x.Game.IsDead && x.Game.PiecesPlaced < MAX_PIECES))
            {
                foreach (Bot b in bots)
                {
                    if (b.Network.Played) continue;

                    if (b.Game.PiecesPlaced >= MAX_PIECES)
                        b.Stop();
                }
                if (trainer.Game.IsDead)
                    trainer.Game.Restart();
                Thread.Sleep(THINK_TIME_MILLIS / 2);
            }

            // End game
            foreach (Bot b in bots)
            {
                b.Stop();
                b.Network.Fitness = b.Game.Sent;
                b.Network.Played = true;
            }
            trainer.Stop();

            // Save progress
            NN.SaveNNs(Population_path, networks, gen, compat_tresh);
        }
    }

    static void FitnessFunctionPCless(NN[] networks, int gen, double compat_tresh)
    {
        // Prepare games
        Queue<NN> queued = new Queue<NN>(networks.Where(nn => !nn.Played));
        Game[] games = new Game[5].Select(x => new Game(next_length: 16)).ToArray();
        Game.SetGames(games);
        Bot[] active_bots = new Bot[games.Length]
        .Select((_, i) =>
            {
                if (!queued.TryDequeue(out NN nn)) return null;

                games[i].TargetMode = TargetModes.None;
                games[i].Matrix = new MatrixMask() { LowLow = 1 };
                games[i].MatrixColors[0][^1] = Game.PieceColors[Piece.Garbage];
                games[i].CheckHeight();

                Bot bot = new BotByScore(nn, games[i]);
                bot.Start(THINK_TIME_MILLIS, MOVE_DELAY_MILLIS);

                return bot;
            })
        .ToArray();
        Game.SetGames(games);
        DisplayInfo(networks, gen, compat_tresh);
        
        // Start bots
        while (queued.Count > 0 || Game.Games.Any(g => g.IsBot && !g.IsDead && (g.PiecesPlaced < MAX_PIECES)))
        {
            // Replace dead bots with new ones
            foreach (var (game, i) in Game.Games.Select((g, i) => (g, i)))
            {
                if (!game.IsDead && game.PiecesPlaced < MAX_PIECES)
                    continue;
                if (!game.IsBot)
                    continue;
                
                // Save progress
                active_bots[i].Stop();
                active_bots[i].Network.Fitness = game.Sent;
                active_bots[i].Network.Played = true;
                NN.SaveNNs(Population_path, networks, gen, compat_tresh);

                // Get next NN
                if (!queued.TryDequeue(out NN nn)) break;

                game.Restart();
                game.TargetMode = TargetModes.None;
                game.Matrix = new MatrixMask() { LowLow = 1 };
                game.MatrixColors[0][^1] = Game.PieceColors[Piece.Garbage];
                game.CheckHeight();
                active_bots[i] = new BotByScore(nn, game);
                active_bots[i].Start(THINK_TIME_MILLIS, MOVE_DELAY_MILLIS);
            }

            Thread.Sleep(THINK_TIME_MILLIS / 2);
        }

        foreach (Bot bot in active_bots)
            bot?.Stop();
    }

    private static void DisplayInfo(NN[] networks, int gen, double compat_tresh)
    {
        FConsole.Set(FConsole.Width, FConsole.Height + 5);
        FConsole.CursorVisible = false;
        FConsole.WriteAt($" Gen: {gen}\n", 0, FConsole.Height - 6);
        NN best_nn = networks.MaxBy(x => x.Fitness);
        FConsole.WriteAt($" Best: {best_nn.Fitness} by {best_nn.Name}", 0, FConsole.Height - 5);
        double avg = 0;
        if (networks.Any(x => x.Played || x.Age > 0))
            avg = networks.Where(x => x.Played || x.Age > 0).Average(x => x.Fitness);
        FConsole.WriteAt($" Average Fitness: {avg}", 0, FConsole.Height - 4);
        FConsole.WriteAt($" Average Size: {networks.Average(x => x.GetSize())}", 0, FConsole.Height - 3);
        FConsole.WriteAt($" No. of Species: {NN.Speciate(networks, compat_tresh).Count}", 0, FConsole.Height - 2);
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
    static void FitnessFunctionVS(NN[] networks, int gen, double compat_tresh)
    {
        const double MinMu = -700, MaxMu = 700;
        const double K = 1, A = 2, C = 2;

        // If it's the start of a new gen, reset mu and delta
        if (networks.All(x => !x.Played))
        {
            foreach (NN network in networks)
            {
                network.Mu = 0;
                network.Delta = 16D / 9D; // 8/3 * 2/3 to get 2 sd or 95% of the ratings
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
                                               .Skip(1) // Skip the one already picked
                                               .Select(x => 4 * Math.Exp((x.Mu - left.Mu) * (left.Mu - x.Mu))) // Gaussian distribution (e^-4x^2)
                                               .ToArray()) + 1];

            // Play 3(?) times
            for (int i = 0; i < PLAY_TIMES; i++)
            {
                // Prepare games
                int seed = Guid.NewGuid().GetHashCode();
                Game[] games = new Game[2].Select(x => new Game(5, seed)).ToArray();
                Game.SetGames(games);
                if (i == PLAY_TIMES - 1) HalfHeight();

                // Show info on console
                FConsole.Set(FConsole.Width, FConsole.Height + 8);
                FConsole.WriteAt($"Gen: {gen}\n", 1, 28);
                NN best_nn = networks.Aggregate((max, current) => (current.Mu - current.Delta > max.Mu - max.Delta) ? current : max);
                //NN best_nn = networks.Aggregate((max, current) => (current.Mu > max.Mu) ? current : max);
                FConsole.WriteLine($" Best: {best_nn.Mu} +- {best_nn.Delta} by {best_nn.Name}");
                FConsole.WriteLine($" Average Rating: {networks.Average(x => x.Mu - x.Delta)}");
                FConsole.WriteLine($" Average Size: {networks.Average(x => x.GetSize())}");
                FConsole.WriteLine($" No. of Species: {NN.Speciate(networks, compat_tresh).Count}");

                // Start bots
                Bot left_bot = new BotByScore(left, games[0]);
                Bot right_bot = new BotByScore(right, games[1]);
                Game.IsPaused = false;
                left_bot.Start(THINK_TIME_MILLIS, MOVE_DELAY_MILLIS);
                right_bot.Start(THINK_TIME_MILLIS, MOVE_DELAY_MILLIS);

                // Wait until one dies or game takes too long
                while (games.All(x => !x.IsDead) && games.All(x => x.Lines < MAX_LINES))
                    Thread.Sleep(THINK_TIME_MILLIS / 2);
                // If only one is alive, wait a while to see if it kills itself
                if (left_bot.Game.IsDead ^ right_bot.Game.IsDead)
                {
                    Game alive = left_bot.Game.IsDead ? right_bot.Game : left_bot.Game;
                    int placed = alive.PiecesPlaced;
                    while (alive.PiecesPlaced - placed < 10 && !alive.IsDead)
                        Thread.Sleep(THINK_TIME_MILLIS / 2);
                }

                // End game
                Game.IsPaused = true;
                left_bot.Stop();
                right_bot.Stop();

                if (left_bot.Game.IsDead ^ right_bot.Game.IsDead)
                {
                    // If left is only one surviving, left wins
                    if (!left_bot.Game.IsDead) left_score += 1;
                }
                else if (left_bot.Game.IsDead && right_bot.Game.IsDead)
                    left_score += 0.5;
                else
                {
                    double apl_sum = games.Sum(x => x.APL);
                    if (apl_sum == 0) left_score += 0.5;
                    else
                    {
                        left_score += (double)games[0].APL / apl_sum;
                    }
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
