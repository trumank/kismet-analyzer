namespace KismetAnalyzer;

using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

public class Kismet {
    public static void Walk(UAsset asset, KismetExpression ex, Action<KismetExpression> func) {
        uint offset = 0;
        Walk(asset, ref offset, ex, (ex, offset) => func(ex));
    }
    public static void Walk(UAsset asset, ref uint offset, KismetExpression ex, Action<KismetExpression, uint> func) {
        func(ex, offset);
        offset++;
        switch (ex) {
            case EX_FieldPathConst e:
                Walk(asset, ref offset, e.Value, func);
                break;
            case EX_SoftObjectConst e:
                Walk(asset, ref offset, e.Value, func);
                break;
            case EX_AddMulticastDelegate e:
                Walk(asset, ref offset, e.Delegate, func);
                Walk(asset, ref offset, e.DelegateToAdd, func);
                break;
            case EX_ArrayConst e:
                offset += 8;
                foreach (var p in e.Elements) Walk(asset, ref offset, p, func);
                break;
            case EX_ArrayGetByRef e:
                Walk(asset, ref offset, e.ArrayVariable, func);
                Walk(asset, ref offset, e.ArrayIndex, func);
                break;
            case EX_Assert e:
                offset += 3;
                Walk(asset, ref offset, e.AssertExpression, func);
                break;
            case EX_BindDelegate e:
                offset += 12;
                Walk(asset, ref offset, e.Delegate, func);
                Walk(asset, ref offset, e.ObjectTerm, func);
                break;
            case EX_CallMath e:
                offset += 8;
                foreach (var p in e.Parameters) Walk(asset, ref offset, p, func);
                offset += 1;
                break;
            case EX_CallMulticastDelegate e:
                offset += 8;
                Walk(asset, ref offset, e.Delegate, func);
                foreach (var p in e.Parameters) Walk(asset, ref offset, p, func);
                offset += 1;
                break;
            case EX_ClearMulticastDelegate e:
                Walk(asset, ref offset, e.DelegateToClear, func);
                break;
            case EX_ComputedJump e:
                Walk(asset, ref offset, e.CodeOffsetExpression, func);
                break;
            case EX_Context e: // +EX_Context_FailSilent +EX_ClassContext
                Walk(asset, ref offset, e.ObjectExpression, func);
                offset += 12;
                Walk(asset, ref offset, e.ContextExpression, func);
                break;
            case EX_CrossInterfaceCast e:
                offset += 8;
                Walk(asset, ref offset, e.Target, func);
                break;
            case EX_DynamicCast e:
                offset += 8;
                Walk(asset, ref offset, e.TargetExpression, func);
                break;
            case EX_FinalFunction e: // +EX_LocalFinalFunction
                offset += 8;
                foreach (var p in e.Parameters) Walk(asset, ref offset, p, func);
                offset += 1;
                break;
            case EX_InterfaceContext e:
                Walk(asset, ref offset, e.InterfaceValue, func);
                break;
            case EX_InterfaceToObjCast e:
                offset += 8;
                Walk(asset, ref offset, e.Target, func);
                break;
            case EX_JumpIfNot e:
                offset += 4;
                Walk(asset, ref offset, e.BooleanExpression, func);
                break;
            case EX_Let e:
                offset += 8;
                Walk(asset, ref offset, e.Variable, func);
                Walk(asset, ref offset, e.Expression, func);
                break;
            case EX_LetBool e:
                Walk(asset, ref offset, e.VariableExpression, func);
                Walk(asset, ref offset, e.AssignmentExpression, func);
                break;
            case EX_LetDelegate e:
                Walk(asset, ref offset, e.VariableExpression, func);
                Walk(asset, ref offset, e.AssignmentExpression, func);
                break;
            case EX_LetMulticastDelegate e:
                Walk(asset, ref offset, e.VariableExpression, func);
                Walk(asset, ref offset, e.AssignmentExpression, func);
                break;
            case EX_LetObj e:
                Walk(asset, ref offset, e.VariableExpression, func);
                Walk(asset, ref offset, e.AssignmentExpression, func);
                break;
            case EX_LetValueOnPersistentFrame e:
                offset += 8;
                Walk(asset, ref offset, e.AssignmentExpression, func);
                break;
            case EX_LetWeakObjPtr e:
                Walk(asset, ref offset, e.VariableExpression, func);
                Walk(asset, ref offset, e.AssignmentExpression, func);
                break;
            case EX_VirtualFunction e: // +EX_LocalVirtualFunction
                offset += 12;
                foreach (var p in e.Parameters) Walk(asset, ref offset, p, func);
                offset += 1;
                break;
            case EX_MapConst e:
                offset += 20;
                foreach (var p in e.Elements) Walk(asset, ref offset, p, func);
                break;
            case EX_MetaCast e:
                offset += 8;
                Walk(asset, ref offset, e.TargetExpression, func);
                break;
            case EX_ObjToInterfaceCast e:
                offset += 8;
                Walk(asset, ref offset, e.Target, func);
                break;
            case EX_PopExecutionFlowIfNot e:
                Walk(asset, ref offset, e.BooleanExpression, func);
                break;
            case EX_PrimitiveCast e:
                offset += 1;
                Walk(asset, ref offset, e.Target, func);
                break;
            case EX_RemoveMulticastDelegate e:
                Walk(asset, ref offset, e.Delegate, func);
                Walk(asset, ref offset, e.DelegateToAdd, func);
                break;
            case EX_Return e:
                Walk(asset, ref offset, e.ReturnExpression, func);
                break;
            case EX_SetArray e:
                Walk(asset, ref offset, e.AssigningProperty, func);
                foreach (var p in e.Elements) Walk(asset, ref offset, p, func);
                offset += 1;
                break;
            case EX_SetConst e:
                offset += 12;
                foreach (var p in e.Elements) Walk(asset, ref offset, p, func);
                offset += 1;
                break;
            case EX_SetMap e:
                Walk(asset, ref offset, e.MapProperty, func);
                offset += 4;
                foreach (var p in e.Elements) Walk(asset, ref offset, p, func);
                break;
            case EX_SetSet e:
                Walk(asset, ref offset, e.SetProperty, func);
                offset += 4;
                foreach (var p in e.Elements) Walk(asset, ref offset, p, func);
                break;
            case EX_Skip e:
                offset += 4;
                Walk(asset, ref offset, e.SkipExpression, func);
                break;
            case EX_StructConst e:
                offset += 12;
                foreach (var p in e.Value) Walk(asset, ref offset, p, func);
                offset += 1;
                break;
            case EX_StructMemberContext e:
                offset += 8;
                Walk(asset, ref offset, e.StructExpression, func);
                break;
            case EX_SwitchValue e:
                offset += 6;
                Walk(asset, ref offset, e.IndexTerm, func);
                foreach (var p in e.Cases) {
                    Walk(asset, ref offset, p.CaseIndexValueTerm, func);
                    offset += 4;
                    Walk(asset, ref offset, p.CaseTerm, func);
                }
                Walk(asset, ref offset, e.DefaultTerm, func);
                break;
            case EX_TextConst e:
                offset += 1;
                switch (e.Value.TextLiteralType) {
                    case EBlueprintTextLiteralType.Empty:
                        break;
                    case EBlueprintTextLiteralType.LocalizedText:
                        Walk(asset, ref offset, e.Value.LocalizedSource, func);
                        Walk(asset, ref offset, e.Value.LocalizedKey, func);
                        Walk(asset, ref offset, e.Value.LocalizedNamespace, func);
                        break;
                    case EBlueprintTextLiteralType.InvariantText:
                        Walk(asset, ref offset, e.Value.InvariantLiteralString, func);
                        break;
                    case EBlueprintTextLiteralType.LiteralString:
                        Walk(asset, ref offset, e.Value.LiteralString, func);
                        break;
                    case EBlueprintTextLiteralType.StringTableEntry:
                        offset += 8;
                        Walk(asset, ref offset, e.Value.StringTableId, func);
                        Walk(asset, ref offset, e.Value.StringTableKey, func);
                        break;
                    default:
                        throw new NotImplementedException();
                };
                break;
            default:
                offset += GetSize(asset, ex) - 1;
                break;
        }
    }
    public static uint GetSize(UAsset asset, KismetExpression exp) {
        return 1 + exp switch
        {
            EX_PushExecutionFlow => 4,
            EX_ComputedJump e => GetSize(asset, e.CodeOffsetExpression),
            EX_Jump e => 4,
            EX_JumpIfNot e => 4 + GetSize(asset, e.BooleanExpression),
            EX_LocalVariable e => 8,
            EX_DefaultVariable e => 8,
            EX_ObjToInterfaceCast e => 8 + GetSize(asset, e.Target),
            EX_Let e => 8 + GetSize(asset, e.Variable) + GetSize(asset, e.Expression),
            EX_LetObj e => GetSize(asset, e.VariableExpression) + GetSize(asset, e.AssignmentExpression),
            EX_LetBool e => GetSize(asset, e.VariableExpression) + GetSize(asset, e.AssignmentExpression),
            EX_LetWeakObjPtr e => GetSize(asset, e.VariableExpression) + GetSize(asset, e.AssignmentExpression),
            EX_LetValueOnPersistentFrame e => 8 + GetSize(asset, e.AssignmentExpression),
            EX_StructMemberContext e => 8 + GetSize(asset, e.StructExpression),
            EX_MetaCast e => 8 + GetSize(asset, e.TargetExpression),
            EX_DynamicCast e => 8 + GetSize(asset, e.TargetExpression),
            EX_PrimitiveCast e => 1 + e.ConversionType switch { ECastToken.ObjectToInterface => 8U, /* TODO InterfaceClass */ _ => 0U} + GetSize(asset, e.Target),
            EX_PopExecutionFlow e => 0,
            EX_PopExecutionFlowIfNot e => GetSize(asset, e.BooleanExpression),
            EX_CallMath e => 8 + e.Parameters.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_SwitchValue e => 6 + GetSize(asset, e.IndexTerm) + e.Cases.Select(c => GetSize(asset, c.CaseIndexValueTerm) + 4 + GetSize(asset, c.CaseTerm)).Aggregate(0U, (acc, x) => x + acc) + GetSize(asset, e.DefaultTerm),
            EX_Self => 0,
            EX_TextConst e =>
                1 + e.Value.TextLiteralType switch
                {
                    EBlueprintTextLiteralType.Empty => 0,
                    EBlueprintTextLiteralType.LocalizedText => GetSize(asset, e.Value.LocalizedSource) + GetSize(asset, e.Value.LocalizedKey) + GetSize(asset, e.Value.LocalizedNamespace),
                    EBlueprintTextLiteralType.InvariantText => GetSize(asset, e.Value.InvariantLiteralString),
                    EBlueprintTextLiteralType.LiteralString => GetSize(asset, e.Value.LiteralString),
                    EBlueprintTextLiteralType.StringTableEntry => 8 + GetSize(asset, e.Value.StringTableId) + GetSize(asset, e.Value.StringTableKey),
                    _ => throw new NotImplementedException(),
                },
            EX_ObjectConst e => 8,
            EX_VectorConst e => asset.ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? 24U : 12U,
            EX_RotationConst e => asset.ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? 24U : 12U,
            EX_TransformConst e => asset.ObjectVersionUE5 >= ObjectVersionUE5.LARGE_WORLD_COORDINATES ? 80U : 40U,
            EX_Context e => + GetSize(asset, e.ObjectExpression) + 4 + 8 + GetSize(asset, e.ContextExpression),
            EX_CallMulticastDelegate e => 8 + GetSize(asset, e.Delegate) + e.Parameters.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_LocalFinalFunction e => 8 + e.Parameters.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_FinalFunction e => 8 + e.Parameters.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_LocalVirtualFunction e => 12 + e.Parameters.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_VirtualFunction e => 12 + e.Parameters.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_InstanceVariable e => 8,
            EX_AddMulticastDelegate e => GetSize(asset, e.Delegate) + GetSize(asset, e.DelegateToAdd),
            EX_RemoveMulticastDelegate e => GetSize(asset, e.Delegate) + GetSize(asset, e.DelegateToAdd),
            EX_ClearMulticastDelegate e => GetSize(asset, e.DelegateToClear),
            EX_BindDelegate e => 12 + GetSize(asset, e.Delegate) + GetSize(asset, e.ObjectTerm),
            EX_StructConst e => 8 + 4 + e.Value.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_SetArray e => GetSize(asset, e.AssigningProperty) + e.Elements.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_SetMap e => GetSize(asset, e.MapProperty) + 4 + e.Elements.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_SetSet e => GetSize(asset, e.SetProperty) + 4 + e.Elements.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_SoftObjectConst e => GetSize(asset, e.Value),
            EX_ByteConst e => 1,
            EX_IntConst e => 4,
            EX_FloatConst e => 4,
            EX_Int64Const e => 8,
            EX_UInt64Const e => 8,
            EX_NameConst e => 12,
            EX_StringConst e => (uint) e.Value.Length + 1,
            EX_UnicodeStringConst e => 2 * ((uint) e.Value.Length + 1),
            EX_SkipOffsetConst e => 4,
            EX_ArrayConst e => 12 + e.Elements.Select(p => GetSize(asset, p)).Aggregate(0U, (acc, x) => x + acc) + 1,
            EX_Return e => GetSize(asset, e.ReturnExpression),
            EX_LocalOutVariable e => 8,
            EX_InterfaceContext e => GetSize(asset, e.InterfaceValue),
            EX_InterfaceToObjCast e => 8 + GetSize(asset, e.Target),
            EX_ArrayGetByRef e => GetSize(asset, e.ArrayVariable) + GetSize(asset, e.ArrayIndex),
            EX_True e => 0,
            EX_False e => 0,
            EX_Nothing e => 0,
            EX_NoObject e => 0,
            EX_EndOfScript e => 0,
            EX_Tracepoint e => 0,
            EX_WireTracepoint e => 0,
            _ => throw new NotImplementedException(exp.ToString()),
        };
    }

