namespace NEAT;

using System.Reflection;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections;

public class NN
{
    internal enum ActivationTypes : byte
    {
        Sigmoid,
        TanH,
        ReLU,
        LeakyReLU,
        ISRU,
        ELU,
        SELU,
        GELU,
        SoftPlus
    }

    internal struct PopData
    {
        public int Gen;
        public float CompatTresh;
        public NNData[] NNData;
    }

    internal struct NNData
    {
        public string Name;
        public bool Played;
        public ushort Inputs, Outputs;
        public short Age;
        public float Fitness;
        public float Mu, Delta;
        public List<ConnectionData> Connections;
        public List<ActivationTypes> Activations;

        public NNData(NN network)
        {
            if (network.InputCount <= 0 || network.OutputCount <= 0)
                throw new ArgumentException("Invalid count of input or output nodes");

            Name = network.Name;
            Played = network.Played;
            Inputs = (ushort)network.InputCount;
            Outputs = (ushort)network.OutputCount;
            Age = (short)network.Age;
            Fitness = (float)network.Fitness;
            Mu = (float)network.Mu;
            Delta = (float)network.Delta;
            Connections = new List<ConnectionData>();
            foreach (Connection c in network.Connections.Values)
            {
                if (c.Input < 0 || c.Output < 0)
                    throw new ArgumentException("Invalid connection found");

                Connections.Add(new ConnectionData
                {
                    Enabled = c.Enabled,
                    Input = (ushort)c.Input,
                    Output = (ushort)c.Output,
                    Weight = (float)c.Weight
                });
            }
            Activations = new List<ActivationTypes>();
            foreach (Node n in network.Nodes) Activations.Add(ToFuncType(n.Activation));
        }
    }

    internal struct ConnectionData
    {
        public bool Enabled;
        public ushort Input, Output;
        public float Weight;
    }

    private class Node
    {
        public List<Node> Network;
        public double Value = 0;
        public readonly int Id;
        public List<Connection> Inputs, Outputs;
        public Func<double, double> Activation;

        public Node(int id, List<Node> network, Func<double, double> activationType, List<Connection> inputs = null, List<Connection> outputs = null)
        {
            Id = id;
            Network = network;
            Inputs = inputs ?? new List<Connection>();
            Outputs = outputs ?? new List<Connection>();
            Activation = activationType;
        }

        public double UpdateValue()
        {
            // sum activations * weights
            Value = 0;
            foreach (Connection c in Inputs)
                if (c.Enabled)
                    Value += Network[c.Input].Value * c.Weight;
            Value = Activation(Value);

            return Value;
        }
    }

    private class Connection
    {
        public bool Enabled;
        public readonly int Id;
        public readonly int Input, Output;
        public double Weight;

        public Connection(int input, int output, double weight, bool enabled = true)
        {
            Input = input;
            Output = output;
            Weight = weight;
            Enabled = enabled;
            // Assign the id according to the input and output nodes
            Id = -1;
            for (int i = 0; i < InNodes.Count && Id == -1; i++)
                if (InNodes[i] == Input && OutNodes[i] == Output)
                    Id = i;
            if (Id == -1)
            {
                Id = InNodes.Count;
                InNodes.Add(Input);
                OutNodes.Add(Output);
            }
        }

        public Connection(ConnectionData data) : this(data.Input, data.Output, data.Weight, data.Enabled) { }

        public Connection Clone()
        {
            Connection clone = new Connection(Input, Output, Weight);
            if (!Enabled) clone.Enabled = false;
            return clone;
        }
    }

