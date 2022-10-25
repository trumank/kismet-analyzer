namespace KismetAnalyzer.Dot;

interface IStatement {
    void Write(TextWriter writer);
}

public class Graph : AbstractGraph, IStatement {
    public string Type { get; set; }

    public Graph(string type) {
        Type = type;
    }

    public void Write(TextWriter writer) {
        writer.WriteLine(Type);
        writer.WriteLine("{");
        WriteStatements(writer);
        writer.WriteLine("}");
    }
}

public class Subgraph : AbstractGraph, IStatement {
    public string? Id { get; set; }

    public Subgraph(string? id = null) {
        Id = id;
    }

    public void Write(TextWriter writer) {
        if (Id != null) writer.WriteLine(AbstractGraph.EscapeId(Id));
        writer.WriteLine("{");
        WriteStatements(writer);
        writer.WriteLine("}");
    }
}

public abstract class AbstractGraph {
    public Attributes Attributes = new Attributes();
    public List<Node> Nodes { get; } = new List<Node>();
    public List<Edge> Edges { get; } = new List<Edge>();
    public List<Subgraph> Subgraphs { get; } = new List<Subgraph>();
    public Attributes GraphAttributes { get; } = new Attributes();
    public Attributes NodeAttributes { get; } = new Attributes();
    public Attributes EdgeAttributes { get; } = new Attributes();

    public virtual void WriteStatements(TextWriter writer) {
        Attributes.WriteStatements(writer);
        WriteAttributes(writer, "graph", GraphAttributes);
        WriteAttributes(writer, "node", NodeAttributes);
        WriteAttributes(writer, "edge", EdgeAttributes);
        foreach (var node in Nodes) {
            node.Write(writer);
        }
        foreach (var edge in Edges) {
            edge.Write(writer);
        }
        foreach (var subgraph in Subgraphs) {
            subgraph.Write(writer);
        }
    }

    static void WriteAttributes(TextWriter writer, string label, Attributes attributes) {
        if (attributes.Count <= 0) return;

        writer.Write($"{label} ");
        attributes.Write(writer);
    }

    public static string EscapeId(string id) {
        return $"\"{id.Replace("\"", "\\\"")}\"";
    }
}

public class Attributes : Dictionary<string, string> {
    public void Write(TextWriter writer) {
        writer.Write("[");
        writer.Write(String.Join("; ", this.Select(attr => $"{attr.Key} = \"{attr.Value}\"").ToList()));
        writer.WriteLine("]");
    }
    public void WriteStatements(TextWriter writer) {
        foreach (var attr in this) {
            writer.WriteLine($"{attr.Key} = \"{attr.Value}\";");
        }
    }
}

public class Node : IStatement {
    public string Id { get; set; }
    public Attributes Attributes { get; } = new Attributes();

    public Node(string id) {
        Id = id;
    }
    public void Write(TextWriter writer) {
        writer.Write(AbstractGraph.EscapeId(Id));
        if (Attributes.Count > 0) {
            writer.Write(" ");
            Attributes.Write(writer);
        } else {
            writer.WriteLine();
        }
    }
}
public class Edge : IStatement {
    public string A { get; set; }
    public string? ACompass { get; set; }
    public string B { get; set; }
    public string? BCompass { get; set; }
    public Attributes Attributes { get; } = new Attributes();
    public Edge(string a, string b) {
        A = a;
        ACompass = null;
        B = b;
        BCompass = null;
    }
    public Edge(string a, string aCompass, string b, string bCompass) {
        A = a;
        ACompass = aCompass;
        B = b;
        BCompass = bCompass;
    }
    public void Write(TextWriter writer) {
        writer.Write($"{AbstractGraph.EscapeId(A)}{(ACompass == null ? "" : ":" + ACompass)} -> {AbstractGraph.EscapeId(B)}{(BCompass == null ? "" : ":" + BCompass)}");
        if (Attributes.Count > 0) {
            writer.Write(" ");
            Attributes.Write(writer);
        } else {
            writer.WriteLine();
        }
    }
}
