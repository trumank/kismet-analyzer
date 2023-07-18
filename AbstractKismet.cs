namespace KismetAnalyzer.AbstractKismet;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

public class AbstractKismetBuilder {
    private UAsset Asset;
    private Dictionary<(string, uint), string> Labels = new Dictionary<(string, uint), string>();
    private HashSet<string> LabelSet = new HashSet<string>();
    private Dictionary<KismetExpression, AbstractKismetExpression> JumpOffsetInstructions = new Dictionary<KismetExpression, AbstractKismetExpression>();

    private static Random random = new Random();

    private static string RandomLabel() {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return new string(Enumerable.Repeat(chars, 5)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public AbstractKismetBuilder(UAsset asset) {
        Asset = asset;
    }

    public string? GetLabel((string, uint) location) {
        return Labels.GetValueOrDefault(location);
    }

    public string GetLabelOrInvalid((string, uint) location) {
        string? label = GetLabel(location);
        if (label != null) {
            return label;
        }
        return $"{location.Item1} {location.Item2} !INVALID!";
    }
    public void CreateLabel((string, uint) location, KismetExpression? ex = null) {
        string label;
        if (Labels.ContainsKey(location)) {
            label = Labels[location];
        } else {
            do {
                label = RandomLabel();
            } while (LabelSet.Contains(label));
            Labels.Add(location, label);
            LabelSet.Add(label);
        }
        switch (ex) {
            case EX_IntConst e:
                JumpOffsetInstructions.Add(ex, new AEX_Jump_IntConst(label));
                break;
            case EX_SkipOffsetConst e:
                JumpOffsetInstructions.Add(ex, new AEX_Jump_SkipOffsetConst(label));
                break;
        }
    }

    public string FromKismetPropertyPointer(KismetPropertyPointer pointer) {
        if (Asset.ObjectVersion >= KismetPropertyPointer.XFER_PROP_POINTER_SWITCH_TO_SERIALIZING_AS_FIELD_PATH_VERSION) {
            return $"{FromPackageIndex(pointer.New.ResolvedOwner)}[{String.Join(",", pointer.New.Path.Select(p => p.ToString().Replace(",", "\\,")))}]";
        } else {
            return FromPackageIndex(pointer.Old);
        }
    }
    public string FromPackageIndex(FPackageIndex package) {
        return $"PackageIndex({(package == null ? "null" : package.Index)})";
    }

    public static AbstractKismet Build(UAsset asset) {
        var builder = new AbstractKismetBuilder(asset);
        return builder.Build();
    }

    private AbstractKismet Build() {
        // TODO: more robust ubergraph detection
        var ubergraph = (FunctionExport?) Asset.Exports.FirstOrDefault(e => e is FunctionExport && e.ObjectName.ToString().StartsWith("ExecuteUbergraph_"));

        foreach (var export in Asset.Exports) {
            if (export is FunctionExport fn) {
                foreach (var inst in fn.ScriptBytecode) {
                    KismetAnalyzer.Kismet.Walk(Asset, inst, ex => {
                        switch (ex) {
                            case EX_LocalFinalFunction e:
                                if (ubergraph != null && e.StackNode.ToExport(Asset) == ubergraph && e.Parameters.Length == 1 && e.Parameters[0] is EX_IntConst offset)
                                    this.CreateLabel((ubergraph.ObjectName.ToString(), (uint) offset.Value), offset);
                                break;
                            case EX_Jump e:
                                this.CreateLabel((fn.ObjectName.ToString(), e.CodeOffset));
                                break;
                            case EX_JumpIfNot e:
                                this.CreateLabel((fn.ObjectName.ToString(), e.CodeOffset));
                                break;
                            case EX_PushExecutionFlow e:
                                this.CreateLabel((fn.ObjectName.ToString(), e.PushingAddress));
                                break;
                            case EX_StructConst e:
                                if (e.Struct.IsImport() && ubergraph != null) {
                                    var str = e.Struct.ToImport(Asset);
                                    if (str.ClassPackage.ToString() == "/Script/CoreUObject" && str.ClassName.ToString() == "ScriptStruct" && str.ObjectName.ToString() == "LatentActionInfo") {
                                        // TODO: confirm struct points to ubergraph
                                        switch (e.Value[0]) {
                                            case EX_SkipOffsetConst latentOffset:
                                                this.CreateLabel((fn.ObjectName.ToString(), latentOffset.Value), latentOffset);
                                                break;
                                            case EX_IntConst latentOffset:
                                                if (latentOffset.Value != -1)
                                                    this.CreateLabel((fn.ObjectName.ToString(), (uint) latentOffset.Value), latentOffset);
                                                break;
                                        }
                                    }
                                }
                                break;
                        }
                    });
                }
            }
        }
        var functions = new Dictionary<string, IEnumerable<AbstractKismetExpression>>();
        foreach (var export in Asset.Exports) {
            if (export is FunctionExport fn) {
                uint offset = 0;
                var inst = fn.ScriptBytecode.Select(e => {
                    var aex = this.FromKismetExpression(fn, e);
                    aex.Label = this.GetLabel((fn.ObjectName.ToString(), offset));
                    offset += Kismet.GetSize(Asset, e);
                    return aex;
                });
                functions.Add(fn.ObjectName.ToString(), inst);
            }
        }

        return new AbstractKismet() {
            Functions = functions,
        };
    }

    public AbstractKismetExpression FromKismetExpression(FunctionExport fn, KismetExpression ex) {
        if (JumpOffsetInstructions.TryGetValue(ex, out var aex)) {
            return aex;
        }
        switch (ex) {
            case EX_AddMulticastDelegate e:
                return new AEX_AddMulticastDelegate(this, fn, e);
            case EX_ArrayConst e:
                return new AEX_ArrayConst(this, fn, e);
            case EX_ArrayGetByRef e:
                return new AEX_ArrayGetByRef(this, fn, e);
            case EX_Assert e:
                return new AEX_Assert(this, fn, e);
            case EX_BindDelegate e:
                return new AEX_BindDelegate(this, fn, e);
            case EX_Breakpoint e:
                return new AEX_Breakpoint(this, fn, e);
            case EX_ByteConst e:
                return new AEX_ByteConst(this, fn, e);
            case EX_CallMath e:
                return new AEX_CallMath(this, fn, e);
            case EX_CallMulticastDelegate e:
                return new AEX_CallMulticastDelegate(this, fn, e);
            case EX_ClassContext e:
                return new AEX_ClassContext(this, fn, e);
            case EX_ClassSparseDataVariable e:
                return new AEX_ClassSparseDataVariable(this, fn, e);
            case EX_ClearMulticastDelegate e:
                return new AEX_ClearMulticastDelegate(this, fn, e);
            case EX_ComputedJump e:
                return new AEX_ComputedJump(this, fn, e);
            case EX_Context_FailSilent e:
                return new AEX_Context_FailSilent(this, fn, e);
            case EX_Context e:
                return new AEX_Context(this, fn, e);
            case EX_CrossInterfaceCast e:
                return new AEX_CrossInterfaceCast(this, fn, e);
            case EX_DefaultVariable e:
                return new AEX_DefaultVariable(this, fn, e);
            case EX_DeprecatedOp4A e:
                return new AEX_DeprecatedOp4A(this, fn, e);
            case EX_DynamicCast e:
                return new AEX_DynamicCast(this, fn, e);
            case EX_EndArray e:
                return new AEX_EndArray(this, fn, e);
            case EX_EndArrayConst e:
                return new AEX_EndArrayConst(this, fn, e);
            case EX_EndFunctionParms e:
                return new AEX_EndFunctionParms(this, fn, e);
            case EX_EndMap e:
                return new AEX_EndMap(this, fn, e);
            case EX_EndMapConst e:
                return new AEX_EndMapConst(this, fn, e);
            case EX_EndOfScript e:
                return new AEX_EndOfScript(this, fn, e);
            case EX_EndParmValue e:
                return new AEX_EndParmValue(this, fn, e);
            case EX_EndSet e:
                return new AEX_EndSet(this, fn, e);
            case EX_EndSetConst e:
                return new AEX_EndSetConst(this, fn, e);
            case EX_EndStructConst e:
                return new AEX_EndStructConst(this, fn, e);
            case EX_False e:
                return new AEX_False(this, fn, e);
            case EX_FieldPathConst e:
                return new AEX_FieldPathConst(this, fn, e);
            case EX_LocalFinalFunction e:
                return new AEX_LocalFinalFunction(this, fn, e);
            case EX_FinalFunction e:
                return new AEX_FinalFunction(this, fn, e);
            case EX_FloatConst e:
                return new AEX_FloatConst(this, fn, e);
            case EX_DoubleConst e:
                return new AEX_DoubleConst(this, fn, e);
            case EX_InstanceDelegate e:
                return new AEX_InstanceDelegate(this, fn, e);
            case EX_InstanceVariable e:
                return new AEX_InstanceVariable(this, fn, e);
            case EX_InstrumentationEvent e:
                return new AEX_InstrumentationEvent(this, fn, e);
            case EX_Int64Const e:
                return new AEX_Int64Const(this, fn, e);
            case EX_IntConst e:
                return new AEX_IntConst(this, fn, e);
            case EX_IntConstByte e:
                return new AEX_IntConstByte(this, fn, e);
            case EX_IntOne e:
                return new AEX_IntOne(this, fn, e);
            case EX_IntZero e:
                return new AEX_IntZero(this, fn, e);
            case EX_InterfaceContext e:
                return new AEX_InterfaceContext(this, fn, e);
            case EX_InterfaceToObjCast e:
                return new AEX_InterfaceToObjCast(this, fn, e);
            case EX_Jump e:
                return new AEX_Jump(this, fn, e);
            case EX_JumpIfNot e:
                return new AEX_JumpIfNot(this, fn, e);
            case EX_Let e:
                return new AEX_Let(this, fn, e);
            case EX_LetBool e:
                return new AEX_LetBool(this, fn, e);
            case EX_LetDelegate e:
                return new AEX_LetDelegate(this, fn, e);
            case EX_LetMulticastDelegate e:
                return new AEX_LetMulticastDelegate(this, fn, e);
            case EX_LetObj e:
                return new AEX_LetObj(this, fn, e);
            case EX_LetValueOnPersistentFrame e:
                return new AEX_LetValueOnPersistentFrame(this, fn, e);
            case EX_LetWeakObjPtr e:
                return new AEX_LetWeakObjPtr(this, fn, e);
            case EX_LocalOutVariable e:
                return new AEX_LocalOutVariable(this, fn, e);
            case EX_LocalVariable e:
                return new AEX_LocalVariable(this, fn, e);
            case EX_LocalVirtualFunction e:
                return new AEX_LocalVirtualFunction(this, fn, e);
            case EX_MapConst e:
                return new AEX_MapConst(this, fn, e);
            case EX_MetaCast e:
                return new AEX_MetaCast(this, fn, e);
            case EX_NameConst e:
                return new AEX_NameConst(this, fn, e);
            case EX_NoInterface e:
                return new AEX_NoInterface(this, fn, e);
            case EX_NoObject e:
                return new AEX_NoObject(this, fn, e);
            case EX_Nothing e:
                return new AEX_Nothing(this, fn, e);
            case EX_ObjToInterfaceCast e:
                return new AEX_ObjToInterfaceCast(this, fn, e);
            case EX_ObjectConst e:
                return new AEX_ObjectConst(this, fn, e);
            case EX_PopExecutionFlow e:
                return new AEX_PopExecutionFlow(this, fn, e);
            case EX_PopExecutionFlowIfNot e:
                return new AEX_PopExecutionFlowIfNot(this, fn, e);
            case EX_PrimitiveCast e:
                return new AEX_PrimitiveCast(this, fn, e);
            case EX_PropertyConst e:
                return new AEX_PropertyConst(this, fn, e);
            case EX_PushExecutionFlow e:
                return new AEX_PushExecutionFlow(this, fn, e);
            case EX_RemoveMulticastDelegate e:
                return new AEX_RemoveMulticastDelegate(this, fn, e);
            case EX_Return e:
                return new AEX_Return(this, fn, e);
            case EX_RotationConst e:
                return new AEX_RotationConst(this, fn, e);
            case EX_Self e:
                return new AEX_Self(this, fn, e);
            case EX_SetArray e:
                return new AEX_SetArray(this, fn, e);
            case EX_SetConst e:
                return new AEX_SetConst(this, fn, e);
            case EX_SetMap e:
                return new AEX_SetMap(this, fn, e);
            case EX_SetSet e:
                return new AEX_SetSet(this, fn, e);
            case EX_Skip e:
                return new AEX_Skip(this, fn, e);
            case EX_SkipOffsetConst e:
                return new AEX_SkipOffsetConst(this, fn, e);
            case EX_SoftObjectConst e:
                return new AEX_SoftObjectConst(this, fn, e);
            case EX_StringConst e:
                return new AEX_StringConst(this, fn, e);
            case EX_StructConst e:
                return new AEX_StructConst(this, fn, e);
            case EX_StructMemberContext e:
                return new AEX_StructMemberContext(this, fn, e);
            case EX_SwitchValue e:
                return new AEX_SwitchValue(this, fn, e);
            case EX_TextConst e:
                return new AEX_TextConst(this, fn, e);
            case EX_Tracepoint e:
                return new AEX_Tracepoint(this, fn, e);
            case EX_TransformConst e:
                return new AEX_TransformConst(this, fn, e);
            case EX_True e:
                return new AEX_True(this, fn, e);
            case EX_UInt64Const e:
                return new AEX_UInt64Const(this, fn, e);
            case EX_UnicodeStringConst e:
                return new AEX_UnicodeStringConst(this, fn, e);
            case EX_VectorConst e:
                return new AEX_VectorConst(this, fn, e);
            case EX_VirtualFunction e:
                return new AEX_VirtualFunction(this, fn, e);
            case EX_WireTracepoint e:
                return new AEX_WireTracepoint(this, fn, e);
            default:
                throw new NotImplementedException(ex.ToString());
        }
    }
}

public class KismetBuilder {
    private UAsset Asset;

    private List<(string, Action<uint>)> LabelsToResolve = new List<(string, Action<uint>)>();

    public KismetBuilder(UAsset asset) {
        Asset = asset;
    }

    public FName ToFName(String name) {
        return FName.FromString(Asset, name);
    }

    static Regex RxPackageIndex = new Regex(@"^PackageIndex\(([^)]+)\)$", RegexOptions.Compiled);
    public FPackageIndex? ToPackageIndex(String packageIndex) {
        var match = RxPackageIndex.Match(packageIndex).Groups[1].Value;
        if (match == "null") return null;
        return FPackageIndex.FromRawIndex(Int32.Parse(match));
    }

    static Regex RxKismetPropertyPointer = new Regex(@"^PackageIndex\((-?\d+)\)\[(.*)\]$", RegexOptions.Compiled);
    public KismetPropertyPointer ToKismetPropertyPointer(String pointer) {
        if (Asset.ObjectVersion >= KismetPropertyPointer.XFER_PROP_POINTER_SWITCH_TO_SERIALIZING_AS_FIELD_PATH_VERSION) {
            var match = RxKismetPropertyPointer.Match(pointer);

            var owner = FPackageIndex.FromRawIndex(Int32.Parse(match.Groups[1].Value));
            // TODO handle backslash escaped ,
            var path = match.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => ToFName(n)).ToArray();

            return new KismetPropertyPointer(new FFieldPath(path, owner));
        } else {
            return new KismetPropertyPointer(ToPackageIndex(pointer));
        }
    }

    public void ResolveLabel(string label, Action<uint> cb) {
        LabelsToResolve.Add((label, cb));
    }

    public void Build(AbstractKismet ak) {
        var functionExports = new Dictionary<String, FunctionExport>();
        var ubergraph = (FunctionExport?) Asset.Exports.FirstOrDefault(e => e is FunctionExport && e.ObjectName.ToString().StartsWith("ExecuteUbergraph_"));

        foreach (var export in Asset.Exports) {
            if (export is FunctionExport fn) {
                functionExports.Add(fn.ObjectName.ToString(), fn);
            }
        }

        var labels = new Dictionary<string, uint>();
        foreach (var fnName in functionExports.Keys.Union(ak.Functions.Keys)) {
            var fnExport = functionExports[fnName];
            var fn = ak.Functions[fnName];

            uint offset = 0;
            uint offsetWalk = 0;
            fnExport.ScriptBytecode = fn.Select(ae => {
                if (ae.Label != null) {
                    labels.Add(ae.Label, offset);
                }
                var ex = ae.ToKismetExpression(this);
                offset += Kismet.GetSize(Asset, ex);
                Kismet.Walk(Asset, ref offsetWalk, ex, (ex, offset) => {
                    // fix absolute offsets in EX_SwitchValue
                    if (ex is EX_SwitchValue e) {
                        offset += 7 + Kismet.GetSize(Asset, e.IndexTerm);
                        e.Cases = e.Cases.Select(c => {
                            offset += Kismet.GetSize(Asset, c.CaseIndexValueTerm);
                            offset += 4;
                            offset += Kismet.GetSize(Asset, c.CaseTerm);
                            return new FKismetSwitchCase(c.CaseIndexValueTerm, offset, c.CaseTerm);
                        }).ToArray();
                        offset += Kismet.GetSize(Asset, e.DefaultTerm);
                        e.EndGotoOffset = offset;
                    }
                });
                if (offset != offsetWalk) throw new Exception("offset mismatch");
                return ex;
            }).ToArray();
        }

        foreach (var cb in LabelsToResolve) {
            cb.Item2(labels[cb.Item1]);
        }
    }
}

public class AbstractKismet {
    public required Dictionary<string, IEnumerable<AbstractKismetExpression>> Functions { get; set; }