    #region // Hyperparameters
    // Activation
    public static readonly Func<double, double> INPUT_ACTIVATION = ReLU;
    public static readonly Func<double, double> HIDDEN_ACTIVATION = ReLU;
    public static readonly Func<double, double> OUTPUT_ACTIVATION = ISRU;
    //public const bool ALLOW_RECURSIVE = false;
    // Crossover & speciation
    public const int SPECIES_TARGET = 6, SPECIES_TRY_TIMES = 20;
    public const double COMPAT_MOD = 1.1;
    public const double WEIGHT_DIFF_COE = 1, EXCESS_COE = 2;
    public const double ELITE_PERCENT = 0.4;
    // Population
    public const double FITNESS_TARGET = double.PositiveInfinity;
    public const int MAX_GENERATIONS = -1; // Leave maxgen as -1 for unlimited
    // Mutation
    public const bool BOUND_WEIGHTS = false, ALLOW_RECURSIVE_CON = false;
    public const double MUTATE_POW = 3;
    public const double CON_PURTURB_CHANCE = 0.8, CON_ENABLE_CHANCE = 0.1, CON_ADD_CHANCE = 0.1, NODE_ADD_CHANCE = 0.02;
    public const int TRY_ADD_CON_TIMES = 20;
    #endregion

    public const string NN_FILE_END = ".nn";
    public const string POPULATION_FILE_END = ".nnpop";

    static readonly Random Rand = new();
    static readonly List<int> InNodes = new List<int>(), OutNodes = new List<int>();

    public readonly string Name = GenerateName();
    public bool Played = false;
    public readonly int InputCount;
    public readonly int OutputCount;
    public int Age { get; private set; } = 0;
    public double Fitness = 0;
    public double Mu, Delta;
    private readonly List<Node> Nodes = new List<Node>();
    public bool[] Visited { get; private set; }
    private readonly List<int> ConnectionIds = new List<int>();
    private readonly Dictionary<int, Connection> Connections = new Dictionary<int, Connection>();

    private NN(int inputs, int outputs)
    {
        InputCount = inputs;
        OutputCount = outputs;
        // Make input nodes
        for (int i = 0; i < InputCount + 1; i++)
        {
            // Connect every input to every output
            List<Connection> outs = new();
            for (int j = 0; j < OutputCount; j++)
            {
                outs.Add(new(i, InputCount + j + 1, GaussRand()));
                Connections.Add(outs[j].Id, outs[j]);
                ConnectionIds.Add(outs[j].Id);
            }
            Nodes.Add(new Node(i, Nodes, INPUT_ACTIVATION, outputs: new(outs)));
        }
        // Make output nodes
        for (int i = 0; i < OutputCount; i++)
        {
            List<Connection> ins = new();
            for (int j = 0; j < InputCount + 1; j++)
                ins.Add(Connections[ConnectionIds[OutputCount * j + i]]);
            Nodes.Add(new(InputCount + i + 1, Nodes, OUTPUT_ACTIVATION, inputs: new List<Connection>(ins)));
        }
        // Find all connected nodes
        FindConnectedNodes();
    }

    private NN(int inputs, int outputs, List<Connection> connections)
    {
        InputCount = inputs;
        OutputCount = outputs;
        foreach (Connection c in connections)
        {
            // Add connection to connection tracking lists
            Connection newc = c.Clone();
            ConnectionIds.Add(newc.Id);
            Connections.Add(newc.Id, newc);
            // Add nodes as nescessary
            while (Nodes.Count <= newc.Input || Nodes.Count <= newc.Output)
                Nodes.Add(new Node(Nodes.Count, Nodes, Nodes.Count <= inputs ? INPUT_ACTIVATION :
                                                       Nodes.Count <= inputs + outputs ? OUTPUT_ACTIVATION :
                                                                                         HIDDEN_ACTIVATION));
            // Add connection to coresponding nodes
            Nodes[c.Input].Outputs.Add(newc);
            Nodes[c.Output].Inputs.Add(newc);
        }
        // Find all connected nodes
        FindConnectedNodes();
    }

    private NN(NNData data) : this(data.Inputs, data.Outputs, data.Connections.Select(x => new Connection(x)).ToList())
    {
        Name = data.Name;
        Played = data.Played;
        Age = data.Age;
        Fitness = data.Fitness;
        Mu = data.Mu;
        Delta = data.Delta;
        if (data.Activations != null)
            for (int i = 0; i < data.Activations.Count; i++)
                Nodes[i].Activation = ToFunc(data.Activations[i]);
    }

