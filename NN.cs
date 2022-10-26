namespace NEAT;

using System.Reflection;
using System.IO;
using System.Text.Json;

public sealed class NN
{
    public enum ActivationTypes
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

    public struct NNData
    {
        public string Name;
        public bool Played;
        public int Inputs, Outputs;
        public int Age;
        public double Fitness;
        public double Mu, Delta;
        public List<ConnectionData> Connections;
        public List<ActivationTypes> Activations;
    }

    public struct ConnectionData
    {
        public bool Enabled;
        public int Input, Output;
        public double Weight;
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

    static readonly Random Rand = new();
    static readonly List<int> InNodes = new List<int>(), OutNodes = new List<int>();

    public readonly string Name = "";
    public readonly int InputCount;
    public readonly int OutputCount;
    public double Fitness = 0;
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
        Fitness = data.Fitness;
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

    public int GetSize() => Visited.Count(x => x) + Connections.Values.Count(x => x.Enabled);

    public static NN LoadNN(string path)
    {
        string jsonString = File.ReadAllText(path);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        NNData data = JsonSerializer.Deserialize<NNData>(jsonString, options);

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
    #endregion
}
