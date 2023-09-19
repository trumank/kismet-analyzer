namespace KismetAnalyzer;

using System.Text.RegularExpressions;

using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

using Dot;

public class SummaryGenerator {
    public class Lines {
        public string Label { get; }
        public List<Lines> Children { get; }

        public Lines(string label) {
            Label = label;
            Children = new List<Lines>();
        }
        public Lines Add(Lines line) {
            Children.Add(line);
            return this;
        }
        public Lines Add(string line) {
            Children.Add(new Lines(line));
            return this;
        }
        public string ToString(int indent = 0) {
            var output = new StringWriter();
            output.WriteLine("".PadLeft(indent * 4) + Label);
            foreach (var child in Children) {
                output.Write(child.ToString(indent + 1));
            }
            return output.ToString();
        }
        public IEnumerable<(int, string)> GetLines() {
            yield return (0, Label);
            foreach (var child in Children) {
                foreach (var (nest, line) in child.GetLines()) {
                    yield return (nest + 1, line);
                }
            }
        }
    }
    public class Instruction {
        public uint Address { get; }
        public List<Reference> ReferencedAddresses { get; }
        public Lines Content { get; }

        public Instruction(uint address, List<Reference> referencedAddresses, Lines content) {
            Address = address;
            ReferencedAddresses = referencedAddresses;
            Content = content;
        }
    }
    public readonly struct Reference {
        public uint Address { get; }
        public ReferenceType Type { get; }
        public string? FunctionName { get; }

        public Reference(uint address, ReferenceType type, string? functionName = null) {
            Address = address;
            Type = type;
            FunctionName = functionName;
        }
    }
    public enum ReferenceType {
        Normal,
        Jump,
        JumpTrue,
        JumpFalse,
        Push,
        Skip,
        Function,
    }

    UAsset Asset;
    TextWriter Output;
    TextWriter DotOutput;
    Graph Graph;

    public SummaryGenerator(UAsset asset, TextWriter output, TextWriter dotOutput) {
        Asset = asset;
        Output = output;
        DotOutput = dotOutput;

        Graph = new Graph("digraph");
        Graph.GraphAttributes["fontname"] = "monospace";
        Graph.NodeAttributes["fontname"] = "monospace";
        Graph.EdgeAttributes["fontname"] = "monospace";
    }