    private static string GenerateName()
    {
        string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x" };
        string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };

        string name = "";
        int len = Rand.Next(8) + 4;
        while (name.Length < len)
        {
            name += consonants[Rand.Next(consonants.Length)];
            name += vowels[Rand.Next(vowels.Length)];
        }

        //name = (name[0] + 32) + name.Substring(1);
        Span<char> name_span = name.ToCharArray();
        name_span[0] = (char)(name_span[0] - 32);

        return name_span.ToString();
    }

    public double[] FeedFoward(double[] input)
    {
        // Set input nodes
        for (int i = 0; i < InputCount; i++)
            if (Visited[i])
                Nodes[i].Value = input[i];
        Nodes[InputCount].Value = 1; // Bias node
        // Update hidden all nodes
        for (int i = InputCount + OutputCount + 1; i < Nodes.Count; i++)
            if (Visited[i]) Nodes[i].UpdateValue();
        // Update ouput nodes and get output
        double[] output = new double[OutputCount];
        for (int i = 0; i < OutputCount; i++) output[i] = Nodes[i + InputCount + 1].UpdateValue();

        return output;
    }

    private void FindConnectedNodes()
    {
        Visited = new bool[Nodes.Count];
        for (int i = InputCount + 1; i < InputCount + OutputCount + 1; i++)
            if (!Visited[i])
                VisitDownstream(i);

        void VisitDownstream(int i)
        {
            Visited[i] = true;
            foreach (Connection c in Nodes[i].Inputs)
                if (!Visited[c.Input] && c.Enabled)
                    VisitDownstream(c.Input);
        }
    }

    public NN MateWith(NN other)
    {
        // Other NN should be fitter
        if (this.Fitness > other.Fitness)
            return other.MateWith(this);

        // Clone
        NN child = other.Clone();
        // Corss
        foreach (int c in child.Connections.Keys)
        {
            if (Connections.ContainsKey(c))
            {
                // 50% of the time change weights
                if (Rand.NextDouble() < 0.5) child.Connections[c].Weight = Connections[c].Weight;
                // 10% of the time make enabled same as less fit one
                if (Rand.NextDouble() < 0.1) child.Connections[c].Enabled = Connections[c].Enabled;
            }
        }
        // Mutate
        child.Mutate();
        return child;
    }

    public NN Mutate()
    {
        // Mutate connections
        foreach (Connection c in Connections.Values)
        {
            // Mutate the weight
            if (Rand.NextDouble() < CON_PURTURB_CHANCE)
            {
                if (Rand.NextDouble() < 0.1) c.Weight = GaussRand();                           // 10% chance to completely change weight
                else if (Rand.NextDouble() < 0.5) c.Weight += UniformRand() / 25 * MUTATE_POW; // 45% chance to slightly change weight
                else c.Weight *= 1 + (UniformRand() / 40 * MUTATE_POW);                        // 45% chance to slightly scale weight
            }
            // Enable/disable connection
            if (Rand.NextDouble() < CON_ENABLE_CHANCE) c.Enabled = !c.Enabled;
            // Keep weight within bounds
            #if BOUND_WEIGHTS
            c.Weight = c.Weight < 0 ? Math.Max(c.Weight, -MUTATE_POW * 2) : Math.Min(c.Weight, MUTATE_POW * 2);
            #endif
        }

        // Add connection between existing nodes
        if (Rand.NextDouble() < CON_ADD_CHANCE)
        {
            for (int i = 0; i < TRY_ADD_CON_TIMES; i++)
            {
                int inid = Rand.Next(Nodes.Count), outid = Rand.Next(Nodes.Count);
                if (outid == inid) outid = (outid + 1) % Nodes.Count;
                Connection newcon = new Connection(inid, outid, GaussRand()), reversecon = new Connection(outid, inid, 0);
                if (!Connections.ContainsKey(newcon.Id) && (ALLOW_RECURSIVE_CON || !Connections.ContainsKey(reversecon.Id)))
                {
                    Nodes[inid].Outputs.Add(newcon);
                    Nodes[outid].Inputs.Add(newcon);
                    Connections.Add(newcon.Id, newcon);
                    ConnectionIds.Add(newcon.Id);
                    break;
                }
            }
        }

        // Add node between onto existing connection
        if (Rand.NextDouble() < NODE_ADD_CHANCE)
        {
            // Original connection to split
            Connection breakcon = Connections[ConnectionIds[Rand.Next(Connections.Count)]];
            for (int i = 0; i < TRY_ADD_CON_TIMES - 1 && !breakcon.Enabled; i++) breakcon = Connections[ConnectionIds[Rand.Next(Connections.Count)]];
            // Disable original connection
            breakcon.Enabled = false;
            // Insert node inbetween
            Connection incon = new Connection(breakcon.Input, Nodes.Count, breakcon.Weight), outcon = new Connection(Nodes.Count, breakcon.Output, 1);
            Connections.Add(incon.Id, incon);
            Connections.Add(outcon.Id, outcon);
            ConnectionIds.Add(incon.Id);
            ConnectionIds.Add(outcon.Id);
            Nodes[breakcon.Input].Outputs.Add(incon);
            Nodes[breakcon.Output].Inputs.Add(outcon);
            Nodes.Add(new Node(Nodes.Count, Nodes, HIDDEN_ACTIVATION, new List<Connection>() { incon }, new List<Connection>() { outcon }));
        }

        // Find all connected nodes
        FindConnectedNodes();

        return this;
    }

    public bool SameSpecies(NN other, double compat_tresh)
    {
        int matching = 0, large_genome_norm = Math.Max(1, Math.Max(Connections.Count, other.Connections.Count) - 20);
        double weight_diff = 0;
        // Go through each connection and see if it is excess or matching
        foreach (int conid in Connections.Keys)
        {
            if (other.Connections.ContainsKey(conid))
            {
                double weight = Connections[conid].Enabled ? Connections[conid].Weight : 0, other_weight = other.Connections[conid].Enabled ? other.Connections[conid].Weight : 0;
                weight_diff += Math.Abs(weight - other_weight);
                matching++;
            }
        }
        // Return whether or not they're the same species
        if (matching == 0) return EXCESS_COE * (Connections.Count + other.Connections.Count - 2 * matching) / large_genome_norm < compat_tresh;
        else return (WEIGHT_DIFF_COE * weight_diff / matching) + (EXCESS_COE * (Connections.Count + other.Connections.Count - 2 * matching) / large_genome_norm) < compat_tresh;
    }

    public int GetSize() => Visited.Count(x => x) + Connections.Values.Count(x => x.Enabled);

    public NN Clone() => new NN(InputCount, OutputCount, new List<Connection>(Connections.Values));

    public static void SaveNN(string path, NN network)
    {
        if (path.EndsWith(NN_FILE_END))
        {
            SaveNNBin(path, network);
            return;
        }

        NNData data = new NNData(network);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static void SaveNNBin(string path, NN network, bool remove_unused = false) =>
        File.WriteAllBytes(path, ToByteArray(network, remove_unused));

    public static void SaveNNs(string path, NN[] networks, int gen, double compat_tresh)
    {
        if (path.EndsWith(POPULATION_FILE_END))
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(gen));
            bytes.AddRange(BitConverter.GetBytes((float)compat_tresh));
            bytes.AddRange(BitConverter.GetBytes(networks.Length));
            foreach (NN network in networks) bytes.AddRange(ToByteArray(network));
            File.WriteAllBytes(path, bytes.ToArray());
        }
        else
        {
            NNData[] networks_data = networks.Select(x => new NNData(x)).ToArray();
            PopData training_data = new PopData
            {
                Gen = gen,
                CompatTresh = (float)compat_tresh,
                NNData = networks_data
            };
            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
            string json = JsonSerializer.Serialize(training_data, options);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
    }

    public static NN LoadNN(string path)
    {
        if (path.EndsWith(NN_FILE_END))
        {
            return LoadNNBin(path);
        }

        string jsonString = File.ReadAllText(path);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true, AllowTrailingCommas = true };
        NNData data = JsonSerializer.Deserialize<NNData>(jsonString, options);

        return new NN(data);
    }

    private static NN LoadNNBin(string path) => FromByteArray(File.ReadAllBytes(path), out int _);

    public static NN[] LoadNNs(string path, out int gen, out double compat_tresh)
    {
        if (path.EndsWith(POPULATION_FILE_END))
        {
            byte[] bytes = File.ReadAllBytes(path);
            gen = BitConverter.ToInt32(bytes, 0);
            compat_tresh = BitConverter.ToSingle(bytes, 4);
            int pop_count = BitConverter.ToInt32(bytes, 8);
            NN[] networks = new NN[pop_count];
            int offset = 12;
            for (int i = 0; i < pop_count; i++)
            {
                networks[i] = FromByteArray(bytes, out int size, offset);
                offset += size;
            }
            return networks;
        }
        else
        {
            string jsonString = File.ReadAllText(path);
            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
            PopData training_data = JsonSerializer.Deserialize<PopData>(jsonString, options)!;
            gen = training_data.Gen;
            compat_tresh = training_data.CompatTresh;
            NN[] networks = training_data.NNData
                                         .Select(x => new NN(x))
                                         .ToArray();

            return networks;
        }
    }

    private static byte[] ToByteArray(NN network, bool remove_unused = false)
    {
        List<byte> bytes = new List<byte>();
        NNData data = new NNData(network);
        int connection_count = data.Connections.Count(x => !remove_unused | x.Enabled);
        BitList bools = new BitList(connection_count + 1);

        // Add name
        bytes.Add((byte)data.Name.Length);
        bytes.AddRange(data.Name.ToArray().Select(x => (byte)x));
        // Add input and output count
        bytes.AddRange(BitConverter.GetBytes(data.Inputs));
        bytes.AddRange(BitConverter.GetBytes(data.Outputs));
        // Add played and age
        bools.Add(data.Played);
        bytes.AddRange(BitConverter.GetBytes(data.Age));
        // Add fitness, mu and delta
        bytes.AddRange(BitConverter.GetBytes(data.Fitness));
        bytes.AddRange(BitConverter.GetBytes(data.Mu));
        bytes.AddRange(BitConverter.GetBytes(data.Delta));
        // Add connections
        bytes.AddRange(BitConverter.GetBytes((ushort)connection_count));
        foreach (var c in data.Connections)
        {
            if (remove_unused && !c.Enabled) continue;

            bools.Add(c.Enabled);
            bytes.AddRange(BitConverter.GetBytes(c.Input));
            bytes.AddRange(BitConverter.GetBytes(c.Output));
            bytes.AddRange(BitConverter.GetBytes(c.Weight));
        }
        // Add activations
        bytes.AddRange(BitConverter.GetBytes((ushort)data.Activations.Count));
        bytes.AddRange(data.Activations.Select(x => (byte)x));
        // Add played and enabled bools as a bit array
        bytes.AddRange(bools.GetBytes());

        return bytes.ToArray();
    }

    private static NN FromByteArray(byte[] bytes, out int size, int offset = 0)
    {
        NNData data = new NNData();
        int i = offset;
        // Get name
        int name_length = bytes[i++];
        data.Name = Encoding.UTF8.GetString(bytes, i, name_length);
        i += name_length;
        // Get input and output count
        data.Inputs = BitConverter.ToUInt16(bytes, i);
        i += 2;
        data.Outputs = BitConverter.ToUInt16(bytes, i);
        i += 2;
        // Get age
        data.Age = BitConverter.ToInt16(bytes, i);
        i += 2;
        // Get fitness, mu and delta
        data.Fitness = BitConverter.ToSingle(bytes, i);
        i += 4;
        data.Mu = BitConverter.ToSingle(bytes, i);
        i += 4;
        data.Delta = BitConverter.ToSingle(bytes, i);
        i += 4;
        // Get connections
        int connection_count = BitConverter.ToUInt16(bytes, i);
        i += 2;
        int activation_count = BitConverter.ToUInt16(bytes, i + connection_count * 8);
        BitList bools = new BitList(bytes[(i + connection_count * 8 + activation_count + 2)..], connection_count + 1);
        data.Played = bools[0];
        data.Connections = new List<ConnectionData>(connection_count);
        for (int j = 0; j < connection_count; j++)
        {
            var c = new ConnectionData();
            c.Enabled = bools[j + 1];
            c.Input = BitConverter.ToUInt16(bytes, i);
            i += 2;
            c.Output = BitConverter.ToUInt16(bytes, i);
            i += 2;
            c.Weight = BitConverter.ToSingle(bytes, i);
            i += 4;
            data.Connections.Add(c);
        }
        // Get activations
        activation_count = BitConverter.ToUInt16(bytes, i);
        i += 2;
        data.Activations = new List<ActivationTypes>(activation_count);
        for (int j = 0; j < activation_count; j++)
            data.Activations.Add((ActivationTypes)bytes[i++]);
        i += bools.ByteCount;

        size = i - offset;
        return new NN(data);
    }

    public static NN[] Train(string path, Action<NN[], int, double> fitness_func, int inputs, int outputs, int pop_size)
    {
        const int SAVE_EVERY = 5;

        // Initialise/load NNs
        int gen = 0;
        double compat_tresh = 0.5;
        NN[] NNs = new NN[pop_size];
        if (!path.EndsWith('\\'))
            path += '\\';
        Directory.CreateDirectory(path);
        string population_file = Directory.GetFiles(path)
            .Where(f => f[f.LastIndexOf('\\')..f.LastIndexOf('.')] == "\\current")
            .FirstOrDefault(string.Empty);
        if (population_file != string.Empty)
        {
            NNs = LoadNNs(population_file, out gen, out compat_tresh);
            // Add extra NNs if too few
            if (NNs.Length < pop_size)
            {
                var extra = new NN[pop_size - NNs.Length].Select(x => new NN(inputs, outputs));
                NNs = NNs.Concat(extra).ToArray();
            }
            else pop_size = NNs.Length;
        }
        else
        {
            for (int i = 0; i < pop_size; i++)
                NNs[i] = new NN(inputs, outputs);
        }
        SaveNNs(path + @"current.json", NNs, gen, compat_tresh);

        // Train
        for (; gen < MAX_GENERATIONS || MAX_GENERATIONS == -1;)
        {
            // Update fitness and age
            fitness_func(NNs, gen, compat_tresh);
            foreach (NN n in NNs) n.Age++;
            NNs = NNs.OrderByDescending(x => x.Fitness).ToArray();
            if (NNs[^1].Fitness < 0) throw new Exception("Fitness must be not less than 0!");

            // Speciate
            List<List<NN>> species = Speciate(NNs, compat_tresh);
            for (int i = 0; i < SPECIES_TRY_TIMES; i++)
            {
                // Adjust compattresh
                if (species.Count < SPECIES_TARGET) compat_tresh /= Math.Pow(COMPAT_MOD, 1 - (double)i / SPECIES_TRY_TIMES);
                else if (species.Count > SPECIES_TARGET) compat_tresh *= Math.Pow(COMPAT_MOD, 1 - (double)i / SPECIES_TRY_TIMES);
                else break;
                species = Speciate(NNs, compat_tresh);
            }
            species = species.OrderByDescending(s => s.Average(x => x.Fitness)).ToList();

            // Explicit fitness sharing
            // Find average fitnesses
            double avg_fit = NNs.Average(x => x.Fitness);
            List<double> sp_avg_fits = species.ConvertAll(s => s.Average(x => x.Fitness));
            // Calculate amount of babies
            int[] species_babies = new int[species.Count];
            for (int i = 0; i < species_babies.Length; i++)
            {
                if (avg_fit == 0)
                    species_babies[i] = species[i].Count;
                else
                    species_babies[i] = (int)(sp_avg_fits[i] / avg_fit * species[i].Count);
            }
            // If there is space for extra babies, distribute babies to the fittest species first
            for (int i = 0; species_babies.Sum() < pop_size; i++)
                species_babies[i]++;

            // Return if target reached
            if (NNs.Max(x => x.Fitness) >= FITNESS_TARGET)
            {
                SaveNNs(path, NNs, gen, compat_tresh);
                return NNs;
            }

            // Make save file before mating
            SaveNNs(path + @$"gen {gen}.json", NNs, gen, compat_tresh);
            if (File.Exists(path + $@"gen {gen - 1}.json"))
                File.Delete(path + $@"gen {gen - 1}.json");
            if (gen % SAVE_EVERY == 0)
                SaveNNs(path + @$"gen {gen}" + POPULATION_FILE_END, NNs, gen, compat_tresh);

            // Mating season
            List<NN> new_NNs = new List<NN>();
            for (int i = 0; i < species.Count; i++)
            {
                // Only top n% can reproduce
                species[i] = species[i].OrderByDescending(x => x.Fitness).ToList();
                List<NN> elites = species[i].GetRange(0, (int)Math.Ceiling(species[i].Count * ELITE_PERCENT));

                // Add best from species without mutating
                if (species_babies[i] > 0) new_NNs.Add(elites[0]);

                // Prepare roulette wheel (fitter parents have higher chance of mating)
                double[] wheel = new double[elites.Count];
                wheel[0] = elites[0].Fitness;
                for (int j = 1; j < wheel.Length; j++)
                    wheel[j] = wheel[j - 1] + elites[j].Fitness;

                // Make babeeeeeee
                for (int j = 0; j < species_babies[i] - 1; j++)
                {
                    // Spin the wheel
                    int index = 0;
                    double speen = Rand.NextDouble() * wheel[^1];
                    while (wheel[index] <= speen)
                    {
                        if (++index == wheel.Length)
                        {
                            index = 0;
                            break;
                        }
                    }
                    NN parent1 = elites[index];

                    index = 0;
                    speen = Rand.NextDouble() * wheel[^1];
                    while (wheel[index] <= speen)
                    {
                        if (++index == wheel.Length)
                        {
                            index = 0;
                            break;
                        }
                    }
                    NN parent2 = elites[index];

                    // Uwoooogh seeeeegs
                    new_NNs.Add(parent1.MateWith(parent2));
                }
            }

            // Replace old NNs with new NNs and set all NNs to unplayed
            new_NNs.CopyTo(NNs);
            foreach (NN nn in NNs)
                nn.Played = false;

            // Save new generation
            gen++;
            SaveNNs(path + @"current.json", NNs, gen, compat_tresh);
        }

        return NNs;
    }

    public static List<List<NN>> Speciate(NN[] networks, double compat_tresh)
    {
        List<List<NN>> species = new List<List<NN>>
        {
            new List<NN> { networks[0] }
        };
        for (int i = 1; i < networks.Length; i++)
        {
            bool existing_sp = false;
            NN nn = networks[i];
            foreach (List<NN> s in species)
            {
                if (nn.SameSpecies(s[0], compat_tresh))
                {
                    s.Add(nn);
                    existing_sp = true;
                    break;
                }
            }
            if (!existing_sp) species.Add(new List<NN> { nn });
        }

        return species;
    }

    static double GaussRand()
    {
        double output = 0;
        for (int i = 0; i < 6; i++) output += Rand.NextDouble();
        //return (output - 3) / 3;
        return Math.FusedMultiplyAdd(output, 1D / 3D, -1);
    }
    static double UniformRand() => Math.FusedMultiplyAdd(Rand.NextDouble(), 2, -1);
    // Most activation functions here may not be so useful as there were made with the vanishing/exploding gradient problem in mind
    // But hey, no harm having them at our disposal
    #region // Activation functions
    static Func<double, double> ToFunc(ActivationTypes type)
    {
        MethodInfo[] methods = typeof(NN).GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
        foreach (MethodInfo method in methods)
        {
            if (method.Name == type.ToString())
                return (Func<double, double>)Delegate.CreateDelegate(typeof(Func<double, double>), method);
        }

        throw new MissingMethodException("Activation function not found!");
    }
    static ActivationTypes ToFuncType(Func<double, double> func)
    {
        MemberInfo[] types = typeof(ActivationTypes).GetMembers();
        foreach (MemberInfo type in types)
        {
            if (type.Name == func.Method.Name)
                return (ActivationTypes)Enum.Parse(typeof(ActivationTypes), type.Name);
        }

        throw new MissingMemberException("Activation type not found!");
    }

