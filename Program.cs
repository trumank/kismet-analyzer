namespace KismetAnalyzer;

using CommandLine;

using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

public class Program {
    [Verb("run", HelpText = "Generate call graph of asset")]
    class RunOptions {
        [Value(0, Required = true, MetaName = "asset path", HelpText = "Path of .uasset")]
        public string AssetPath { get; set; }
        [Value(1, Required = true, MetaName = "output path", HelpText = "Path of output directory")]
        public string OutputPath { get; set; }
    }

    [Verb("run-tree", HelpText = "Generate disassembly and graph for entire tree")]
    class RunTreeOptions {
        [Value(0, Required = true, MetaName = "content", HelpText = "Path of Content directory")]
        public string ContentPath { get; set; }
        [Value(1, Required = true, MetaName = "output", HelpText = "Path of output directory")]
        public string OutputPath { get; set; }
    }

    [Verb("copy-imports", HelpText = "Copies imports from one asset to another and returns the new index")]
    class CopyImportsOptions {
        [Value(0, Required = true, MetaName = "from", HelpText = "Path of the asset to copy imports from")]
        public string From { get; set; }
        [Value(1, Required = true, MetaName = "to", HelpText = "Path of the asset to copy imports to")]
        public string To { get; set; }
        [Value(2, Required = true, MetaName = "imports", HelpText = "Indexes of imports to copy")]
        public IEnumerable<int> Imports { get; set; }
    }

    [Verb("hierarchy", HelpText = "Generates a graph showing the class hierarchy of assets")]
    class GenerateClassHierarchyOptions {
        [Value(0, Required = true, MetaName = "content path", HelpText = "Path of Content directory")]
        public string ContentPath { get; set; }
        [Value(1, Required = true, MetaName = "output path", HelpText = "Path of output .dot file")]
        public string OutputPath { get; set; }
    }

    [Verb("merge-functions", HelpText = "Merge functions from a source asset into the start of the dest asset")]
    class MergeFunctionsOptions {
        [Value(0, Required = true, MetaName = "source", HelpText = "Path of the source asset")]
        public string SourcePath { get; set; }
        [Value(1, Required = true, MetaName = "dest", HelpText = "Path of the dest asset")]
        public string DestPath { get; set; }
    }

    [Verb("generate", HelpText = "Generate blueprint asset")]
    class GenerateOptions {
        [Value(0, Required = true, MetaName = "source", HelpText = "Path of the source asset")]
        public string SourcePath { get; set; }
        //[Value(1, Required = true, MetaName = "dest", HelpText = "Path of the dest asset")]
        //public string DestPath { get; set; }
    }