    public bool Summarize() {
        var anyExport = false;
        var minRank = new Subgraph();
        minRank.Attributes["rank"] = "min";
        Graph.Subgraphs.Add(minRank);

        var classExport = Asset.GetClassExport();
        if (classExport != null) {
            anyExport = true;
            var classLines = new Lines($"ClassExport: {classExport.ObjectName}");
            classLines.Add(new Lines($"SuperStruct: {ToString(classExport.SuperStruct)}"));
            var propLines = new Lines("Properties:");

            foreach (var prop in classExport.LoadedProperties) {
                var lines = new Lines($"{prop.SerializedType.ToString()} {prop.Name.ToString()}");

                var flags = new List<string>();
                foreach (EPropertyFlags flag in Enum.GetValues(typeof(EPropertyFlags))) {
                    if (flag != EPropertyFlags.CPF_None && prop.PropertyFlags.HasFlag(flag)) {
                        flags.Add(flag.ToString());
                    }
                }
                if (flags.Count > 0) lines.Add(new Lines(String.Join("\\|", flags)));
                propLines.Add(lines);
            }
            classLines.Add(propLines);

            var classNode = new Node(classExport.ObjectName.ToString());
            classNode.Attributes["label"] = $"{{{LinesToField(classLines)}}}";
            classNode.Attributes["shape"] = "record";
            classNode.Attributes["style"] = "filled";
            classNode.Attributes["fillcolor"] = "#88ff88";
            Graph.Nodes.Add(classNode);
            minRank.Nodes.Add(new Node(classNode.Id));
        } else {
            Output.WriteLine("No ClassExport");
        }

        foreach (var export in Asset.Exports) {
            if (export is FunctionExport e) {
                anyExport = true;
                Output.WriteLine("FunctionExport " + e.ObjectName);

                string functionName = e.ObjectName.ToString();

                var functionLines = new Lines("Function " + functionName);
                foreach (var prop in e.LoadedProperties) {
                    var flags = new List<string>();
                    foreach (EPropertyFlags flag in Enum.GetValues(typeof(EPropertyFlags))) {
                        if (flag != EPropertyFlags.CPF_None && prop.PropertyFlags.HasFlag(flag)) {
                            flags.Add(flag.ToString());
                        }
                    }
                    var propLines = new Lines($"{prop.SerializedType.ToString()} {prop.Name.ToString()}");
                    if (flags.Count > 0) propLines.Add(new Lines(String.Join("\\|", flags)));
                    functionLines.Add(propLines);
                }

                var functionNode = new Node(functionName);
                functionNode.Attributes["label"] = $"{{{LinesToField(functionLines)}}}";
                functionNode.Attributes["shape"] = "record";
                functionNode.Attributes["style"] = "filled";
                functionNode.Attributes["fillcolor"] = "#ff8888";
                Graph.Nodes.Add(functionNode);
                minRank.Nodes.Add(new Node(functionName));

                var functionEdge = new Edge(functionName, functionName + "__0");
                Graph.Edges.Add(functionEdge);

                uint index = 0;
                Console.WriteLine(functionName);
                foreach (var exp in e.ScriptBytecode) {
                    var intr = Stringify(exp, ref index);
                    foreach (var (nest, line) in intr.Content.GetLines()) {
                        var prefix = nest == 0 ? intr.Address.ToString() + ": " : "";
                        Output.WriteLine(prefix.PadRight((nest + 2) * 4) + line);
                    }

                    var node = new Node($"{e.ObjectName.ToString()}__{intr.Address.ToString()}");
                    node.Attributes["label"] = $"{{{classExport.ObjectName.ToString()}::{e.ObjectName.ToString()}:{intr.Address.ToString()} | {LinesToField(intr.Content)}}}";
                    node.Attributes["shape"] = "record";
                    node.Attributes["style"] = "filled";
                    node.Attributes["fillcolor"] = "#eeeeee";

                    foreach (var reference in intr.ReferencedAddresses) {
                        var edge = new Edge(
                                $"{e.ObjectName.ToString()}__{intr.Address.ToString()}",
                                $"{reference.FunctionName ?? e.ObjectName.ToString()}__{reference.Address.ToString()}"
                            );
                        switch (reference.Type) {
                            case ReferenceType.Normal:
                                {
                                    break;
                                }
                            case ReferenceType.JumpFalse:
                                {
                                    edge.Attributes["label"] = "IF NOT";
                                    edge.Attributes["color"] = "red";
                                    edge.Attributes["arrowhead"] = "onormal";
                                    edge.ACompass = "e";
                                    break;
                                }
                            case ReferenceType.JumpTrue:
                                {
                                    edge.Attributes["label"] = "IF";
                                    break;
                                }
                            case ReferenceType.Jump:
                            case ReferenceType.Push:
                                {
                                    edge.Attributes["label"] = "PUSH STACK";
                                    edge.Attributes["color"] = "red";
                                    edge.Attributes["arrowhead"] = "onormal";
                                    edge.ACompass = "e";
                                    break;
                                }
                            case ReferenceType.Skip:
                            case ReferenceType.Function:
                                {
                                    edge.Attributes["color"] = "red";
                                    edge.Attributes["arrowhead"] = "onormal";
                                    edge.ACompass = "e";
                                    break;
                                }
                        }
                        Graph.Edges.Add(edge);
                    }
                    Graph.Nodes.Add(node);
                }
            }
        }

        Graph.Write(DotOutput);
        return anyExport;
    }

    public static string SanitizeLabel(string str) {
        return str
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace(">", "&gt;")
            .Replace("<", "&lt;")
            .Replace("\"", "\\\"");
    }

    public static string LinesToField(Lines lines) {
        var label = new StringWriter();
        foreach (var (nest, line) in lines.GetLines()) {
            label.Write("".PadRight(nest * 2, '\u00A0') + SanitizeLabel(line) + "\\l");
        }
        return label.ToString();
    }