    public static void Export(EngineVersion ueVersion, string assetInput, string jsonOutput) {
        var asset = new UAsset(assetInput, ueVersion);
        var abstractKismet = AbstractKismetBuilder.Build(asset);
        using var stream = File.Create(jsonOutput);
        JsonSerializer.Serialize(stream, abstractKismet, new JsonSerializerOptions { WriteIndented = true, });
    }

    public static void Import(EngineVersion ueVersion, string assetInput, string assetOutput, string jsonInput) {
        var asset = new UAsset(assetInput, ueVersion);
        using FileStream stream = File.OpenRead(jsonInput);
        var restore = JsonSerializer.Deserialize<AbstractKismet>(stream)!;
        var kb = new KismetBuilder(asset);
        kb.Build(restore);
        asset.Write(assetOutput);
    }
}

public struct AbstractKismetPropertyPointer {
    public string? Label { get; set; }
}

[JsonDerivedType(typeof(AEX_Jump_IntConst), typeDiscriminator: "Jump_IntConst")]
[JsonDerivedType(typeof(AEX_Jump_SkipOffsetConst), typeDiscriminator: "Jump_SkipOffsetConst")]

[JsonDerivedType(typeof(AEX_AddMulticastDelegate), typeDiscriminator: "EX_AddMulticastDelegate")]
[JsonDerivedType(typeof(AEX_ArrayConst), typeDiscriminator: "EX_ArrayConst")]
[JsonDerivedType(typeof(AEX_ArrayGetByRef), typeDiscriminator: "EX_ArrayGetByRef")]
[JsonDerivedType(typeof(AEX_Assert), typeDiscriminator: "EX_Assert")]
[JsonDerivedType(typeof(AEX_BindDelegate), typeDiscriminator: "EX_BindDelegate")]
[JsonDerivedType(typeof(AEX_Breakpoint), typeDiscriminator: "EX_Breakpoint")]
[JsonDerivedType(typeof(AEX_ByteConst), typeDiscriminator: "EX_ByteConst")]
[JsonDerivedType(typeof(AEX_CallMath), typeDiscriminator: "EX_CallMath")]
[JsonDerivedType(typeof(AEX_CallMulticastDelegate), typeDiscriminator: "EX_CallMulticastDelegate")]
[JsonDerivedType(typeof(AEX_ClassContext), typeDiscriminator: "EX_ClassContext")]
[JsonDerivedType(typeof(AEX_ClassSparseDataVariable), typeDiscriminator: "EX_ClassSparseDataVariable")]
[JsonDerivedType(typeof(AEX_ClearMulticastDelegate), typeDiscriminator: "EX_ClearMulticastDelegate")]
[JsonDerivedType(typeof(AEX_ComputedJump), typeDiscriminator: "EX_ComputedJump")]
[JsonDerivedType(typeof(AEX_Context), typeDiscriminator: "EX_Context")]
[JsonDerivedType(typeof(AEX_Context_FailSilent), typeDiscriminator: "EX_Context_FailSilent")]
[JsonDerivedType(typeof(AEX_CrossInterfaceCast), typeDiscriminator: "EX_CrossInterfaceCast")]
[JsonDerivedType(typeof(AEX_DefaultVariable), typeDiscriminator: "EX_DefaultVariable")]
[JsonDerivedType(typeof(AEX_DeprecatedOp4A), typeDiscriminator: "EX_DeprecatedOp4A")]
[JsonDerivedType(typeof(AEX_DynamicCast), typeDiscriminator: "EX_DynamicCast")]
[JsonDerivedType(typeof(AEX_EndArray), typeDiscriminator: "EX_EndArray")]
[JsonDerivedType(typeof(AEX_EndArrayConst), typeDiscriminator: "EX_EndArrayConst")]
[JsonDerivedType(typeof(AEX_EndFunctionParms), typeDiscriminator: "EX_EndFunctionParms")]
[JsonDerivedType(typeof(AEX_EndMap), typeDiscriminator: "EX_EndMap")]
[JsonDerivedType(typeof(AEX_EndMapConst), typeDiscriminator: "EX_EndMapConst")]
[JsonDerivedType(typeof(AEX_EndOfScript), typeDiscriminator: "EX_EndOfScript")]
[JsonDerivedType(typeof(AEX_EndParmValue), typeDiscriminator: "EX_EndParmValue")]
[JsonDerivedType(typeof(AEX_EndSet), typeDiscriminator: "EX_EndSet")]
[JsonDerivedType(typeof(AEX_EndSetConst), typeDiscriminator: "EX_EndSetConst")]
[JsonDerivedType(typeof(AEX_EndStructConst), typeDiscriminator: "EX_EndStructConst")]
[JsonDerivedType(typeof(AEX_False), typeDiscriminator: "EX_False")]
[JsonDerivedType(typeof(AEX_FieldPathConst), typeDiscriminator: "EX_FieldPathConst")]
[JsonDerivedType(typeof(AEX_FinalFunction), typeDiscriminator: "EX_FinalFunction")]
[JsonDerivedType(typeof(AEX_FloatConst), typeDiscriminator: "EX_FloatConst")]
[JsonDerivedType(typeof(AEX_DoubleConst), typeDiscriminator: "EX_DoubleConst")]
[JsonDerivedType(typeof(AEX_InstanceDelegate), typeDiscriminator: "EX_InstanceDelegate")]
[JsonDerivedType(typeof(AEX_InstanceVariable), typeDiscriminator: "EX_InstanceVariable")]
[JsonDerivedType(typeof(AEX_InstrumentationEvent), typeDiscriminator: "EX_InstrumentationEvent")]
[JsonDerivedType(typeof(AEX_Int64Const), typeDiscriminator: "EX_Int64Const")]
[JsonDerivedType(typeof(AEX_IntConst), typeDiscriminator: "EX_IntConst")]
[JsonDerivedType(typeof(AEX_IntConstByte), typeDiscriminator: "EX_IntConstByte")]
[JsonDerivedType(typeof(AEX_IntOne), typeDiscriminator: "EX_IntOne")]
[JsonDerivedType(typeof(AEX_IntZero), typeDiscriminator: "EX_IntZero")]
[JsonDerivedType(typeof(AEX_InterfaceContext), typeDiscriminator: "EX_InterfaceContext")]
[JsonDerivedType(typeof(AEX_InterfaceToObjCast), typeDiscriminator: "EX_InterfaceToObjCast")]
[JsonDerivedType(typeof(AEX_Jump), typeDiscriminator: "EX_Jump")]
[JsonDerivedType(typeof(AEX_JumpIfNot), typeDiscriminator: "EX_JumpIfNot")]
[JsonDerivedType(typeof(AEX_Let), typeDiscriminator: "EX_Let")]
[JsonDerivedType(typeof(AEX_LetBool), typeDiscriminator: "EX_LetBool")]
[JsonDerivedType(typeof(AEX_LetDelegate), typeDiscriminator: "EX_LetDelegate")]
[JsonDerivedType(typeof(AEX_LetMulticastDelegate), typeDiscriminator: "EX_LetMulticastDelegate")]
[JsonDerivedType(typeof(AEX_LetObj), typeDiscriminator: "EX_LetObj")]
[JsonDerivedType(typeof(AEX_LetValueOnPersistentFrame), typeDiscriminator: "EX_LetValueOnPersistentFrame")]
[JsonDerivedType(typeof(AEX_LetWeakObjPtr), typeDiscriminator: "EX_LetWeakObjPtr")]
[JsonDerivedType(typeof(AEX_LocalFinalFunction), typeDiscriminator: "EX_LocalFinalFunction")]
[JsonDerivedType(typeof(AEX_LocalOutVariable), typeDiscriminator: "EX_LocalOutVariable")]
[JsonDerivedType(typeof(AEX_LocalVariable), typeDiscriminator: "EX_LocalVariable")]
[JsonDerivedType(typeof(AEX_LocalVirtualFunction), typeDiscriminator: "EX_LocalVirtualFunction")]
[JsonDerivedType(typeof(AEX_MapConst), typeDiscriminator: "EX_MapConst")]
[JsonDerivedType(typeof(AEX_MetaCast), typeDiscriminator: "EX_MetaCast")]
[JsonDerivedType(typeof(AEX_NameConst), typeDiscriminator: "EX_NameConst")]
[JsonDerivedType(typeof(AEX_NoInterface), typeDiscriminator: "EX_NoInterface")]
[JsonDerivedType(typeof(AEX_NoObject), typeDiscriminator: "EX_NoObject")]
[JsonDerivedType(typeof(AEX_Nothing), typeDiscriminator: "EX_Nothing")]
[JsonDerivedType(typeof(AEX_ObjToInterfaceCast), typeDiscriminator: "EX_ObjToInterfaceCast")]
[JsonDerivedType(typeof(AEX_ObjectConst), typeDiscriminator: "EX_ObjectConst")]
[JsonDerivedType(typeof(AEX_PopExecutionFlow), typeDiscriminator: "EX_PopExecutionFlow")]
[JsonDerivedType(typeof(AEX_PopExecutionFlowIfNot), typeDiscriminator: "EX_PopExecutionFlowIfNot")]
[JsonDerivedType(typeof(AEX_PrimitiveCast), typeDiscriminator: "EX_PrimitiveCast")]
[JsonDerivedType(typeof(AEX_PropertyConst), typeDiscriminator: "EX_PropertyConst")]
[JsonDerivedType(typeof(AEX_PushExecutionFlow), typeDiscriminator: "EX_PushExecutionFlow")]
[JsonDerivedType(typeof(AEX_RemoveMulticastDelegate), typeDiscriminator: "EX_RemoveMulticastDelegate")]
[JsonDerivedType(typeof(AEX_Return), typeDiscriminator: "EX_Return")]
[JsonDerivedType(typeof(AEX_RotationConst), typeDiscriminator: "EX_RotationConst")]
[JsonDerivedType(typeof(AEX_Self), typeDiscriminator: "EX_Self")]
[JsonDerivedType(typeof(AEX_SetArray), typeDiscriminator: "EX_SetArray")]
[JsonDerivedType(typeof(AEX_SetConst), typeDiscriminator: "EX_SetConst")]
[JsonDerivedType(typeof(AEX_SetMap), typeDiscriminator: "EX_SetMap")]
[JsonDerivedType(typeof(AEX_SetSet), typeDiscriminator: "EX_SetSet")]
[JsonDerivedType(typeof(AEX_Skip), typeDiscriminator: "EX_Skip")]
[JsonDerivedType(typeof(AEX_SkipOffsetConst), typeDiscriminator: "EX_SkipOffsetConst")]
[JsonDerivedType(typeof(AEX_SoftObjectConst), typeDiscriminator: "EX_SoftObjectConst")]
[JsonDerivedType(typeof(AEX_StringConst), typeDiscriminator: "EX_StringConst")]
[JsonDerivedType(typeof(AEX_StructConst), typeDiscriminator: "EX_StructConst")]
[JsonDerivedType(typeof(AEX_StructMemberContext), typeDiscriminator: "EX_StructMemberContext")]
[JsonDerivedType(typeof(AEX_SwitchValue), typeDiscriminator: "EX_SwitchValue")]
[JsonDerivedType(typeof(AEX_TextConst), typeDiscriminator: "EX_TextConst")]
[JsonDerivedType(typeof(AEX_Tracepoint), typeDiscriminator: "EX_Tracepoint")]
[JsonDerivedType(typeof(AEX_TransformConst), typeDiscriminator: "EX_TransformConst")]
[JsonDerivedType(typeof(AEX_True), typeDiscriminator: "EX_True")]
[JsonDerivedType(typeof(AEX_UInt64Const), typeDiscriminator: "EX_UInt64Const")]
[JsonDerivedType(typeof(AEX_UnicodeStringConst), typeDiscriminator: "EX_UnicodeStringConst")]
[JsonDerivedType(typeof(AEX_VectorConst), typeDiscriminator: "EX_VectorConst")]
[JsonDerivedType(typeof(AEX_VirtualFunction), typeDiscriminator: "EX_VirtualFunction")]
[JsonDerivedType(typeof(AEX_WireTracepoint), typeDiscriminator: "EX_WireTracepoint")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Ex")]
public abstract class AbstractKismetExpression {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrderAttribute(-1)]
    public string? Label { get; set; }

    public abstract KismetExpression ToKismetExpression(KismetBuilder b);
}

public class AEX_Jump_IntConst : AbstractKismetExpression {
    public string Jump { get; set; } = null!;