    static int Main(string[] args) {
        return Parser.Default.ParseArguments<
            RunOptions,
            RunTreeOptions,
            CopyImportsOptions,
            GenerateClassHierarchyOptions,
            MergeFunctionsOptions,
            GenerateOptions
                >(args)
            .MapResult(
                (RunOptions opts) => Summarize(opts),
                (RunTreeOptions opts) => RunTree(opts),
                (CopyImportsOptions opts) => CopyImports(opts),
                (GenerateClassHierarchyOptions opts) => GenerateClassHierarchy(opts),
                (MergeFunctionsOptions opts) => MergeFunctions(opts),
                (GenerateOptions opts) => Generate(opts),
                errs => 1);
    }
    static int Summarize(RunOptions opts) {
        UAsset asset = new UAsset(opts.AssetPath, UE4Version.VER_UE4_27);
        var fileName = Path.GetFileName(opts.AssetPath);
        var output = new StreamWriter(Path.Join(opts.OutputPath, Path.ChangeExtension(fileName, ".txt")));
        var dotOutput = new StreamWriter(Path.Join(opts.OutputPath, Path.ChangeExtension(fileName, ".dot")));
        new SummaryGenerator(asset, output, dotOutput).Summarize();
        output.Close();
        dotOutput.Close();
        return 0;
    }
    static int RunTree(RunTreeOptions opts) {
        var enumOptions = new EnumerationOptions();
        enumOptions.IgnoreInaccessible = true;
        enumOptions.RecurseSubdirectories = true;
        IEnumerable<string> files = Directory.EnumerateFiles(opts.ContentPath, "*.uasset", enumOptions);
        foreach (var assetPath in files) {
            var outputPath = Path.Join(opts.OutputPath, Path.GetRelativePath(opts.ContentPath, assetPath));
            Console.WriteLine(outputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var asset = new UAsset(assetPath, UE4Version.VER_UE4_27);
            var output = new StreamWriter(Path.ChangeExtension(outputPath, ".txt"));
            var dotOutput = new StreamWriter(Path.ChangeExtension(outputPath, ".dot"));
            new SummaryGenerator(asset, output, dotOutput).Summarize();
            output.Close();
            dotOutput.Close();
        }
        return 0;
    }
    static int GenerateClassHierarchy(GenerateClassHierarchyOptions opts) {
        var enumOptions = new EnumerationOptions();
        enumOptions.IgnoreInaccessible = true;
        enumOptions.RecurseSubdirectories = true;
        IEnumerable<string> files = Directory.EnumerateFiles(opts.ContentPath, "*.uasset", enumOptions);

        var graph = new Graph("digraph");
        graph.Attributes.Add("rankdir", "LR");

        string Sanitize(string path) {
            return path.Replace("/", "_").Replace(".", "_").Replace("-", "_");
        }

        foreach (var assetPath in files) {
            var asset = new UAsset(assetPath, UE4Version.VER_UE4_27);
            var classExport = asset.GetClassExport();
            if (classExport != null) {
                var parent = classExport.SuperStruct.ToImport(asset);

                var assetPackage = Path.Join("/Game", Path.GetRelativePath(opts.ContentPath, Path.GetDirectoryName(assetPath)), Path.GetFileNameWithoutExtension(assetPath));
                Console.WriteLine($"{assetPackage}.{classExport.ObjectName} : {parent.OuterIndex.ToImport(asset).ObjectName}.{parent.ObjectName}");
                var fullClassName = $"{assetPackage}.{classExport.ObjectName}";
                var fullParentName = $"{parent.OuterIndex.ToImport(asset).ObjectName}.{parent.ObjectName}";

                var classNode = new Graph.Node(Sanitize(fullClassName));
                classNode.Attributes["label"] = classExport.ObjectName.ToString();
                classNode.Attributes["URL"] = Path.Join("out/Content/", Path.GetRelativePath(opts.ContentPath, Path.GetDirectoryName(assetPath)), Path.ChangeExtension(Path.GetFileName(assetPath), ".svg"));
                graph.Nodes.Add(classNode);

                var edge = new Graph.Edge(Sanitize(fullClassName), Sanitize(fullParentName));
                graph.Edges.Add(edge);
            }
        }
        var output = new StreamWriter(opts.OutputPath);
        graph.Write(output);
        output.Close();
        return 0;
    }
    static int CopyImports(CopyImportsOptions opts) {
        UAsset from = new UAsset(opts.From, UE4Version.VER_UE4_27);
        UAsset to = new UAsset(opts.To, UE4Version.VER_UE4_27);

        foreach (var index in opts.Imports) {
            var newIndex = Kismet.CopyImportTo((from, FPackageIndex.FromRawIndex(index)), to);
            var i = newIndex.ToImport(to);
            Console.WriteLine($"Copied import {index} => {newIndex}: {i.ClassName}, {i.ClassPackage}, {i.ObjectName}");
        }

        to.Write(opts.To);

        return 0;
    }
    static int MergeFunctions(MergeFunctionsOptions opts) {
        UAsset source = new UAsset(opts.SourcePath, UE4Version.VER_UE4_27);
        UAsset dest = new UAsset(opts.DestPath, UE4Version.VER_UE4_27);
        foreach (var export in source.Exports) {
            if (export is FunctionExport fnSrc) {
                if (export.ObjectName.ToString().StartsWith("ExecuteUbergraph")) {
                    Console.Error.WriteLine("Ignoring ubergraph");
                    continue;
                }
                var found = false;
                foreach (var exportDest in dest.Exports) {
                    if (exportDest is FunctionExport fnDest) {
                        if (fnSrc.ObjectName.ToString().TrimStart('_') != fnDest.ObjectName.ToString().TrimStart('_')) continue;
                        Console.WriteLine($"Found matching function named {export.ObjectName}");

                        var newInst = new List<KismetExpression>();
                        //for (int i = 0; i < fnSrc.ScriptBytecode.Length; i++) {
                        var offset = 0;
                        foreach (var inst in fnSrc.ScriptBytecode) {
                            if (inst.GetType() == typeof(EX_Return)) break;
                            offset += Kismet.GetSize(inst);
                            newInst.Add(Kismet.CopyExpressionTo(inst, source, dest, fnSrc, fnDest));
                        }
                        foreach (var inst in fnDest.ScriptBytecode) {
                            Kismet.ShiftAddressses(inst, offset);
                            newInst.Add(inst);
                        }
                        fnDest.ScriptBytecode = newInst.ToArray();

                        found = true;
                        break;
                    }
                }
                if (!found) Console.Error.WriteLine($"Could not find matching function for {fnSrc.ObjectName} in dest asset");
                break;
            }
        }
        dest.Write(opts.DestPath);
        return 0;
    }
    static int Generate(GenerateOptions opts) {
        UAsset source = new UAsset(opts.SourcePath, UE4Version.VER_UE4_27);
        var generator = new BlueprintGenerator(source);
        generator.Generate();
        return 0;
    }
}
