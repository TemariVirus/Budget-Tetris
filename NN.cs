using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace NEAT;
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

    // Activation
    public static readonly Func<double, double> INPUT_ACTIVATION = ReLU;
    public static readonly Func<double, double> HIDDEN_ACTIVATION = ReLU;
    public static readonly Func<double, double> OUTPUT_ACTIVATION = ISRU;

    public const string NN_FILE_END = ".nn";
    public const string POPULATION_FILE_END = ".nnpop";

    static readonly List<int> InNodes = new List<int>(), OutNodes = new List<int>();

    public readonly string Name = "";
    public bool Played = false;
    public readonly int InputCount;
    public readonly int OutputCount;
    public int Age { get; private set; } = 0;
    public int Size { get => Visited.Count(x => x) + Connections.Values.Count(x => x.Enabled); }
    public double Fitness = 0;
    public double Mu, Delta;
    private readonly List<Node> Nodes = new List<Node>();
    public bool[] Visited { get; private set; }
    private readonly List<int> ConnectionIds = new List<int>();
    private readonly Dictionary<int, Connection> Connections = new Dictionary<int, Connection>();

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
            var c = new ConnectionData
            {
                Enabled = bools[j + 1],
                Input = BitConverter.ToUInt16(bytes, i)
            };
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