    public AEX_Jump_IntConst() {}
    public AEX_Jump_IntConst(string jump) {
        Jump = jump;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        var ex = new EX_IntConst();
        b.ResolveLabel(Jump, offset => ex.Value = (int) offset);
        return ex;
    }
}
public class AEX_Jump_SkipOffsetConst : AbstractKismetExpression {
    public string Jump { get; set; } = null!;

    public AEX_Jump_SkipOffsetConst() {}
    public AEX_Jump_SkipOffsetConst(string jump) {
        Jump = jump;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        var ex = new EX_SkipOffsetConst();
        b.ResolveLabel(Jump, offset => ex.Value = offset);
        return ex;
    }
}

public class AEX_AddMulticastDelegate : AbstractKismetExpression {
    public AbstractKismetExpression Delegate { get; set; } = null!;
    public AbstractKismetExpression DelegateToAdd { get; set; } = null!;

    public AEX_AddMulticastDelegate() {}
    public AEX_AddMulticastDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_AddMulticastDelegate e) {
        Delegate = ab.FromKismetExpression(fn, e.Delegate);
        DelegateToAdd = ab.FromKismetExpression(fn, e.DelegateToAdd);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_AddMulticastDelegate() {
            Delegate = Delegate.ToKismetExpression(b),
            DelegateToAdd = DelegateToAdd.ToKismetExpression(b),
        };
    }
}
public class AEX_ArrayConst : AbstractKismetExpression {
    public string InnerProperty { get; set; } = null!;
    public IEnumerable<AbstractKismetExpression> Elements { get; set; } = null!;

