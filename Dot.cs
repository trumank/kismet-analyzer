namespace KismetAnalyzer;

public class Graph {
    public class Node {
        public string Id { get; set; }
        public Dictionary<string, string> Attributes { get; }

        public Node(string id) {
            Id = id;
            Attributes = new Dictionary<string, string>();
        }
        public void Write(TextWriter writer) {
            writer.WriteLine(String.Format("{0} [", Id));
            WriteAttributes(writer, Attributes);
            writer.WriteLine("]");
        }
    }
    public class Edge {
        public string A { get; set; }
        public string B { get; set; }
        public Dictionary<string, string> Attributes { get; }
        public Edge(string a, string b) {
            A = a;
            B = b;
            Attributes = new Dictionary<string, string>();
        }
        public void Write(TextWriter writer) {
            writer.Write(String.Format("{0} -> {1} [", A, B));
            WriteAttributes(writer, Attributes);
            writer.WriteLine("]");
        }
    }
    public string Type { get; set; }
    public List<Node> Nodes { get; }
    public List<Edge> Edges { get; }
    public Dictionary<string, string> Attributes { get; }

    public Graph(string type) {
        Type = type;
        Nodes = new List<Node>();
        Edges = new List<Edge>();
        Attributes = new Dictionary<string, string>();
    }

    public void Write(TextWriter writer) {
        writer.WriteLine(Type);
        writer.WriteLine("{");
        WriteAttributes(writer, Attributes);
        writer.WriteLine("");
        foreach (var node in Nodes) {
            node.Write(writer);
        }
        foreach (var edge in Edges) {
            edge.Write(writer);
        }
        writer.WriteLine("}");
    }

    static void WriteAttributes(TextWriter writer, Dictionary<string, string> attributes) {
        writer.Write(String.Join("; ", attributes.Select(attr => $"{attr.Key} = \"{attr.Value}\"").ToList()));
    }
}