#pragma warning disable IDE0051 // Remove unused private members
    static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
    static double TanH(double x) => Math.Tanh(x);
    static double ReLU(double x) => x >= 0 ? x : 0;
    static double LeakyReLU(double x) => x >= 0 ? x : 0.01 * x;
    static double ISRU(double x) => x >= 0 ? x : x / Math.Sqrt(Math.FusedMultiplyAdd(x, x, 1));
    static double ELU(double x) => x >= 0 ? x : Math.Exp(x) - 1;
    static double SELU(double x) => x >= 0 ? 1.050700987355480D * x : 1.050700987355480D * 1.670086994173469D * (Math.Exp(x) - 1);
    static double GELU(double x)
    {
        const double C = 0.797884560802865D; //Math.Sqrt(2 / Math.PI);
        return 0.5D * x * (1 + Math.Tanh(C * Math.FusedMultiplyAdd(0.044715D, Math.Pow(x, 3), x)));
    }
    static double SoftPlus(double x) => Math.Log(1 + Math.Exp(x));
#pragma warning restore IDE0051 // Remove unused private members
    #endregion
}

sealed class BitList : ICollection, IEnumerable, ICloneable
{
    public const byte TRUE = 1, FLASE = 0;

    private byte[] _m;
    public int Count { get; private set; }
    public int ByteCount { get => (Count - 1) / 8 + 1; }

