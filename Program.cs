namespace KismetAnalyzer;

using CommandLine;

using UAssetAPI;
using UAssetAPI.UnrealTypes;

public class Program {
    [Verb("run", HelpText = "Generate call graph of asset")]
    class RunOptions {
        [Value(0, Required = true, MetaName = "path", HelpText = "Path of .uasset")]
        public string AssetPath { get; set; }
        [Value(1, Required = true, MetaName = "path", HelpText = "Path of output directory")]
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

    static int Main(string[] args) {
        return Parser.Default.ParseArguments<RunOptions,RunTreeOptions,CopyImportsOptions>(args)
            .MapResult(
                (RunOptions opts) => Summarize(opts),
                (RunTreeOptions opts) => RunTree(opts),
                (CopyImportsOptions opts) => CopyImports(opts),
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
    static int CopyImports(CopyImportsOptions opts) {
        UAsset from = new UAsset(opts.From, UE4Version.VER_UE4_27);
        UAsset to = new UAsset(opts.To, UE4Version.VER_UE4_27);

        foreach (var index in opts.Imports) {
            var newIndex = CopyImportTo((from, FPackageIndex.FromRawIndex(index)), to);
            var i = newIndex.ToImport(to);
            Console.WriteLine($"Copied import {index} => {newIndex}: {i.ClassName}, {i.ClassPackage}, {i.ObjectName}");
        }

        to.Write(opts.To);

        return 0;
    }
    static FPackageIndex CopyImportTo((UAsset, FPackageIndex) import, UAsset asset) {
        for (int i = 0; i < asset.Imports.Count; i++) {
            var existing = FPackageIndex.FromImport(i);
            if (AreImportsEqual(import, (asset, existing))) return existing;
        }
        var imp = import.Item2.ToImport(import.Item1);
        if (imp.OuterIndex.IsNull()) {
            return asset.AddImport(new Import(imp.ClassPackage.ToString(), imp.ClassName.ToString(), FPackageIndex.FromRawIndex(0), imp.ObjectName.ToString(), asset));
        } else {
            return asset.AddImport(new Import(imp.ClassPackage.ToString(), imp.ClassName.ToString(), CopyImportTo((import.Item1, imp.OuterIndex), asset), imp.ObjectName.ToString(), asset));
        }
    }
    static bool AreImportsEqual((UAsset, FPackageIndex) a, (UAsset, FPackageIndex) b) {
        if (a.Item2.IsNull() && b.Item2.IsNull()) {
            return true;
        } else if (a.Item2.IsNull() || b.Item2.IsNull()) {
            return false;
        }
        var importA = a.Item2.ToImport(a.Item1);
        var importB = b.Item2.ToImport(b.Item1);
        return importA.ClassPackage == importB.ClassPackage
            && importA.ClassName == importB.ClassName
            && importA.ObjectName == importB.ObjectName
            && AreImportsEqual((a.Item1, importA.OuterIndex), (b.Item1, importB.OuterIndex));
    }
}