    public AEX_ArrayConst() {}
    public AEX_ArrayConst(AbstractKismetBuilder ab, FunctionExport fn, EX_ArrayConst e) {
        InnerProperty = ab.FromKismetPropertyPointer(e.InnerProperty);
        Elements = e.Elements.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_ArrayConst() {
            InnerProperty = b.ToKismetPropertyPointer(InnerProperty),
            Elements = Elements.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_ArrayGetByRef : AbstractKismetExpression {
    public AEX_ArrayGetByRef() {}
    public AEX_ArrayGetByRef(AbstractKismetBuilder ab, FunctionExport fn, EX_ArrayGetByRef e) {
        throw new NotImplementedException("EX_ArrayGetByRef");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_ArrayGetByRef");
        return new EX_ArrayGetByRef() {
        };
    }
}
public class AEX_Assert : AbstractKismetExpression {
    public AEX_Assert() {}
    public AEX_Assert(AbstractKismetBuilder ab, FunctionExport fn, EX_Assert e) {
        throw new NotImplementedException("EX_Assert");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_Assert");
        return new EX_Assert() {
        };
    }
}
public class AEX_BindDelegate : AbstractKismetExpression {
    public AEX_BindDelegate() {}
    public string FunctionName { get; set; } = null!;
    public AbstractKismetExpression Delegate { get; set; } = null!;
    public AbstractKismetExpression ObjectTerm { get; set; } = null!;

    public AEX_BindDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_BindDelegate e) {
        FunctionName = e.FunctionName.ToString();
        Delegate = ab.FromKismetExpression(fn, e.Delegate);
        ObjectTerm = ab.FromKismetExpression(fn, e.ObjectTerm);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_BindDelegate() {
            FunctionName = b.ToFName(FunctionName),
            Delegate = Delegate.ToKismetExpression(b),
            ObjectTerm = ObjectTerm.ToKismetExpression(b),
        };
    }
}
public class AEX_Breakpoint : AbstractKismetExpression {
    public AEX_Breakpoint() {}
    public AEX_Breakpoint(AbstractKismetBuilder ab, FunctionExport fn, EX_Breakpoint e) {
        throw new NotImplementedException("EX_Breakpoint");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_Breakpoint");
        return new EX_Breakpoint() {
        };
    }
}
public class AEX_ByteConst : AbstractKismetExpression {
    public AEX_ByteConst() {}
    public byte Value { get; set; }

    public AEX_ByteConst(AbstractKismetBuilder ab, FunctionExport fn, EX_ByteConst e) {
        Value = e.Value;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_ByteConst() {
            Value = Value,
        };
    }
}
public class AEX_CallMath : AbstractKismetExpression {
    public AEX_CallMath() {}
    public string StackNode { get; set; } = null!;
    public IEnumerable<AbstractKismetExpression> Parameters { get; set; } = null!;

    public AEX_CallMath(AbstractKismetBuilder ab, FunctionExport fn, EX_CallMath e) {
        StackNode = ab.FromPackageIndex(e.StackNode);
        Parameters = e.Parameters.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_CallMath() {
            StackNode = b.ToPackageIndex(StackNode),
            Parameters = Parameters.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_CallMulticastDelegate : AbstractKismetExpression {
    public AEX_CallMulticastDelegate() {}
    public AEX_CallMulticastDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_CallMulticastDelegate e) {
        throw new NotImplementedException("EX_CallMulticastDelegate");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_CallMulticastDelegate");
        return new EX_CallMulticastDelegate() {
        };
    }
}
public class AEX_ClassContext : AbstractKismetExpression {
    public AEX_ClassContext() {}
    public AEX_ClassContext(AbstractKismetBuilder ab, FunctionExport fn, EX_ClassContext e) {
        throw new NotImplementedException("EX_ClassContext");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_ClassContext");
        return new EX_ClassContext() {
        };
    }
}
public class AEX_ClassSparseDataVariable : AbstractKismetExpression {
    public AEX_ClassSparseDataVariable() {}
    public AEX_ClassSparseDataVariable(AbstractKismetBuilder ab, FunctionExport fn, EX_ClassSparseDataVariable e) {
        throw new NotImplementedException("EX_ClassSparseDataVariable");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_ClassSparseDataVariable");
        return new EX_ClassSparseDataVariable() {
        };
    }
}
public class AEX_ClearMulticastDelegate : AbstractKismetExpression {
    public AEX_ClearMulticastDelegate() {}
    public AEX_ClearMulticastDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_ClearMulticastDelegate e) {
        throw new NotImplementedException("EX_ClearMulticastDelegate");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_ClearMulticastDelegate");
        return new EX_ClearMulticastDelegate() {
        };
    }
}
public class AEX_ComputedJump : AbstractKismetExpression {
    public AEX_ComputedJump() {}
    public AbstractKismetExpression CodeOffsetExpression { get; set; } = null!;

    public AEX_ComputedJump(AbstractKismetBuilder ab, FunctionExport fn, EX_ComputedJump e) {
        CodeOffsetExpression = ab.FromKismetExpression(fn, e.CodeOffsetExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_ComputedJump() {
            CodeOffsetExpression = CodeOffsetExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_Context : AbstractKismetExpression {
    public AEX_Context() {}
    public AbstractKismetExpression ObjectExpression { get; set; } = null!;
    public uint Offset { get; set; }
    public string RValuePointer { get; set; } = null!;
    public AbstractKismetExpression ContextExpression { get; set; } = null!;

    public AEX_Context(AbstractKismetBuilder ab, FunctionExport fn, EX_Context e) {
        ObjectExpression = ab.FromKismetExpression(fn, e.ObjectExpression);
        Offset = e.Offset;
        RValuePointer = ab.FromKismetPropertyPointer(e.RValuePointer);
        ContextExpression = ab.FromKismetExpression(fn, e.ContextExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_Context() {
            ObjectExpression = ObjectExpression.ToKismetExpression(b),
            Offset = Offset,
            RValuePointer = b.ToKismetPropertyPointer(RValuePointer),
            ContextExpression = ContextExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_Context_FailSilent : AbstractKismetExpression {
    public AEX_Context_FailSilent() {}
    public AEX_Context_FailSilent(AbstractKismetBuilder ab, FunctionExport fn, EX_Context_FailSilent e) {
        throw new NotImplementedException("EX_Context_FailSilent");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_Context_FailSilent");
        return new EX_Context_FailSilent() {
        };
    }
}
public class AEX_CrossInterfaceCast : AbstractKismetExpression {
    public AEX_CrossInterfaceCast() {}
    public AEX_CrossInterfaceCast(AbstractKismetBuilder ab, FunctionExport fn, EX_CrossInterfaceCast e) {
        throw new NotImplementedException("EX_CrossInterfaceCast");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_CrossInterfaceCast");
        return new EX_CrossInterfaceCast() {
        };
    }
}
public class AEX_DefaultVariable : AbstractKismetExpression {
    public AEX_DefaultVariable() {}
    public AEX_DefaultVariable(AbstractKismetBuilder ab, FunctionExport fn, EX_DefaultVariable e) {
        throw new NotImplementedException("EX_DefaultVariable");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_DefaultVariable");
        return new EX_DefaultVariable() {
        };
    }
}
public class AEX_DeprecatedOp4A : AbstractKismetExpression {
    public AEX_DeprecatedOp4A() {}
    public AEX_DeprecatedOp4A(AbstractKismetBuilder ab, FunctionExport fn, EX_DeprecatedOp4A e) {
        throw new NotImplementedException("EX_DeprecatedOp4A");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_DeprecatedOp4A");
        return new EX_DeprecatedOp4A() {
        };
    }
}
public class AEX_DynamicCast : AbstractKismetExpression {
    public AEX_DynamicCast() {}
    public string ClassPtr { get; set; } = null!;
    public AbstractKismetExpression TargetExpression { get; set; } = null!;

    public AEX_DynamicCast(AbstractKismetBuilder ab, FunctionExport fn, EX_DynamicCast e) {
        ClassPtr = ab.FromPackageIndex(e.ClassPtr);
        TargetExpression = ab.FromKismetExpression(fn, e.TargetExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_DynamicCast() {
            ClassPtr = b.ToPackageIndex(ClassPtr),
            TargetExpression = TargetExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_EndArray : AbstractKismetExpression {
    public AEX_EndArray() {}
    public AEX_EndArray(AbstractKismetBuilder ab, FunctionExport fn, EX_EndArray e) {
        throw new NotImplementedException("EX_EndArray");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndArray");
        return new EX_EndArray() {
        };
    }
}
public class AEX_EndArrayConst : AbstractKismetExpression {
    public AEX_EndArrayConst() {}
    public AEX_EndArrayConst(AbstractKismetBuilder ab, FunctionExport fn, EX_EndArrayConst e) {
        throw new NotImplementedException("EX_EndArrayConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndArrayConst");
        return new EX_EndArrayConst() {
        };
    }
}
public class AEX_EndFunctionParms : AbstractKismetExpression {
    public AEX_EndFunctionParms() {}
    public AEX_EndFunctionParms(AbstractKismetBuilder ab, FunctionExport fn, EX_EndFunctionParms e) {
        throw new NotImplementedException("EX_EndFunctionParms");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndFunctionParms");
        return new EX_EndFunctionParms() {
        };
    }
}
public class AEX_EndMap : AbstractKismetExpression {
    public AEX_EndMap() {}
    public AEX_EndMap(AbstractKismetBuilder ab, FunctionExport fn, EX_EndMap e) {
        throw new NotImplementedException("EX_EndMap");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndMap");
        return new EX_EndMap() {
        };
    }
}
public class AEX_EndMapConst : AbstractKismetExpression {
    public AEX_EndMapConst() {}
    public AEX_EndMapConst(AbstractKismetBuilder ab, FunctionExport fn, EX_EndMapConst e) {
        throw new NotImplementedException("EX_EndMapConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndMapConst");
        return new EX_EndMapConst() {
        };
    }
}
public class AEX_EndOfScript : AbstractKismetExpression {
    public AEX_EndOfScript() {}
    public AEX_EndOfScript(AbstractKismetBuilder ab, FunctionExport fn, EX_EndOfScript e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_EndOfScript();
    }
}
public class AEX_EndParmValue : AbstractKismetExpression {
    public AEX_EndParmValue() {}
    public AEX_EndParmValue(AbstractKismetBuilder ab, FunctionExport fn, EX_EndParmValue e) {
        throw new NotImplementedException("EX_EndParmValue");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndParmValue");
        return new EX_EndParmValue() {
        };
    }
}
public class AEX_EndSet : AbstractKismetExpression {
    public AEX_EndSet() {}
    public AEX_EndSet(AbstractKismetBuilder ab, FunctionExport fn, EX_EndSet e) {
        throw new NotImplementedException("EX_EndSet");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndSet");
        return new EX_EndSet() {
        };
    }
}
public class AEX_EndSetConst : AbstractKismetExpression {
    public AEX_EndSetConst() {}
    public AEX_EndSetConst(AbstractKismetBuilder ab, FunctionExport fn, EX_EndSetConst e) {
        throw new NotImplementedException("EX_EndSetConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndSetConst");
        return new EX_EndSetConst() {
        };
    }
}
public class AEX_EndStructConst : AbstractKismetExpression {
    public AEX_EndStructConst() {}
    public AEX_EndStructConst(AbstractKismetBuilder ab, FunctionExport fn, EX_EndStructConst e) {
        throw new NotImplementedException("EX_EndStructConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_EndStructConst");
        return new EX_EndStructConst() {
        };
    }
}
public class AEX_False : AbstractKismetExpression {
    public AEX_False() {}
    public AEX_False(AbstractKismetBuilder ab, FunctionExport fn, EX_False e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_False();
    }
}
public class AEX_FieldPathConst : AbstractKismetExpression {
    public AEX_FieldPathConst() {}
    public AEX_FieldPathConst(AbstractKismetBuilder ab, FunctionExport fn, EX_FieldPathConst e) {
        throw new NotImplementedException("EX_FieldPathConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_FieldPathConst");
        return new EX_FieldPathConst() {
        };
    }
}
public class AEX_FinalFunction : AbstractKismetExpression {
    public AEX_FinalFunction() {}
    public string StackNode { get; set; } = null!;
    public IEnumerable<AbstractKismetExpression> Parameters { get; set; } = null!;

    public AEX_FinalFunction(AbstractKismetBuilder ab, FunctionExport fn, EX_FinalFunction e) {
        StackNode = ab.FromPackageIndex(e.StackNode);
        Parameters = e.Parameters.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_FinalFunction() {
            StackNode = b.ToPackageIndex(StackNode),
            Parameters = Parameters.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_FloatConst : AbstractKismetExpression {
    public AEX_FloatConst() {}
    public float Value { get; set; }

    public AEX_FloatConst(AbstractKismetBuilder ab, FunctionExport fn, EX_FloatConst e) {
        Value = e.Value;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_FloatConst() {
            Value = Value,
        };
    }
}
public class AEX_DoubleConst : AbstractKismetExpression {
    public AEX_DoubleConst() {}
    public double Value { get; set; }

    public AEX_DoubleConst(AbstractKismetBuilder ab, FunctionExport fn, EX_DoubleConst e) {
        Value = e.Value;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_DoubleConst() {
            Value = Value,
        };
    }
}
public class AEX_InstanceDelegate : AbstractKismetExpression {
    public AEX_InstanceDelegate() {}
    public AEX_InstanceDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_InstanceDelegate e) {
        throw new NotImplementedException("EX_InstanceDelegate");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_InstanceDelegate");
        return new EX_InstanceDelegate() {
        };
    }
}
public class AEX_InstanceVariable : AbstractKismetExpression {
    public AEX_InstanceVariable() {}
    public string Variable { get; set; } = null!;

    public AEX_InstanceVariable(AbstractKismetBuilder ab, FunctionExport fn, EX_InstanceVariable e) {
        Variable = ab.FromKismetPropertyPointer(e.Variable);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_InstanceVariable() {
            Variable = b.ToKismetPropertyPointer(Variable),
        };
    }
}
public class AEX_InstrumentationEvent : AbstractKismetExpression {
    public AEX_InstrumentationEvent() {}
    public AEX_InstrumentationEvent(AbstractKismetBuilder ab, FunctionExport fn, EX_InstrumentationEvent e) {
        throw new NotImplementedException("EX_InstrumentationEvent");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_InstrumentationEvent");
        return new EX_InstrumentationEvent() {
        };
    }
}
public class AEX_Int64Const : AbstractKismetExpression {
    public AEX_Int64Const() {}
    public long Value { get; set; }

    public AEX_Int64Const(AbstractKismetBuilder ab, FunctionExport fn, EX_Int64Const e) {
        Value = e.Value;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_Int64Const() {
            Value = Value,
        };
    }
}
public class AEX_IntConst : AbstractKismetExpression {
    public AEX_IntConst() {}
    public int Value { get; set; }

    public AEX_IntConst(AbstractKismetBuilder ab, FunctionExport fn, EX_IntConst e) {
        Value = e.Value;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_IntConst() {
            Value = Value,
        };
    }
}
public class AEX_IntConstByte : AbstractKismetExpression {
    public AEX_IntConstByte() {}
    public byte Value { get; set; }

    public AEX_IntConstByte(AbstractKismetBuilder ab, FunctionExport fn, EX_IntConstByte e) {
        Value = e.Value;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_IntConstByte() {
            Value = Value,
        };
    }
}
public class AEX_IntOne : AbstractKismetExpression {
    public AEX_IntOne() {}
    public AEX_IntOne(AbstractKismetBuilder ab, FunctionExport fn, EX_IntOne e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_IntOne();
    }
}
public class AEX_IntZero : AbstractKismetExpression {
    public AEX_IntZero() {}
    public AEX_IntZero(AbstractKismetBuilder ab, FunctionExport fn, EX_IntZero e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_IntZero();
    }
}
public class AEX_InterfaceContext : AbstractKismetExpression {
    public AEX_InterfaceContext() {}
    public AEX_InterfaceContext(AbstractKismetBuilder ab, FunctionExport fn, EX_InterfaceContext e) {
        throw new NotImplementedException("EX_InterfaceContext");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_InterfaceContext");
        return new EX_InterfaceContext() {
        };
    }
}
public class AEX_InterfaceToObjCast : AbstractKismetExpression {
    public AEX_InterfaceToObjCast() {}
    public AEX_InterfaceToObjCast(AbstractKismetBuilder ab, FunctionExport fn, EX_InterfaceToObjCast e) {
        throw new NotImplementedException("EX_InterfaceToObjCast");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_InterfaceToObjCast");
        return new EX_InterfaceToObjCast() {
        };
    }
}
public class AEX_Jump : AbstractKismetExpression {
    public AEX_Jump() {}
    public string CodeOffset { get; set; } = null!;

    public AEX_Jump(AbstractKismetBuilder ab, FunctionExport fn, EX_Jump e) {
        CodeOffset = ab.GetLabelOrInvalid((fn.ObjectName.ToString(), e.CodeOffset));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        var ex = new EX_Jump();
        b.ResolveLabel(CodeOffset, offset => ex.CodeOffset = offset);
        return ex;
    }
}
public class AEX_JumpIfNot : AbstractKismetExpression {
    public AEX_JumpIfNot() {}
    public string CodeOffset { get; set; } = null!;
    public AbstractKismetExpression BooleanExpression { get; set; } = null!;

    public AEX_JumpIfNot(AbstractKismetBuilder ab, FunctionExport fn, EX_JumpIfNot e) {
        CodeOffset = ab.GetLabelOrInvalid((fn.ObjectName.ToString(), e.CodeOffset));
        BooleanExpression = ab.FromKismetExpression(fn, e.BooleanExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        var ex = new EX_JumpIfNot() {
            BooleanExpression = BooleanExpression.ToKismetExpression(b),
        };
        b.ResolveLabel(CodeOffset, offset => ex.CodeOffset = offset);
        return ex;
    }
}
public class AEX_Let : AbstractKismetExpression {
    public AEX_Let() {}
    public string Value { get; set; } = null!;
    public AbstractKismetExpression Variable { get; set; } = null!;
    public AbstractKismetExpression Expression { get; set; } = null!;

    public AEX_Let(AbstractKismetBuilder ab, FunctionExport fn, EX_Let e) {
        Value = ab.FromKismetPropertyPointer(e.Value);
        Variable = ab.FromKismetExpression(fn, e.Variable);
        Expression = ab.FromKismetExpression(fn, e.Expression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_Let() {
            Value = b.ToKismetPropertyPointer(Value),
            Variable = Variable.ToKismetExpression(b),
            Expression = Expression.ToKismetExpression(b),
        };
    }
}
public class AEX_LetBool : AbstractKismetExpression {
    public AEX_LetBool() {}
    public AbstractKismetExpression VariableExpression { get; set; } = null!;
    public AbstractKismetExpression AssignmentExpression { get; set; } = null!;

    public AEX_LetBool(AbstractKismetBuilder ab, FunctionExport fn, EX_LetBool e) {
        VariableExpression = ab.FromKismetExpression(fn, e.VariableExpression);
        AssignmentExpression = ab.FromKismetExpression(fn, e.AssignmentExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_LetBool() {
            VariableExpression = VariableExpression.ToKismetExpression(b),
            AssignmentExpression = AssignmentExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_LetDelegate : AbstractKismetExpression {
    public AEX_LetDelegate() {}
    public AEX_LetDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_LetDelegate e) {
        throw new NotImplementedException("EX_LetDelegate");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_LetDelegate");
        return new EX_LetDelegate() {
        };
    }
}
public class AEX_LetMulticastDelegate : AbstractKismetExpression {
    public AEX_LetMulticastDelegate() {}
    public AEX_LetMulticastDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_LetMulticastDelegate e) {
        throw new NotImplementedException("EX_LetMulticastDelegate");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_LetMulticastDelegate");
        return new EX_LetMulticastDelegate() {
        };
    }
}
public class AEX_LetObj : AbstractKismetExpression {
    public AEX_LetObj() {}
    public AbstractKismetExpression VariableExpression { get; set; } = null!;
    public AbstractKismetExpression AssignmentExpression { get; set; } = null!;

    public AEX_LetObj(AbstractKismetBuilder ab, FunctionExport fn, EX_LetObj e) {
        VariableExpression = ab.FromKismetExpression(fn, e.VariableExpression);
        AssignmentExpression = ab.FromKismetExpression(fn, e.AssignmentExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_LetObj() {
            VariableExpression = VariableExpression.ToKismetExpression(b),
            AssignmentExpression = AssignmentExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_LetValueOnPersistentFrame : AbstractKismetExpression {
    public AEX_LetValueOnPersistentFrame() {}
    public string DestinationProperty { get; set; } = null!;
    public AbstractKismetExpression AssignmentExpression { get; set; } = null!;

    public AEX_LetValueOnPersistentFrame(AbstractKismetBuilder ab, FunctionExport fn, EX_LetValueOnPersistentFrame e) {
        DestinationProperty = ab.FromKismetPropertyPointer(e.DestinationProperty);
        AssignmentExpression = ab.FromKismetExpression(fn, e.AssignmentExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_LetValueOnPersistentFrame() {
            DestinationProperty = b.ToKismetPropertyPointer(DestinationProperty),
            AssignmentExpression = AssignmentExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_LetWeakObjPtr : AbstractKismetExpression {
    public AEX_LetWeakObjPtr() {}
    public AEX_LetWeakObjPtr(AbstractKismetBuilder ab, FunctionExport fn, EX_LetWeakObjPtr e) {
        throw new NotImplementedException("EX_LetWeakObjPtr");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_LetWeakObjPtr");
        return new EX_LetWeakObjPtr() {
        };
    }
}
public class AEX_LocalFinalFunction : AbstractKismetExpression {
    public AEX_LocalFinalFunction() {}
    public string StackNode { get; set; } = null!;
    public IEnumerable<AbstractKismetExpression> Parameters { get; set; } = null!;

    public AEX_LocalFinalFunction(AbstractKismetBuilder ab, FunctionExport fn, EX_LocalFinalFunction e) {
        StackNode = ab.FromPackageIndex(e.StackNode);
        Parameters = e.Parameters.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_LocalFinalFunction() {
            StackNode = b.ToPackageIndex(StackNode),
            Parameters = Parameters.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_LocalOutVariable : AbstractKismetExpression {
    public AEX_LocalOutVariable() {}
    public string Variable { get; set; } = null!;

    public AEX_LocalOutVariable(AbstractKismetBuilder ab, FunctionExport fn, EX_LocalOutVariable e) {
        Variable = ab.FromKismetPropertyPointer(e.Variable);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_LocalOutVariable() {
            Variable = b.ToKismetPropertyPointer(Variable),
        };
    }
}
public class AEX_LocalVariable : AbstractKismetExpression {
    public AEX_LocalVariable() {}
    public string Variable { get; set; } = null!;

    public AEX_LocalVariable(AbstractKismetBuilder ab, FunctionExport fn, EX_LocalVariable e) {
        Variable = ab.FromKismetPropertyPointer(e.Variable);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_LocalVariable() {
            Variable = b.ToKismetPropertyPointer(Variable),
        };
    }
}
public class AEX_LocalVirtualFunction : AbstractKismetExpression {
    public AEX_LocalVirtualFunction() {}
    public string VirtualFunctionName { get; set; } = null!;
    public IEnumerable<AbstractKismetExpression> Parameters { get; set; } = null!;

    public AEX_LocalVirtualFunction(AbstractKismetBuilder ab, FunctionExport fn, EX_LocalVirtualFunction e) {
        VirtualFunctionName = e.VirtualFunctionName.ToString();
        Parameters = e.Parameters.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_LocalVirtualFunction() {
            VirtualFunctionName = b.ToFName(VirtualFunctionName),
            Parameters = Parameters.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_MapConst : AbstractKismetExpression {
    public AEX_MapConst() {}
    public AEX_MapConst(AbstractKismetBuilder ab, FunctionExport fn, EX_MapConst e) {
        throw new NotImplementedException("EX_MapConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_MapConst");
        return new EX_MapConst() {
        };
    }
}
public class AEX_MetaCast : AbstractKismetExpression {
    public AEX_MetaCast() {}
    public AEX_MetaCast(AbstractKismetBuilder ab, FunctionExport fn, EX_MetaCast e) {
        throw new NotImplementedException("EX_MetaCast");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_MetaCast");
        return new EX_MetaCast() {
        };
    }
}
public class AEX_NameConst : AbstractKismetExpression {
    public AEX_NameConst() {}
    public string Value { get; set; } = null!;
    public AEX_NameConst(AbstractKismetBuilder ab, FunctionExport fn, EX_NameConst e) {
        Value = e.Value.ToString();
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_NameConst() {
            Value = b.ToFName(Value),
        };
    }
}
public class AEX_NoInterface : AbstractKismetExpression {
    public AEX_NoInterface() {}
    public AEX_NoInterface(AbstractKismetBuilder ab, FunctionExport fn, EX_NoInterface e) {
        throw new NotImplementedException("EX_NoInterface");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_NoInterface");
        return new EX_NoInterface() {
        };
    }
}
public class AEX_NoObject : AbstractKismetExpression {
    public AEX_NoObject() {}
    public AEX_NoObject(AbstractKismetBuilder ab, FunctionExport fn, EX_NoObject e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_NoObject();
    }
}
public class AEX_Nothing : AbstractKismetExpression {
    public AEX_Nothing() {}
    public AEX_Nothing(AbstractKismetBuilder ab, FunctionExport fn, EX_Nothing e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_Nothing();
    }
}
public class AEX_ObjToInterfaceCast : AbstractKismetExpression {
    public AEX_ObjToInterfaceCast() {}
    public AEX_ObjToInterfaceCast(AbstractKismetBuilder ab, FunctionExport fn, EX_ObjToInterfaceCast e) {
        throw new NotImplementedException("EX_ObjToInterfaceCast");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_ObjToInterfaceCast");
        return new EX_ObjToInterfaceCast() {
        };
    }
}
public class AEX_ObjectConst : AbstractKismetExpression {
    public AEX_ObjectConst() {}
    public string Value { get; set; } = null!;

    public AEX_ObjectConst(AbstractKismetBuilder ab, FunctionExport fn, EX_ObjectConst e) {
        Value = ab.FromPackageIndex(e.Value);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_ObjectConst() {
            Value = b.ToPackageIndex(Value),
        };
    }
}
public class AEX_PopExecutionFlow : AbstractKismetExpression {
    public AEX_PopExecutionFlow() {}
    public AEX_PopExecutionFlow(AbstractKismetBuilder ab, FunctionExport fn, EX_PopExecutionFlow e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_PopExecutionFlow();
    }
}
public class AEX_PopExecutionFlowIfNot : AbstractKismetExpression {
    public AEX_PopExecutionFlowIfNot() {}
    public AbstractKismetExpression BooleanExpression { get; set; } = null!;

    public AEX_PopExecutionFlowIfNot(AbstractKismetBuilder ab, FunctionExport fn, EX_PopExecutionFlowIfNot e) {
        BooleanExpression = ab.FromKismetExpression(fn, e.BooleanExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_PopExecutionFlowIfNot() {
            BooleanExpression = BooleanExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_PrimitiveCast : AbstractKismetExpression {
    public AEX_PrimitiveCast() {}
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ECastToken ConversionType { get; set; }
    public AbstractKismetExpression Target { get; set; } = null!;

    public AEX_PrimitiveCast(AbstractKismetBuilder ab, FunctionExport fn, EX_PrimitiveCast e) {
        ConversionType = e.ConversionType;
        Target = ab.FromKismetExpression(fn, e.Target);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_PrimitiveCast() {
            ConversionType = ConversionType,
            Target = Target.ToKismetExpression(b),
        };
    }
}
public class AEX_PropertyConst : AbstractKismetExpression {
    public AEX_PropertyConst() {}
    public AEX_PropertyConst(AbstractKismetBuilder ab, FunctionExport fn, EX_PropertyConst e) {
        throw new NotImplementedException("EX_PropertyConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_PropertyConst");
        return new EX_PropertyConst() {
        };
    }
}
public class AEX_PushExecutionFlow : AbstractKismetExpression {
    public AEX_PushExecutionFlow() {}
    public string PushingAddress { get; set; } = null!;

    public AEX_PushExecutionFlow(AbstractKismetBuilder ab, FunctionExport fn, EX_PushExecutionFlow e) {
        PushingAddress = ab.GetLabelOrInvalid((fn.ObjectName.ToString(), e.PushingAddress));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        var ex = new EX_PushExecutionFlow();
        b.ResolveLabel(PushingAddress, offset => ex.PushingAddress = offset);
        return ex;
    }
}
public class AEX_RemoveMulticastDelegate : AbstractKismetExpression {
    public AEX_RemoveMulticastDelegate() {}
    public AEX_RemoveMulticastDelegate(AbstractKismetBuilder ab, FunctionExport fn, EX_RemoveMulticastDelegate e) {
        throw new NotImplementedException("EX_RemoveMulticastDelegate");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_RemoveMulticastDelegate");
        return new EX_RemoveMulticastDelegate() {
        };
    }
}
public class AEX_Return : AbstractKismetExpression {
    public AEX_Return() {}
    public AbstractKismetExpression ReturnExpression { get; set; } = null!;

    public AEX_Return(AbstractKismetBuilder ab, FunctionExport fn, EX_Return e) {
        ReturnExpression = ab.FromKismetExpression(fn, e.ReturnExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_Return() {
            ReturnExpression = ReturnExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_RotationConst : AbstractKismetExpression {
    public AEX_RotationConst() {}
    public AEX_RotationConst(AbstractKismetBuilder ab, FunctionExport fn, EX_RotationConst e) {
        throw new NotImplementedException("EX_RotationConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_RotationConst");
        return new EX_RotationConst() {
        };
    }
}
public class AEX_Self : AbstractKismetExpression {
    public AEX_Self() {}
    public AEX_Self(AbstractKismetBuilder ab, FunctionExport fn, EX_Self e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_Self();
    }
}
public class AEX_SetArray : AbstractKismetExpression {
    public AEX_SetArray() {}
    public AbstractKismetExpression AssigningProperty { get; set; } = null!;
    public string ArrayInnerProp { get; set; } = null!;
    public IEnumerable<AbstractKismetExpression> Elements { get; set; } = null!;

    public AEX_SetArray(AbstractKismetBuilder ab, FunctionExport fn, EX_SetArray e) {
        AssigningProperty = ab.FromKismetExpression(fn, e.AssigningProperty);
        ArrayInnerProp = ab.FromPackageIndex(e.ArrayInnerProp);
        Elements = e.Elements.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_SetArray() {
            AssigningProperty = AssigningProperty.ToKismetExpression(b),
            ArrayInnerProp = b.ToPackageIndex(ArrayInnerProp),
            Elements = Elements.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_SetConst : AbstractKismetExpression {
    public AEX_SetConst() {}
    public AEX_SetConst(AbstractKismetBuilder ab, FunctionExport fn, EX_SetConst e) {
        throw new NotImplementedException("EX_SetConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_SetConst");
        return new EX_SetConst() {
        };
    }
}
public class AEX_SetMap : AbstractKismetExpression {
    public AEX_SetMap() {}
    public AEX_SetMap(AbstractKismetBuilder ab, FunctionExport fn, EX_SetMap e) {
        throw new NotImplementedException("EX_SetMap");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_SetMap");
        return new EX_SetMap() {
        };
    }
}
public class AEX_SetSet : AbstractKismetExpression {
    public AEX_SetSet() {}
    public AEX_SetSet(AbstractKismetBuilder ab, FunctionExport fn, EX_SetSet e) {
        throw new NotImplementedException("EX_SetSet");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_SetSet");
        return new EX_SetSet() {
        };
    }
}
public class AEX_Skip : AbstractKismetExpression {
    public AEX_Skip() {}
    public AEX_Skip(AbstractKismetBuilder ab, FunctionExport fn, EX_Skip e) {
        throw new NotImplementedException("EX_Skip");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_Skip");
        return new EX_Skip() {
        };
    }
}
public class AEX_SkipOffsetConst : AbstractKismetExpression {
    public AEX_SkipOffsetConst() {}
    public string Value { get; set; } = null!;

    public AEX_SkipOffsetConst(AbstractKismetBuilder ab, FunctionExport fn, EX_SkipOffsetConst e) {
        Value = ab.GetLabelOrInvalid((fn.ObjectName.ToString(), e.Value));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        var ex = new EX_SkipOffsetConst();
        b.ResolveLabel(Value, offset => ex.Value = offset);
        return ex;
    }
}
public class AEX_SoftObjectConst : AbstractKismetExpression {
    public AEX_SoftObjectConst() {}
    public AEX_SoftObjectConst(AbstractKismetBuilder ab, FunctionExport fn, EX_SoftObjectConst e) {
        throw new NotImplementedException("EX_SoftObjectConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_SoftObjectConst");
        return new EX_SoftObjectConst() {
        };
    }
}
public class AEX_StringConst : AbstractKismetExpression {
    public AEX_StringConst() {}
    public string Value { get; set; } = null!;

    public AEX_StringConst(AbstractKismetBuilder ab, FunctionExport fn, EX_StringConst e) {
        Value = e.Value;
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_StringConst() {
            Value = Value,
        };
    }
}
public class AEX_StructConst : AbstractKismetExpression {
    public AEX_StructConst() {}
    public string Struct { get; set; } = null!;
    public int StructSize { get; set; }
    public IEnumerable<AbstractKismetExpression> Value { get; set; } = null!;

    public AEX_StructConst(AbstractKismetBuilder ab, FunctionExport fn, EX_StructConst e) {
        Struct = ab.FromPackageIndex(e.Struct);
        StructSize = e.StructSize;
        Value = e.Value.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_StructConst() {
            Struct = b.ToPackageIndex(Struct),
            StructSize = StructSize,
            Value = Value.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_StructMemberContext : AbstractKismetExpression {
    public AEX_StructMemberContext() {}
    public string StructMemberExpression { get; set; } = null!;
    public AbstractKismetExpression StructExpression { get; set; } = null!;

    public AEX_StructMemberContext(AbstractKismetBuilder ab, FunctionExport fn, EX_StructMemberContext e) {
        StructMemberExpression = ab.FromKismetPropertyPointer(e.StructMemberExpression);
        StructExpression = ab.FromKismetExpression(fn, e.StructExpression);
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_StructMemberContext() {
            StructMemberExpression = b.ToKismetPropertyPointer(StructMemberExpression),
            StructExpression = StructExpression.ToKismetExpression(b),
        };
    }
}
public class AEX_SwitchValue : AbstractKismetExpression {
    public AEX_SwitchValue() {}
    public struct SwitchCase {
        public AbstractKismetExpression Index { get; set; } = null!;
        public AbstractKismetExpression Value { get; set; } = null!;

        public SwitchCase(AbstractKismetExpression index, AbstractKismetExpression term) {
            Index = index;
            Value = term;
        }
    }
    public AbstractKismetExpression IndexTerm { get; set; } = null!;
    public AbstractKismetExpression DefaultTerm { get; set; } = null!;
    public IEnumerable<SwitchCase> Cases { get; set; } = null!;

    public AEX_SwitchValue(AbstractKismetBuilder ab, FunctionExport fn, EX_SwitchValue e) {
        IndexTerm = ab.FromKismetExpression(fn, e.IndexTerm);
        DefaultTerm = ab.FromKismetExpression(fn, e.DefaultTerm);
        Cases = e.Cases.Select(e => new SwitchCase(ab.FromKismetExpression(fn, e.CaseIndexValueTerm), ab.FromKismetExpression(fn, e.CaseTerm)));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_SwitchValue() {
            IndexTerm = IndexTerm.ToKismetExpression(b),
            DefaultTerm = DefaultTerm.ToKismetExpression(b),
            Cases = Cases.Select(e => new FKismetSwitchCase(e.Index.ToKismetExpression(b), 0, e.Value.ToKismetExpression(b))).ToArray(),
        };
    }
}
public class AEX_TextConst : AbstractKismetExpression {
    public AEX_TextConst() {}
    public AEX_TextConst(AbstractKismetBuilder ab, FunctionExport fn, EX_TextConst e) {
        throw new NotImplementedException("EX_TextConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_TextConst");
        return new EX_TextConst() {
        };
    }
}
public class AEX_Tracepoint : AbstractKismetExpression {
    public AEX_Tracepoint() {}
    public AEX_Tracepoint(AbstractKismetBuilder ab, FunctionExport fn, EX_Tracepoint e) {
        throw new NotImplementedException("EX_Tracepoint");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_Tracepoint");
        return new EX_Tracepoint() {
        };
    }
}
public class AEX_TransformConst : AbstractKismetExpression {
    public AEX_TransformConst() {}
    public AEX_TransformConst(AbstractKismetBuilder ab, FunctionExport fn, EX_TransformConst e) {
        throw new NotImplementedException("EX_TransformConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_TransformConst");
        return new EX_TransformConst() {
        };
    }
}
public class AEX_True : AbstractKismetExpression {
    public AEX_True() {}
    public AEX_True(AbstractKismetBuilder ab, FunctionExport fn, EX_True e) {}
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_True();
    }
}
public class AEX_UInt64Const : AbstractKismetExpression {
    public AEX_UInt64Const() {}
    public AEX_UInt64Const(AbstractKismetBuilder ab, FunctionExport fn, EX_UInt64Const e) {
        throw new NotImplementedException("EX_UInt64Const");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_UInt64Const");
        return new EX_UInt64Const() {
        };
    }
}
public class AEX_UnicodeStringConst : AbstractKismetExpression {
    public AEX_UnicodeStringConst() {}
    public AEX_UnicodeStringConst(AbstractKismetBuilder ab, FunctionExport fn, EX_UnicodeStringConst e) {
        throw new NotImplementedException("EX_UnicodeStringConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_UnicodeStringConst");
        return new EX_UnicodeStringConst() {
        };
    }
}
public class AEX_VectorConst : AbstractKismetExpression {
    public AEX_VectorConst() {}
    public AEX_VectorConst(AbstractKismetBuilder ab, FunctionExport fn, EX_VectorConst e) {
        throw new NotImplementedException("EX_VectorConst");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_VectorConst");
        return new EX_VectorConst() {
        };
    }
}
public class AEX_VirtualFunction : AbstractKismetExpression {
    public AEX_VirtualFunction() {}
    public string VirtualFunctionName { get; set; } = null!;
    public IEnumerable<AbstractKismetExpression> Parameters { get; set; } = null!;

    public AEX_VirtualFunction(AbstractKismetBuilder ab, FunctionExport fn, EX_VirtualFunction e) {
        VirtualFunctionName = e.VirtualFunctionName.ToString();
        Parameters = e.Parameters.Select(e => ab.FromKismetExpression(fn, e));
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        return new EX_VirtualFunction() {
            VirtualFunctionName = b.ToFName(VirtualFunctionName),
            Parameters = Parameters.Select(e => e.ToKismetExpression(b)).ToArray(),
        };
    }
}
public class AEX_WireTracepoint : AbstractKismetExpression {
    public AEX_WireTracepoint() {}
    public AEX_WireTracepoint(AbstractKismetBuilder ab, FunctionExport fn, EX_WireTracepoint e) {
        throw new NotImplementedException("EX_WireTracepoint");
    }
    public override KismetExpression ToKismetExpression(KismetBuilder b) {
        throw new NotImplementedException("EX_WireTracepoint");
        return new EX_WireTracepoint() {
        };
    }
}