    Instruction Stringify(KismetExpression exp, ref uint index) {
        var referencedAddresses = new List<Reference>();
        var address = index;
        var content = Stringify(exp, ref index, referencedAddresses, true);
        return new Instruction(address, referencedAddresses, content);
    }

    Lines Stringify(KismetExpression exp, ref uint index, List<Reference> referencedAddresses, bool top = false) {
        index++;
        Lines lines;
        switch (exp) {
            case EX_PushExecutionFlow e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.PushingAddress);
                    referencedAddresses.Add(new Reference(e.PushingAddress, ReferenceType.Push));
                    index += 4;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_ComputedJump e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.CodeOffsetExpression, ref index, referencedAddresses));
                    // TODO how to map out where this jumps?
                    break;
                }
            case EX_Jump e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.CodeOffset);
                    referencedAddresses.Add(new Reference(e.CodeOffset, ReferenceType.Normal));
                    index += 4;
                    break;
                }
            case EX_JumpIfNot e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.CodeOffset);
                    referencedAddresses.Add(new Reference(e.CodeOffset, ReferenceType.JumpFalse));
                    index += 4;
                    lines.Add(Stringify(e.BooleanExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.JumpTrue));
                    break;
                }
            case EX_LocalVariable e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.Variable));
                    index += 8;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_LocalOutVariable e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    index += 8;
                    lines.Add(ToString(e.Variable));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_DefaultVariable e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.Variable));
                    index += 8;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_InstanceVariable e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.Variable));
                    index += 8;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_ObjToInterfaceCast e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.ClassPtr));
                    index += 8;
                    lines.Add(Stringify(e.Target, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_InterfaceToObjCast e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.ClassPtr));
                    index += 8;
                    lines.Add(Stringify(e.Target, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_Let e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    //PrintIndent(Output, indent + 1, "value = " + ToString(e.Value)); ?? same as variable
                    index += 8;
                    lines.Add(Stringify(e.Variable, ref index, referencedAddresses));
                    lines.Add(Stringify(e.Expression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_LetObj e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.VariableExpression, ref index, referencedAddresses));
                    lines.Add(Stringify(e.AssignmentExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_LetBool e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.VariableExpression, ref index, referencedAddresses));
                    lines.Add(Stringify(e.AssignmentExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_LetWeakObjPtr e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.VariableExpression, ref index, referencedAddresses));
                    lines.Add(Stringify(e.AssignmentExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_LetValueOnPersistentFrame e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.DestinationProperty));
                    index += 8;
                    lines.Add(Stringify(e.AssignmentExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_StructMemberContext e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.StructMemberExpression));
                    index += 8;
                    lines.Add(Stringify(e.StructExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_MetaCast e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.ClassPtr));
                    index += 8;
                    lines.Add(Stringify(e.TargetExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_DynamicCast e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.ClassPtr));
                    index += 8;
                    lines.Add(Stringify(e.TargetExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_PrimitiveCast e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.ConversionType);
                    index++;
                    switch (e.ConversionType) {
                        case ECastToken.InterfaceToBool:
                        {
                            break;
                        }
                        case ECastToken.ObjectToBool:
                        {
                            break;
                        }
                        case ECastToken.ObjectToInterface:
                        {
                            index += 8;
                            // TODO InterfaceClass
                            break;
                        }
                    }
                    lines.Add(Stringify(e.Target, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_PopExecutionFlow e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    break;
                }
            case EX_PopExecutionFlowIfNot e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.BooleanExpression, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_CallMath e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.StackNode));
                    index += 8;
                    foreach (var arg in e.Parameters) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_SwitchValue e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    index += 6;
                    lines.Add(Stringify(e.IndexTerm, ref index, referencedAddresses));
                    lines.Add("OffsetToSwitchEnd = " + e.EndGotoOffset);
                    var ci = -1;
                    foreach (var c in e.Cases) {
                        ci++;
                        var nested = new Lines("case " + ci + ":");
                        nested.Add(Stringify(c.CaseIndexValueTerm, ref index, referencedAddresses));
                        index += 4;
                        nested.Add("NextCaseOffset = " + c.NextOffset);
                        nested.Add(Stringify(c.CaseTerm, ref index, referencedAddresses));
                        lines.Add(nested);
                    }
                    var defaultCase = new Lines("default:");
                    defaultCase.Add(Stringify(e.DefaultTerm, ref index, referencedAddresses));
                    lines.Add(defaultCase);
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_Self e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_TextConst e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    index++;
                    switch (e.Value.TextLiteralType) {
                        case EBlueprintTextLiteralType.Empty:
                            {
                                break;
                            }
                        case EBlueprintTextLiteralType.LocalizedText:
                            {
                                lines.Add(new Lines("SourceString = " + ReadString(e.Value.LocalizedSource, ref index)));
                                lines.Add(new Lines("LocalizedKey = " + ReadString(e.Value.LocalizedKey, ref index)));
                                lines.Add(new Lines("LocalizedNamespace = " + ReadString(e.Value.LocalizedNamespace, ref index)));
                                break;
                            }
                        case EBlueprintTextLiteralType.InvariantText:
                            {
                                lines.Add(new Lines("SourceString = " + ReadString(e.Value.InvariantLiteralString, ref index)));
                                break;
                            }
                        case EBlueprintTextLiteralType.LiteralString:
                            {
                                lines.Add(new Lines("SourceString = " + ReadString(e.Value.LiteralString, ref index)));
                                break;
                            }
                        case EBlueprintTextLiteralType.StringTableEntry:
                            {
                                index += 8;
                                lines.Add(new Lines("TableId = " + ReadString(e.Value.StringTableId, ref index)));
                                lines.Add(new Lines("TableKey = " + ReadString(e.Value.StringTableKey, ref index)));
                                break;
                            }
                    }
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_ObjectConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.Value));
                    index += 8;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_VectorConst e:
                {
                    lines = new Lines(String.Format("EX_{0} {1},{2},{3}", e.Inst, e.Value.X, e.Value.Y, e.Value.Z));
                    index += Asset.ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? 24U : 12U;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_RotationConst e:
                {
                    lines = new Lines(String.Format("EX_{0} {1},{2},{3}", e.Inst, e.Value.Pitch, e.Value.Yaw, e.Value.Roll));
                    index += Asset.ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? 24U : 12U;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_TransformConst e:
                {
                    lines = new Lines(String.Format("EX_{0} [Rot={1},{2},{3},{4}] [Pos={5},{6},{7}] [Scale={8},{9},{10}]",
                                e.Inst, e.Value.Rotation.X, e.Value.Rotation.Y, e.Value.Rotation.Z, e.Value.Rotation.W,
                                e.Value.Translation.X, e.Value.Translation.Y, e.Value.Translation.Z,
                                e.Value.Scale3D.X, e.Value.Scale3D.Y, e.Value.Scale3D.Z));
                    index += Asset.ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? 80U : 40U;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_Context e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.ObjectExpression, ref index, referencedAddresses));
                    index += 4;
                    //lines.Add("SkipOffsetForNull = " + e.Offset);
                    index += 8;
                    lines.Add(Stringify(e.ContextExpression, ref index, referencedAddresses));
                    lines.Add("RValue = " + ToString(e.RValuePointer));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_CallMulticastDelegate e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.StackNode));
                    index += 8;
                    //lines.Add("IsSelfContext = " + (Asset.GetClassExport().OuterIndex.Index == e.StackNode.Index));
                    lines.Add(Stringify(e.Delegate, ref index, referencedAddresses));
                    foreach (var arg in e.Parameters) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_LocalFinalFunction e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.StackNode));
                    index += 8;
                    foreach (var arg in e.Parameters) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;

                    if (e.Parameters.Length == 1 && e.Parameters[0] is EX_IntConst value) {
                        if (e.StackNode.IsExport()) {
                            referencedAddresses.Add(new Reference((uint) value.Value, ReferenceType.Function, e.StackNode.ToExport(Asset).ObjectName.ToString()));
                        } else {
                            Console.Error.WriteLine("WARN: Unimplemented StackNode import");
                        }
                    } else {
                        Console.Error.WriteLine("WARN: Unimplemented non-EX_IntConst");
                    }

                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_FinalFunction e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.StackNode));
                    index += 8;
                    foreach (var arg in e.Parameters) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_LocalVirtualFunction e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.VirtualFunctionName);
                    index += 12;
                    foreach (var arg in e.Parameters) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_VirtualFunction e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.VirtualFunctionName);
                    index += 12;
                    foreach (var arg in e.Parameters) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;

                    if (e.Parameters.Length == 1 && e.Parameters[0] is EX_IntConst value) {
                        if (Asset.Exports.Any(ex => ex is FunctionExport && ex.ObjectName.ToString() == e.VirtualFunctionName.ToString())) {
                            referencedAddresses.Add(new Reference((uint) value.Value, ReferenceType.Function, e.VirtualFunctionName.ToString()));
                        }
                    } else {
                        Console.Error.WriteLine("WARN: Unimplemented non-EX_IntConst");
                    }

                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_AddMulticastDelegate e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.Delegate, ref index, referencedAddresses));
                    lines.Add(Stringify(e.DelegateToAdd, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_RemoveMulticastDelegate e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.Delegate, ref index, referencedAddresses));
                    lines.Add(Stringify(e.DelegateToAdd, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_ClearMulticastDelegate e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.DelegateToClear, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_BindDelegate e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.FunctionName);
                    index += 12;
                    lines.Add(Stringify(e.Delegate, ref index, referencedAddresses));
                    lines.Add(Stringify(e.ObjectTerm, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_StructConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + ToString(e.Struct));
                    index += 8;
                    index += 4;
                    foreach (var arg in e.Value) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;

                    if (e.Struct.IsImport()) {
                        var s = e.Struct.ToImport(Asset);
                        if (s.ObjectName.ToString() == "LatentActionInfo" && s.ClassPackage.ToString() == "/Script/CoreUObject") {
                            if (e.Value.Length != 4) {
                                throw new Exception("Struct LatentActionInfo should have 4 members");
                            }
                            if (e.Value[0] is EX_SkipOffsetConst skip) {
                                if (e.Value[2] is EX_NameConst name) {
                                    if (e.Value[3] is EX_Self self) {
                                        referencedAddresses.Add(new Reference(skip.Value, ReferenceType.Skip, name.Value.ToString()));
                                    } else {
                                        Console.Error.WriteLine("WARN: Unimplemented LatentActionInfo to other than EX_Self");
                                    }
                                } else {
                                    Console.Error.WriteLine("WARN: Expected EX_NameConst but found " + e.Value[2]);
                                }
                            } else {
                                Console.Error.WriteLine("WARN: Expected EX_SkipOffsetConst but found " + e.Value[0]);
                            }
                        }
                    }

                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_SetArray e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.AssigningProperty, ref index, referencedAddresses));
                    foreach (var arg in e.Elements) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_SetSet e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.SetProperty, ref index, referencedAddresses));
                    index += 4;
                    foreach (var arg in e.Elements) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;
                    break;
                }
            case EX_SetMap e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.MapProperty, ref index, referencedAddresses));
                    index += 4;
                    var ei = -1;
                    foreach (var pair in Pairs(e.Elements)) {
                        ei++;
                        var entry = new Lines($"entry {ei}:");
                        lines.Add(Stringify(pair.Item1, ref index, referencedAddresses));
                        lines.Add(Stringify(pair.Item2, ref index, referencedAddresses));
                    }
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_MapConst e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    index += 8;
                    lines.Add("KeyProperty: " + ToString(e.KeyProperty));
                    lines.Add("ValueProperty: " + ToString(e.ValueProperty));
                    index += 4;
                    var ei = -1;
                    foreach (var pair in Pairs(e.Elements)) {
                        ei++;
                        var entry = new Lines($"entry {ei}:");
                        lines.Add(Stringify(pair.Item1, ref index, referencedAddresses));
                        lines.Add(Stringify(pair.Item2, ref index, referencedAddresses));
                    }
                    index++;
                    break;
                }
            case EX_SoftObjectConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    lines.Add(Stringify(e.Value, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_ByteConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_IntConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 4;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_Int64Const e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 8;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_UInt64Const e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 8;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_FloatConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 4;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_DoubleConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 8;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_NameConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 12;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_StringConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 1 + (uint) e.Value.Length;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_UnicodeStringConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 1 + (uint) (e.Value.Length + 1) * 2;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_ArrayConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    index += 8;
                    lines.Add(ToString(e.InnerProperty));
                    index += 4;
                    foreach (var arg in e.Elements) {
                        lines.Add(Stringify(arg, ref index, referencedAddresses));
                    }
                    index++;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_SkipOffsetConst e:
                {
                    lines = new Lines("EX_" + e.Inst + " " + e.Value);
                    // handled in LatentActionInfo struct instead
                    // referencedAddresses.Add(new Reference(e.Value, ReferenceType.Skip));
                    index += 4;
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_Return e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.ReturnExpression, ref index, referencedAddresses));
                    break;
                }
            case EX_InterfaceContext e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.InterfaceValue, ref index, referencedAddresses));
                    break;
                }
            case EX_ArrayGetByRef e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    lines.Add(Stringify(e.ArrayVariable, ref index, referencedAddresses));
                    lines.Add(Stringify(e.ArrayIndex, ref index, referencedAddresses));
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_Tracepoint e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_WireTracepoint e:
                {
                    lines = new Lines("EX_" + e.Inst);
                    if (top) referencedAddresses.Add(new Reference(index, ReferenceType.Normal));
                    break;
                }
            case EX_True:
            case EX_False:
            case EX_Nothing:
            case EX_NoObject:
            case EX_NoInterface:
            case EX_EndOfScript:
                {
                    lines = new Lines("EX_" + exp.Inst);
                    break;
                }
            default:
                {
                    throw new NotImplementedException($"DBG missing expression {exp}");
                    lines = new Lines("DBG missing expression " + exp); // TODO better error handling
                    break;
                }
        }
        return lines;
    }

    static IEnumerable<(KismetExpression, KismetExpression)> Pairs(IEnumerable<KismetExpression> input) {
        var e = input.GetEnumerator();
        try {
            while (e.MoveNext()) {
                var a = e.Current;
                if (e.MoveNext()) {
                    yield return (a, e.Current);
                }
            }
        } finally {
            (e as IDisposable)?.Dispose();
        }
    }

    static string ReadString(KismetExpression exp, ref uint index) {
        index++;
        switch (exp) {
            case EX_StringConst e:
                {
                    index += (uint) e.Value.Length + 1;
                    return e.Value;
                }
            case EX_UnicodeStringConst e:
                {
                    index += 2 * ((uint) e.Value.Length + 1);
                    return e.Value;
                }
            default:
                Console.Error.WriteLine("WARN: ReadString called on non-string");
                return "ERR";
        }
    }

    string ToString(KismetPropertyPointer pointer) {
        if (pointer.Old != null) {
            return ToString(pointer.Old);
        }
        if (pointer.New != null) {
            return ToString(pointer.New.Path);
        }
        throw new Exception("Unreachable");
    }

    string ToString(FPackageIndex index) {
        string getChain(FPackageIndex child) {
            if (child.IsNull()) {
                return "";
            }
            if (child.IsImport()) {
                var a = child.ToImport(Asset);
                return $"{getChain(a.OuterIndex)}{a.ObjectName}->";
            }
            if (child.IsExport()) {
                var a = child.ToExport(Asset);
                return $"{getChain(a.OuterIndex)}{a.ObjectName}->";
            }
            throw new NotImplementedException("Unreachable");

        }
        if (index.IsNull()) {
            return "null";
        } else if (index.IsExport()) {
            return $"export {getChain(index.ToExport(Asset).OuterIndex)}{index.ToExport(Asset).ObjectName}";
        } else if (index.IsImport()) {
            return $"import {getChain(index.ToImport(Asset).OuterIndex)}{index.ToImport(Asset).ObjectName}";
        }
        throw new Exception("Unreachable");
    }

    static void PrintIndent(TextWriter Output, int indent, string str, string prefix = "") {
        Output.WriteLine((indent == 0 ? prefix : "").PadRight((indent + 2) * 4) + str);
    }

    static string ToString(FName[] arr) {
        return "[" + String.Join(",", (object[]?)arr) + "]";
    }
}
