namespace KismetAnalyzer;

using Xunit;
using Dot;

public class DotTests {

    [Fact]
    public void TestEmptyGraph()
    {
        var graph = new Graph("digraph");

        var writer = new StringWriter();
        graph.Write(writer);

        Assert.Equal(@"digraph
{
}
", writer.ToString());
    }

    [Fact]
    public void TestGraphAttributes()
    {
        var graph = new Graph("digraph");

        graph.GraphAttributes["test"] = "value";

        var writer = new StringWriter();
        graph.Write(writer);

        Assert.Equal(@"digraph
{
graph [test = ""value""]
}
", writer.ToString());
    }

    [Fact]
    public void TestNodes()
    {
        var graph = new Graph("digraph");

        var node = new Node("nodeA");

        node.Attributes["color"] = "blue";

        graph.Nodes.Add(node);

        graph.GraphAttributes["test"] = "value";

        var writer = new StringWriter();
        graph.Write(writer);

        Assert.Equal(@"digraph
{
graph [test = ""value""]
nodeA [color = ""blue""]
}
", writer.ToString());
    }

    [Fact]
    public void TestSubgraph()
    {
        var graph = new Graph("digraph");

        var node = new Node("nodeA");
        node.Attributes["color"] = "blue";
        graph.Nodes.Add(node);

        var subgraph = new Subgraph();
        subgraph.Attributes["rank"] = "min";
        subgraph.Nodes.Add(new Node("nodeA"));
        graph.Subgraphs.Add(subgraph);

        var writer = new StringWriter();
        graph.Write(writer);

        Assert.Equal(@"digraph
{
nodeA [color = ""blue""]
{
rank = ""min"";
nodeA
}
}
", writer.ToString());
    }
}