    public bool IsSynchronized => false;
    public object SyncRoot => null;

    public bool this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            return (_m[index / 8] >> (index % 8) & 1) == TRUE;
        }
        set
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();

            if (value)
                _m[index / 8] |= (byte)(1 << (index % 8));
            else
                _m[index / 8] &= (byte)~(1 << (index % 8));
        }
    }

    public BitList()
    {
        _m = Array.Empty<byte>();
        Count = 0;
    }

    public BitList(int capacity)
    {
        _m = new byte[(capacity - 1) / 8 + 1];
        Count = 0;
    }

    public BitList(byte[] bytes, int count = -1)
    {
        Count = count == -1 ? bytes.Length * 8 : count;
        _m = bytes[..ByteCount];
    }

    public void Add(bool bit)
    {
        // Resize array if needed
        if (Count >= _m.Length * 8)
        {
            byte[] new_m = new byte[_m.Length * 2];
            _m.CopyTo(new_m, 0);
            _m = new_m;
        }

        // Add bit
        this[Count++] = bit;
    }

    public void Clear()
    {
        for (int i = 0; i < ByteCount; i++)
            _m[i] = 0;
    }

    public IEnumerator GetEnumerator() =>
        _m.SelectMany((x, index) =>
        {
            bool[] bits = new bool[Math.Min(8, Count - (index * 8))];
            for (int i = 0; i < 8; i++)
                bits[i] = this[index * 8 + i];
            return bits;
        }).GetEnumerator();

    public byte[] GetBytes() => _m.Take(ByteCount).ToArray();

    public void CopyTo(Array array, int index) => _m.CopyTo(array, index);

    public object Clone() =>
        new BitList
        {
            _m = (byte[])_m.Clone(),
            Count = Count
        };
}