    public static void ShiftAddressses(KismetExpression exp, int offset) {
        switch (exp) {
            case EX_PushExecutionFlow e:
                {
                    e.PushingAddress = (uint) (e.PushingAddress + offset);
                    break;
                }
            case EX_ComputedJump e:
                {
                    // TODO
                    break;
                }
            case EX_Jump e:
                {
                    e.CodeOffset = (uint) (e.CodeOffset + offset);
                    break;
                }
            case EX_JumpIfNot e:
                {
                    e.CodeOffset = (uint) (e.CodeOffset + offset);
                    break;
                }
            case EX_LocalFinalFunction e:
                {
                    // TODO: Handle addressess in ubergraph
                    break;
                }
            case EX_StructConst e:
                {
                    // TODO handle LatentActionInfo addresses (only can be in ubergraph)
                    break;
                }
            case EX_SkipOffsetConst e:
                {
                    // referencedAddresses.Add(new Reference(e.Value, ReferenceType.Skip));
                    // TODO
                    break;
                }
        }
    }
    public static FPackageIndex? CopyImportTo((UAsset, FPackageIndex?) import, UAsset asset) {
        if (import.Item2 == null) return null;
        if (import.Item2.IsNull()) return import.Item2;
        for (int i = 0; i < asset.Imports.Count; i++) {
            var existing = FPackageIndex.FromImport(i);
            if (AreImportsEqual(import, (asset, existing))) return existing;
        }
        var imp = import.Item2.ToImport(import.Item1);
        if (imp.OuterIndex.IsNull()) {
            return asset.AddImport(new Import(imp.ClassPackage.ToString(), imp.ClassName.ToString(), FPackageIndex.FromRawIndex(0), imp.ObjectName.ToString(), false, asset));
        } else {
            return asset.AddImport(new Import(imp.ClassPackage.ToString(), imp.ClassName.ToString(), CopyImportTo((import.Item1, imp.OuterIndex), asset), imp.ObjectName.ToString(), false, asset));
        }
    }
    static bool AreImportsEqual((UAsset, FPackageIndex?) a, (UAsset, FPackageIndex?) b) {
        if (a.Item2 == null || b.Item2 == null) {
            return a.Item2 == null && b.Item2 == null;
        } if (a.Item2.IsNull() || b.Item2.IsNull()) {
            return a.Item2.IsNull() && b.Item2.IsNull();
        }
        var importA = a.Item2.ToImport(a.Item1);
        var importB = b.Item2.ToImport(b.Item1);
        return importA.ClassPackage == importB.ClassPackage
            && importA.ClassName == importB.ClassName
            && importA.ObjectName == importB.ObjectName
            && AreImportsEqual((a.Item1, importA.OuterIndex), (b.Item1, importB.OuterIndex));
    }
    public static FProperty? CopyProperty(FProperty? prop, UAsset src, UAsset dst) {
        switch (prop) {
            case FGenericProperty p:
                {
                    return new FGenericProperty() {
                        ArrayDim = p.ArrayDim,
                        ElementSize = p.ElementSize,
                        PropertyFlags = p.PropertyFlags,
                        RepIndex = p.RepIndex,
                        RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                        BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                        RawValue = p.RawValue, // TODO is this ever not null?
                        SerializedType = p.SerializedType.Transfer(dst),
                        Name = p.Name.Transfer(dst),
                        Flags = p.Flags,
                    };
                }
            case FObjectProperty p:
                {
                    return new FObjectProperty() {
                        PropertyClass = CopyImportTo((src, p.PropertyClass), dst),
                        ArrayDim = p.ArrayDim,
                        ElementSize = p.ElementSize,
                        PropertyFlags = p.PropertyFlags,
                        RepIndex = p.RepIndex,
                        RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                        BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                        RawValue = p.RawValue, // TODO is this ever not null?
                        SerializedType = p.SerializedType.Transfer(dst),
                        Name = p.Name.Transfer(dst),
                        Flags = p.Flags,
                    };
                }
            case FInterfaceProperty p:
                {
                    return new FInterfaceProperty() {
                        InterfaceClass = CopyImportTo((src, p.InterfaceClass), dst),
                        ArrayDim = p.ArrayDim,
                        ElementSize = p.ElementSize,
                        PropertyFlags = p.PropertyFlags,
                        RepIndex = p.RepIndex,
                        RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                        BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                        RawValue = p.RawValue, // TODO is this ever not null?
                        SerializedType = p.SerializedType.Transfer(dst),
                        Name = p.Name.Transfer(dst),
                        Flags = p.Flags,
                    };
                }
            case FStructProperty p:
                {
                    return new FStructProperty() {
                        Struct = CopyImportTo((src, p.Struct), dst),
                        ArrayDim = p.ArrayDim,
                        ElementSize = p.ElementSize,
                        PropertyFlags = p.PropertyFlags,
                        RepIndex = p.RepIndex,
                        RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                        BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                        RawValue = p.RawValue, // TODO is this ever not null?
                        SerializedType = p.SerializedType.Transfer(dst),
                        Name = p.Name.Transfer(dst),
                        Flags = p.Flags,
                    };
                }
            case FArrayProperty p:
                {
                    return new FArrayProperty() {
                        Inner = CopyProperty(p.Inner, src, dst),
                        ArrayDim = p.ArrayDim,
                        ElementSize = p.ElementSize,
                        PropertyFlags = p.PropertyFlags,
                        RepIndex = p.RepIndex,
                        RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                        BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                        RawValue = p.RawValue, // TODO is this ever not null?
                        SerializedType = p.SerializedType.Transfer(dst),
                        Name = p.Name.Transfer(dst),
                        Flags = p.Flags,
                    };
                }
            case FBoolProperty p:
                {
                    return new FBoolProperty() {
                        FieldSize = p.FieldSize,
                        ByteOffset = p.ByteOffset,
                        ByteMask = p.ByteMask,
                        FieldMask = p.FieldMask,
                        NativeBool = p.NativeBool,
                        Value = p.Value,
                        ArrayDim = p.ArrayDim,
                        ElementSize = p.ElementSize,
                        PropertyFlags = p.PropertyFlags,
                        RepIndex = p.RepIndex,
                        RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                        BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                        RawValue = p.RawValue, // TODO is this ever not null?
                        SerializedType = p.SerializedType.Transfer(dst),
                        Name = p.Name.Transfer(dst),
                        Flags = p.Flags,
                    };
                }
        }
        throw new NotImplementedException($"FProperty {prop} not implemented");
    }
    public static UProperty CopyUProperty(UProperty prop, UAsset src, UAsset dst) {
        switch (prop) {
            case UObjectProperty p:
                return new UObjectProperty() {
                    // UField
                    Next = null,

                    // UProperty
                    ArrayDim = p.ArrayDim,
                    ElementSize = p.ElementSize,
                    PropertyFlags = p.PropertyFlags,
                    RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                    BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                    RawValue = p.RawValue,

                    // UObjectProperty
                    PropertyClass = CopyImportTo((src, p.PropertyClass), dst),
                };
            case UStructProperty p:
                return new UStructProperty() {
                    // UField
                    Next = null,

                    // UProperty
                    ArrayDim = p.ArrayDim,
                    ElementSize = p.ElementSize,
                    PropertyFlags = p.PropertyFlags,
                    RepNotifyFunc = p.RepNotifyFunc.Transfer(dst),
                    BlueprintReplicationCondition = p.BlueprintReplicationCondition,
                    RawValue = p.RawValue,

                    // UStructProperty
                    Struct = CopyImportTo((src, p.Struct), dst),
                };
        }
        throw new NotImplementedException($"UProperty {prop} not implemented");
    }
    public static List<FPackageIndex> GetDependencies(UProperty prop) {
        var dependencies = new List<FPackageIndex>();
        switch (prop) {
            case UObjectProperty p:
                dependencies.Add(p.PropertyClass);
                break;
            case UStructProperty p:
                dependencies.Add(p.Struct);
                break;
        }
        return dependencies;
    }
    public static FFieldPath? CopyFieldPath(FFieldPath? p, UAsset src, UAsset dst, FunctionExport fnSrc, FunctionExport fnDst) {
        if (p == null) return null;
        if (p.ResolvedOwner.IsNull()) {
            return new FFieldPath() {
                Path = p.Path.Select(p => p.Transfer(dst)).ToArray(),
                ResolvedOwner = FPackageIndex.FromRawIndex(0),
            };
        }
        if (p.ResolvedOwner.IsImport()) {
            return new FFieldPath() {
                Path = p.Path.Select(p => p.Transfer(dst)).ToArray(),
                ResolvedOwner = CopyImportTo((src, p.ResolvedOwner), dst),
            };
        }
        if (p.Path.Length > 1) throw new NotImplementedException($"FFieldPath.Length > 1: {String.Join(", ", p.Path.Select(p => p.ToString()))}");
        if (p.ResolvedOwner.ToExport(src) == fnSrc) {
            var prop = fnSrc.LoadedProperties.First(l => l.Name.ToString() == p.Path[0].ToString());
            if (prop == null) throw new NotImplementedException("Property missing");

            var existing = fnDst.LoadedProperties.FirstOrDefault(l => l.Name.ToString() == p.Path[0].ToString(), null);
            if (existing == null) { // prop doesn't already exist so copy it over
                // TODO check type of prop == existing, only checking by name currently
                fnDst.LoadedProperties = fnDst.LoadedProperties.Append(CopyProperty(prop, src, dst)).ToArray();
            }

            return new FFieldPath() {
                Path = p.Path.Select(p => p.Transfer(dst)).ToArray(),
                ResolvedOwner = FPackageIndex.FromExport(dst.Exports.IndexOf(fnDst)), // TODO avoid IndexOf
            };
        }
        throw new NotImplementedException("FFieldPath points to an export that is not the source function");
    }
    public static FPackageIndex? CopyPropertyExport(FPackageIndex p, UAsset src, UAsset dst, FunctionExport fnSrc, FunctionExport fnDst) {
        if (p == null) return null;
        if (p.ToExport(src) is PropertyExport property) {
            var fnPiSrc = FPackageIndex.FromExport(src.Exports.IndexOf(fnSrc)); // TODO avoid IndexOf
            var fnPiDst = FPackageIndex.FromExport(dst.Exports.IndexOf(fnDst)); // TODO avoid IndexOf

            var existing = dst.Exports.FindIndex(e => e is PropertyExport && e.OuterIndex.Equals(fnPiDst) && e.ObjectName.ToString() == property.ObjectName.ToString());
            if (existing != -1) {
                return FPackageIndex.FromExport(existing);
            }

            var uprop = CopyUProperty(property.Property, src, dst);
            var newProperty = new PropertyExport() {
                //PropertyExport
                Property = uprop,

                //NormalExport
                Data = new List<PropertyData>(), // TODO?

                //Export
                ClassIndex = CopyImportTo((src, property.ClassIndex), dst),
                SuperIndex = CopyImportTo((src, property.SuperIndex), dst),
                TemplateIndex = CopyImportTo((src, property.TemplateIndex), dst),
                ObjectFlags = property.ObjectFlags,
                bForcedExport = property.bForcedExport,
                bNotForClient = property.bNotForClient,
                bNotForServer = property.bNotForServer,
                PackageGuid = property.PackageGuid,
                PackageFlags = property.PackageFlags,
                bNotAlwaysLoadedForEditorGame = property.bNotAlwaysLoadedForEditorGame,
                bIsAsset = property.bIsAsset,
                SerializationBeforeSerializationDependencies = new List<FPackageIndex>(),
                CreateBeforeSerializationDependencies = GetDependencies(uprop),
                SerializationBeforeCreateDependencies = new List<FPackageIndex>(),
                CreateBeforeCreateDependencies = new List<FPackageIndex>() {fnPiDst},
                Asset = dst,

                //FObjectResource
                ObjectName = property.ObjectName.Transfer(dst),
                OuterIndex = fnPiDst, // TODO don't assume this is owned by the function

                //Export
                Extras = property.Extras,
            };

            dst.Exports.Add(newProperty);

            var pi = FPackageIndex.FromExport(dst.Exports.Count - 1);
            fnDst.Children = fnDst.Children.Append(pi).ToArray();
            fnDst.SerializationBeforeSerializationDependencies.Add(pi);

            return pi;
        } else {
            throw new NotImplementedException("expected PropertyExport");
        }
    }
    public static KismetPropertyPointer CopyKismetPropertyPointer(KismetPropertyPointer p, UAsset src, UAsset dst, FunctionExport fnSrc, FunctionExport fnDst) {
        return new KismetPropertyPointer() {
            Old = CopyPropertyExport(p.Old, src, dst, fnSrc, fnDst),
            New = CopyFieldPath(p.New, src, dst, fnSrc, fnDst),
        };
    }
    public static KismetExpression? CopyExpressionTo(KismetExpression? exp, UAsset src, UAsset dst, FunctionExport fnSrc, FunctionExport fnDst) {
        if (exp == null) return null;
        switch (exp) {
            case EX_PushExecutionFlow e:
                {
                    return new EX_PushExecutionFlow() {
                        PushingAddress = e.PushingAddress,
                    };
                }
            case EX_Context e:
                {
                    return new EX_Context() {
                        ObjectExpression = CopyExpressionTo(e.ObjectExpression, src, dst, fnSrc, fnDst),
                        Offset = e.Offset,
                        RValuePointer = CopyKismetPropertyPointer(e.RValuePointer, src, dst, fnSrc, fnDst),
                        ContextExpression = CopyExpressionTo(e.ContextExpression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_ObjectConst e:
                {
                    return new EX_ObjectConst() {
                        Value = CopyImportTo((src, e.Value), dst),
                    };
                }
            case EX_LocalVirtualFunction e:
                {
                    return new EX_LocalVirtualFunction() {
                        VirtualFunctionName = e.VirtualFunctionName.Transfer(dst),
                        Parameters = e.Parameters.Select(p => CopyExpressionTo(p, src, dst, fnSrc, fnDst)).ToArray(),
                    };
                }
            case EX_SkipOffsetConst e:
                {
                    return new EX_SkipOffsetConst() {
                        Value = e.Value,
                    };
                }
            case EX_ByteConst e:
                {
                    return new EX_ByteConst() {
                        Value = e.Value,
                    };
                }
            case EX_IntConst e:
                {
                    return new EX_IntConst() {
                        Value = e.Value,
                    };
                }
            case EX_FloatConst e:
                {
                    return new EX_FloatConst() {
                        Value = e.Value,
                    };
                }
            case EX_DoubleConst e:
                {
                    return new EX_DoubleConst() {
                        Value = e.Value,
                    };
                }
            case EX_VectorConst e:
                {
                    return new EX_VectorConst() {
                        Value = e.Value,
                    };
                }
            case EX_RotationConst e:
                {
                    return new EX_RotationConst() {
                        Value = e.Value,
                    };
                }
            case EX_StringConst e:
                {
                    return new EX_StringConst() {
                        Value = e.Value,
                    };
                }
            case EX_NameConst e:
                {
                    return new EX_NameConst() {
                        Value = e.Value.Transfer(dst),
                    };
                }
            case EX_TextConst e:
                {
                    return new EX_TextConst() {
                        Value = new FScriptText() {
                            TextLiteralType = e.Value.TextLiteralType,
                            LocalizedSource = CopyExpressionTo(e.Value.LocalizedSource, src, dst, fnSrc, fnDst),
                            LocalizedKey = CopyExpressionTo(e.Value.LocalizedKey, src, dst, fnSrc, fnDst),
                            LocalizedNamespace = CopyExpressionTo(e.Value.LocalizedNamespace, src, dst, fnSrc, fnDst),
                            InvariantLiteralString = CopyExpressionTo(e.Value.InvariantLiteralString, src, dst, fnSrc, fnDst),
                            LiteralString = CopyExpressionTo(e.Value.LiteralString, src, dst, fnSrc, fnDst),
                            StringTableAsset = CopyImportTo((src, e.Value.StringTableAsset), dst), // TODO likely not actually an import
                            StringTableId = CopyExpressionTo(e.Value.StringTableId, src, dst, fnSrc, fnDst),
                            StringTableKey = CopyExpressionTo(e.Value.StringTableKey, src, dst, fnSrc, fnDst),
                        },
                    };
                }
            case EX_True e:
                {
                    return new EX_True();
                }
            case EX_False e:
                {
                    return new EX_False();
                }
            case EX_Self e:
                {
                    return new EX_Self();
                }
            case EX_NoObject e:
                {
                    return new EX_NoObject();
                }
            case EX_Nothing e:
                {
                    return new EX_Nothing();
                }
            case EX_Let e:
                {
                    return new EX_Let() {
                        Value = CopyKismetPropertyPointer(e.Value, src, dst, fnSrc, fnDst),
                        Variable = CopyExpressionTo(e.Variable, src, dst, fnSrc, fnDst),
                        Expression = CopyExpressionTo(e.Expression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_LocalVariable e:
                {
                    return new EX_LocalVariable() {
                        Variable = CopyKismetPropertyPointer(e.Variable, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_LocalOutVariable e:
                {
                    return new EX_LocalOutVariable() {
                        Variable = CopyKismetPropertyPointer(e.Variable, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_CallMath e:
                {
                    return new EX_CallMath() {
                        StackNode = CopyImportTo((src, e.StackNode), dst),
                        Parameters = e.Parameters.Select(p => CopyExpressionTo(p, src, dst, fnSrc, fnDst)).ToArray(),
                    };
                }
            case EX_FinalFunction e:
                {
                    return new EX_FinalFunction() {
                        StackNode = CopyImportTo((src, e.StackNode), dst),
                        Parameters = e.Parameters.Select(p => CopyExpressionTo(p, src, dst, fnSrc, fnDst)).ToArray(),
                    };
                }
            case EX_Return e:
                {
                    return new EX_Return() {
                        ReturnExpression = CopyExpressionTo(e.ReturnExpression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_LetObj e:
                {
                    return new EX_LetObj() {
                        VariableExpression = CopyExpressionTo(e.VariableExpression, src, dst, fnSrc, fnDst),
                        AssignmentExpression = CopyExpressionTo(e.AssignmentExpression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_LetBool e:
                {
                    return new EX_LetBool() {
                        VariableExpression = CopyExpressionTo(e.VariableExpression, src, dst, fnSrc, fnDst),
                        AssignmentExpression = CopyExpressionTo(e.AssignmentExpression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_StructConst e:
                {
                    return new EX_StructConst() {
                        Struct = CopyImportTo((src, e.Struct), dst),
                        StructSize = e.StructSize,
                        Value = e.Value.Select(p => CopyExpressionTo(p, src, dst, fnSrc, fnDst)).ToArray(),
                    };
                }
            case EX_StructMemberContext e:
                {
                    return new EX_StructMemberContext() {
                        StructMemberExpression = CopyKismetPropertyPointer(e.StructMemberExpression, src, dst, fnSrc, fnDst),
                        StructExpression = CopyExpressionTo(e.StructExpression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_InterfaceContext e:
                {
                    return new EX_InterfaceContext() {
                        InterfaceValue = CopyExpressionTo(e.InterfaceValue, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_VirtualFunction e:
                {
                    return new EX_VirtualFunction() {
                        VirtualFunctionName = e.VirtualFunctionName.Transfer(dst),
                        Parameters = e.Parameters.Select(p => CopyExpressionTo(p, src, dst, fnSrc, fnDst)).ToArray(),
                    };
                }
            case EX_SetArray e:
                {
                    return new EX_SetArray() {
                        AssigningProperty = CopyExpressionTo(e.AssigningProperty, src, dst, fnSrc, fnDst),
                        ArrayInnerProp = CopyImportTo((src, e.ArrayInnerProp), dst),
                        Elements = e.Elements.Select(p => CopyExpressionTo(p, src, dst, fnSrc, fnDst)).ToArray(),
                    };
                }
            case EX_InstanceVariable e:
                {
                    return new EX_InstanceVariable() {
                        Variable = CopyKismetPropertyPointer(e.Variable, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_DynamicCast e:
                {
                    return new EX_DynamicCast() {
                        ClassPtr = CopyImportTo((src, e.ClassPtr), dst),
                        TargetExpression = CopyExpressionTo(e.TargetExpression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_PrimitiveCast e:
                {
                    return new EX_PrimitiveCast() {
                        ConversionType = e.ConversionType,
                        Target = CopyExpressionTo(e.Target, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_JumpIfNot e:
                {
                    return new EX_JumpIfNot() {
                        CodeOffset = e.CodeOffset, // TODO wtf to do about jumps
                        BooleanExpression = CopyExpressionTo(e.BooleanExpression, src, dst, fnSrc, fnDst),
                    };
                }
            case EX_PopExecutionFlow e:
                {
                    return new EX_PopExecutionFlow();
                }
            case EX_Tracepoint e:
                {
                    return new EX_Tracepoint();
                }
            case EX_WireTracepoint e:
                {
                    return new EX_WireTracepoint();
                }
            default:
                {
                    throw new NotImplementedException(exp.ToString());
                }
        }
    }

    public static IEnumerable<(uint, KismetExpression)> GetOffsets(UAsset asset, KismetExpression[] bytecode) {
        var offsets = new List<(uint, KismetExpression)>();
        uint offset = 0;
        foreach (var inst in bytecode) {
            offsets.Add((offset, inst));
            offset += Kismet.GetSize(asset, inst);
        }
        return offsets;
    }

    public static void SpliceMissionTerminal(UAsset asset) {
        foreach (var export in asset.Exports) {
            if (export is FunctionExport ubergraph) {
                if (!export.ObjectName.ToString().StartsWith("ExecuteUbergraph")) {
                    continue;
                }

                Console.WriteLine("found ubergraph");

                var offsets = GetOffsets(asset, ubergraph.ScriptBytecode).ToDictionary(l => l.Item1, l => l.Item2);

                var list = ubergraph.ScriptBytecode.ToList();

                //list.RemoveAt(list.IndexOf(offsets[5179]));

                list.Insert(list.IndexOf(offsets[5200]), new EX_Jump() {
                    CodeOffset = 5339
                });

                list.Insert(list.IndexOf(offsets[5200]), new EX_StringConst() {
                    Value = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                });

                list.Remove(offsets[5200]);
                list.Remove(offsets[5236]);
                list.Remove(offsets[5257]);
                list.Remove(offsets[5290]);


                ubergraph.ScriptBytecode = list.ToArray();

                var newOffsets = GetOffsets(asset, ubergraph.ScriptBytecode).ToDictionary(l => l.Item2, l => l.Item1);

                foreach (var inst in ubergraph.ScriptBytecode) {
                    if (inst is EX_JumpIfNot jumpIfNot) {
                        jumpIfNot.CodeOffset = newOffsets[offsets[jumpIfNot.CodeOffset]];
                    } else if (inst is EX_Jump jump) {
                        jump.CodeOffset = newOffsets[offsets[jump.CodeOffset]];
                    } else if (inst is EX_PushExecutionFlow push) {
                        push.PushingAddress = newOffsets[offsets[push.PushingAddress]];
                    } else if (inst is EX_CallMath callMath) {
                        foreach (var param in callMath.Parameters) {
                            if (param is EX_StructConst sc && sc.Struct.IsImport() && sc.Struct.ToImport(asset).ObjectName.ToString() == "LatentActionInfo" && sc.Value[2] is EX_NameConst nc && nc.Value == ubergraph.ObjectName && sc.Value[0] is EX_SkipOffsetConst soc) {
                                soc.Value = newOffsets[offsets[soc.Value]];
                            }
                        }
                    }
                }

                // Fix events jumping into Ubergraph
                foreach (var export2 in asset.Exports) {
                    if (export2 is FunctionExport fn) {
                        foreach (var inst in fn.ScriptBytecode) {
                            if (inst is EX_LocalFinalFunction ug && ug.StackNode.IsExport() && ug.StackNode.ToExport(asset) == export) {
                                if (ug.Parameters.Length == 1 && ug.Parameters[0] is EX_IntConst offset) {
                                    offset.Value = (int) newOffsets[offsets[(uint) offset.Value]];
                                } else {
                                    Console.WriteLine("WARN: Expected EX_IntConst parameter");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public static void SpliceAsset(UAsset asset) {
        var src = new UAsset("../../FSD/Saved/Cooked/LinuxNoEditor/FSD/Content/_AssemblyStorm/CustomDifficulty2/Hook_PLS_Base.uasset");
        var srcFn = (FunctionExport) src.Exports.First(e => e is FunctionExport && e.ObjectName.ToString() == "PLS");

        var srcUg = (FunctionExport) src.Exports.First(e => e is FunctionExport && e.ObjectName.ToString() == "ExecuteUbergraph_Hook_PLS_Base");
        var srcOffsets = GetOffsets(asset, srcUg.ScriptBytecode).ToDictionary(l => l.Item1, l => l.Item2);

        var logFn = (EX_Context) srcFn.ScriptBytecode[0];

        foreach (var export in asset.Exports) {
            if (export is FunctionExport ubergraph) {
                if (!export.ObjectName.ToString().StartsWith("ExecuteUbergraph")) {
                    continue;
                }

                Console.WriteLine("found ubergraph");

                var offsets = GetOffsets(asset, ubergraph.ScriptBytecode).ToDictionary(l => l.Item1, l => l.Item2);


                Func<string, KismetExpression> log = (msg) => {
                    var exp = CopyExpressionTo(logFn, src, asset, srcFn, ubergraph);
                    if (exp is EX_Context ctx && ctx.ContextExpression is EX_LocalVirtualFunction lvf) {
                        if (lvf.Parameters[1] is EX_StringConst scMsg) scMsg.Value = msg;
                        ctx.Offset = GetSize(asset, ctx.ContextExpression);
                    }
                    return exp;
                };

                var list = ubergraph.ScriptBytecode.ToList();
                //list.Insert(list.IndexOf(offsets[21994]), log("OnLoaded"));
                //list.Insert(list.IndexOf(offsets[19085]), log("ReceiveBeginPlay"));
                //list.Insert(list.IndexOf(offsets[9848]), log("before begin generation"));
                //list.Insert(list.IndexOf(offsets[8643]), log("wait loop"));
                list.Insert(list.IndexOf(offsets[14398]), log("before BeginGeneration"));
                list.Insert(list.IndexOf(offsets[14408]), log("after BeginGeneration"));
                list.Insert(list.IndexOf(offsets[15059]), log("before client return"));
                list.Insert(list.IndexOf(offsets[15159]), log("before set random stream seed"));

                //list.Remove(offsets[14958]); // remove `IsInitialized = true`


                //client
                var index = list.IndexOf(offsets[15348]);
                var exp = log("wait loop client");
                list.Insert(index++, exp);
                offsets[15348] = exp;

                list.Insert(index++, exp = CopyExpressionTo(srcOffsets[15], src, asset, srcUg, ubergraph));
                offsets[100002] = exp;
                list.Insert(index++, exp = CopyExpressionTo(srcOffsets[45], src, asset, srcUg, ubergraph));
                list.Insert(index++, exp = CopyExpressionTo(srcOffsets[91], src, asset, srcUg, ubergraph));
                ((EX_JumpIfNot) exp).CodeOffset = 100000;
                offsets[100001] = exp;

                list.Add(exp = CopyExpressionTo(srcOffsets[156], src, asset, srcUg, ubergraph));
                offsets[100000] = exp;
                Console.WriteLine(exp);
                ((EX_SkipOffsetConst)((EX_StructConst) ((EX_CallMath) exp).Parameters[2]).Value[0]).Value = 100002;
                ((EX_NameConst)((EX_StructConst) ((EX_CallMath) exp).Parameters[2]).Value[2]).Value = FName.FromString(asset, "ExecuteUbergraph_PLS_Base");
                list.Add(exp = CopyExpressionTo(srcOffsets[210], src, asset, srcUg, ubergraph));


                /*
                //server
                // doesn't work
                //uint serverInst = 15069;

                // works
                //uint serverInst = 14398;
                //uint serverInst = 14833;
                uint serverInst = 15059;

                index = list.IndexOf(offsets[serverInst]);
                exp = log("wait loop server");
                list.Insert(index++, exp);
                offsets[serverInst] = exp;

                list.Insert(index++, exp = CopyExpressionTo(srcOffsets[15], src, asset, srcUg, ubergraph));
                offsets[100012] = exp;
                list.Insert(index++, exp = CopyExpressionTo(srcOffsets[45], src, asset, srcUg, ubergraph));
                list.Insert(index++, exp = CopyExpressionTo(srcOffsets[91], src, asset, srcUg, ubergraph));
                ((EX_JumpIfNot) exp).CodeOffset = 100010;
                offsets[100011] = exp;

                list.Add(exp = CopyExpressionTo(srcOffsets[156], src, asset, srcUg, ubergraph));
                offsets[100010] = exp;
                Console.WriteLine(exp);
                ((EX_SkipOffsetConst)((EX_StructConst) ((EX_CallMath) exp).Parameters[2]).Value[0]).Value = 100012;
                ((EX_NameConst)((EX_StructConst) ((EX_CallMath) exp).Parameters[2]).Value[2]).Value = FName.FromString(asset, "ExecuteUbergraph_PLS_Base");
                list.Add(exp = CopyExpressionTo(srcOffsets[210], src, asset, srcUg, ubergraph));
                */

                //list.Add(CopyExpressionTo(srcOffsets[210], src, asset, srcUg, ubergraph));

                //ubergraph.ScriptBytecode = ubergraph.ScriptBytecode.Where(inst => {
                    //return inst is EX_CallMath cm && cm.StackNode.IsImport() && cm.StackNode.ToImport(asset).ObjectName.ToString() == "PrintString";
                //}).ToArray();
                ubergraph.ScriptBytecode = list.ToArray();

                var newOffsets = GetOffsets(asset, ubergraph.ScriptBytecode).ToDictionary(l => l.Item2, l => l.Item1);

                foreach (var inst in ubergraph.ScriptBytecode) {
                    if (inst is EX_JumpIfNot jumpIfNot) {
                        jumpIfNot.CodeOffset = newOffsets[offsets[jumpIfNot.CodeOffset]];
                    } else if (inst is EX_Jump jump) {
                        jump.CodeOffset = newOffsets[offsets[jump.CodeOffset]];
                    } else if (inst is EX_PushExecutionFlow push) {
                        push.PushingAddress = newOffsets[offsets[push.PushingAddress]];
                    } else if (inst is EX_CallMath callMath) {
                        foreach (var param in callMath.Parameters) {
                            if (param is EX_StructConst sc && sc.Struct.IsImport() && sc.Struct.ToImport(asset).ObjectName.ToString() == "LatentActionInfo" && sc.Value[2] is EX_NameConst nc && nc.Value == ubergraph.ObjectName && sc.Value[0] is EX_SkipOffsetConst soc) {
                                soc.Value = newOffsets[offsets[soc.Value]];
                            }
                        }
                    }
                }

                // Fix events jumping into Ubergraph
                foreach (var export2 in asset.Exports) {
                    if (export2 is FunctionExport fn) {
                        foreach (var inst in fn.ScriptBytecode) {
                            if (inst is EX_LocalFinalFunction ug && ug.StackNode.IsExport() && ug.StackNode.ToExport(asset) == export) {
                                if (ug.Parameters.Length == 1 && ug.Parameters[0] is EX_IntConst offset) {
                                    offset.Value = (int) newOffsets[offsets[(uint) offset.Value]];
                                } else {
                                    Console.WriteLine("WARN: Expected EX_IntConst parameter");
                                }
                            }
                        }
                    }
                }

            }
        }
        /*
        foreach (var export in asset.Exports) {
            if (export is FunctionExport fnSrc) {
                if (export.ObjectName.ToString().StartsWith("ExecuteUbergraph")) {
                    Console.Error.WriteLine("Ignoring ubergraph");
                    continue;
                }
                var found = false;
                foreach (var exportDest in asset.Exports) {
                    if (exportDest is FunctionExport fnDest) {
                        if (fnSrc.ObjectName.ToString().TrimStart('_') != fnDest.ObjectName.ToString().TrimStart('_')) continue;
                        Console.WriteLine($"Found matching function named {export.ObjectName}");

                        var newInst = new List<KismetExpression>();
                        //for (int i = 0; i < fnSrc.ScriptBytecode.Length; i++) {
                        var offset = 0;
                        var keepReturn = false;
                        foreach (var inst in fnSrc.ScriptBytecode) {
                            if (inst is EX_Context c) {
                                if (c.ContextExpression is EX_LocalVirtualFunction i) {
                                    if (i.VirtualFunctionName.Value.ToString() == "RETURN") {
                                        keepReturn = true;
                                        continue; // TODO handle offset addresses in source function because now we're skipping expressions
                                    }
                                }
                            }
                            var isReturn = inst.GetType() == typeof(EX_Return);
                            if (isReturn ? keepReturn : true) {
                                offset += (int) Kismet.GetSize(asset, inst);
                                newInst.Add(Kismet.CopyExpressionTo(inst, source, dest, fnSrc, fnDest));
                            }
                            if (isReturn) break;
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
            }
        }
        */
    }
}
