namespace KismetAnalyzer;

using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using UAssetAPI.FieldTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

public class Kismet {
    public static uint GetSize(KismetExpression exp) {
        uint index = 1;
        switch (exp) {
            case EX_PushExecutionFlow e:
                {
                    index += 4;
                    break;
                }
            case EX_ComputedJump e:
                {
                    index += GetSize(e.CodeOffsetExpression);
                    break;
                }
            case EX_Jump e:
                {
                    index += 4;
                    break;
                }
            case EX_JumpIfNot e:
                {
                    index += 4;
                    index += GetSize(e.BooleanExpression);
                    break;
                }
            case EX_LocalVariable e:
                {
                    index += 8;
                    break;
                }
            case EX_ObjToInterfaceCast e:
                {
                    index += 8;
                    index += GetSize(e.Target);
                    break;
                }
            case EX_Let e:
                {
                    index += 8;
                    index += GetSize(e.Variable);
                    index += GetSize(e.Expression);
                    break;
                }
            case EX_LetObj e:
                {
                    index += GetSize(e.VariableExpression);
                    index += GetSize(e.AssignmentExpression);
                    break;
                }
            case EX_LetBool e:
                {
                    index += GetSize(e.VariableExpression);
                    index += GetSize(e.AssignmentExpression);
                    break;
                }
            case EX_LetWeakObjPtr e:
                {
                    index += GetSize(e.VariableExpression);
                    index += GetSize(e.AssignmentExpression);
                    break;
                }
            case EX_LetValueOnPersistentFrame e:
                {
                    index += 8;
                    index += GetSize(e.AssignmentExpression);
                    break;
                }
            case EX_StructMemberContext e:
                {
                    index += 8;
                    index += GetSize(e.StructExpression);
                    break;
                }
            case EX_MetaCast e:
                {
                    index += 8;
                    index += GetSize(e.TargetExpression);
                    break;
                }
            case EX_DynamicCast e:
                {
                    index += 8;
                    index += GetSize(e.TargetExpression);
                    break;
                }
            case EX_PrimitiveCast e:
                {
                    index++;
                    switch (e.ConversionType) {
                        case ECastToken.ObjectToInterface:
                        {
                            index += 8;
                            // TODO InterfaceClass
                            break;
                        }
                    }
                    index += GetSize(e.Target);
                    break;
                }
            case EX_PopExecutionFlow e:
                {
                    break;
                }
            case EX_PopExecutionFlowIfNot e:
                {
                    index += GetSize(e.BooleanExpression);
                    break;
                }
            case EX_CallMath e:
                {
                    index += 8;
                    foreach (var arg in e.Parameters) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_SwitchValue e:
                {
                    index += 6;
                    index += GetSize(e.IndexTerm);
                    foreach (var c in e.Cases) {
                        index += GetSize(c.CaseIndexValueTerm);
                        index += 4;
                        index += GetSize(c.CaseTerm);
                    }
                    index += GetSize(e.DefaultTerm);
                    break;
                }
            case EX_Self e:
                {
                    break;
                }
            case EX_TextConst e:
                {
                    index++;
                    switch (e.Value.TextLiteralType) {
                        case EBlueprintTextLiteralType.Empty:
                            {
                                break;
                            }
                        case EBlueprintTextLiteralType.LocalizedText:
                            {
                                index += GetStringSize(e.Value.LocalizedSource);
                                index += GetStringSize(e.Value.LocalizedKey);
                                index += GetStringSize(e.Value.LocalizedNamespace);
                                break;
                            }
                        case EBlueprintTextLiteralType.InvariantText:
                            {
                                index += GetStringSize(e.Value.InvariantLiteralString);
                                break;
                            }
                        case EBlueprintTextLiteralType.LiteralString:
                            {
                                index += GetStringSize(e.Value.LiteralString);
                                break;
                            }
                        case EBlueprintTextLiteralType.StringTableEntry:
                            {
                                index += 8;
                                index += GetStringSize(e.Value.StringTableId);
                                index += GetStringSize(e.Value.StringTableKey);
                                break;
                            }
                    }
                    break;
                }
            case EX_ObjectConst e:
                {
                    index += 8;
                    break;
                }
            case EX_VectorConst e:
                {
                    index += 12;
                    break;
                }
            case EX_RotationConst e:
                {
                    index += 12;
                    break;
                }
            case EX_TransformConst e:
                {
                    index += 40;
                    break;
                }
            case EX_Context e:
                {
                    index += GetSize(e.ObjectExpression);
                    index += 4;
                    index += 8;
                    index += GetSize(e.ContextExpression);
                    break;
                }
            case EX_CallMulticastDelegate e:
                {
                    index += 8;
                    index += GetSize(e.Delegate);
                    foreach (var arg in e.Parameters) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_LocalFinalFunction e:
                {
                    index += 8;
                    foreach (var arg in e.Parameters) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_FinalFunction e:
                {
                    index += 8;
                    foreach (var arg in e.Parameters) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_LocalVirtualFunction e:
                {
                    index += 12;
                    foreach (var arg in e.Parameters) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_VirtualFunction e:
                {
                    index += 12;
                    foreach (var arg in e.Parameters) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_InstanceVariable e:
                {
                    index += 8;
                    break;
                }
            case EX_AddMulticastDelegate e:
                {
                    index += GetSize(e.Delegate);
                    index += GetSize(e.DelegateToAdd);
                    break;
                }
            case EX_RemoveMulticastDelegate e:
                {
                    index += GetSize(e.Delegate);
                    index += GetSize(e.DelegateToAdd);
                    break;
                }
            case EX_ClearMulticastDelegate e:
                {
                    index += GetSize(e.DelegateToClear);
                    break;
                }
            case EX_BindDelegate e:
                {
                    index += 12;
                    index += GetSize(e.Delegate);
                    index += GetSize(e.ObjectTerm);
                    break;
                }
            case EX_StructConst e:
                {
                    index += 8;
                    index += 4;
                    foreach (var arg in e.Value) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_SetArray e:
                {
                    index += GetSize(e.AssigningProperty);
                    foreach (var arg in e.Elements) {
                        index += GetSize(arg);
                    }
                    index++;
                    break;
                }
            case EX_SoftObjectConst e:
                {
                    index += GetSize(e.Value);
                    break;
                }
            case EX_ByteConst e:
                {
                    index++;
                    break;
                }
            case EX_IntConst e:
                {
                    index += 4;
                    break;
                }
            case EX_FloatConst e:
                {
                    index += 4;
                    break;
                }
            case EX_NameConst e:
                {
                    index += 12;
                    break;
                }
            case EX_StringConst e:
                {
                    index += GetStringSize(e);
                    break;
                }
            case EX_SkipOffsetConst e:
                {
                    index += 4;
                    break;
                }
            case EX_ArrayConst e:
                {
                    index += 13;
                    foreach (var arg in e.Elements) {
                        index += GetSize(arg);
                    }
                    break;
                }
            case EX_Return e:
                {
                    index += GetSize(e.ReturnExpression);
                    break;
                }
            case EX_LocalOutVariable e:
                {
                    index += 8;
                    break;
                }
            case EX_InterfaceContext e:
                {
                    index += GetSize(e.InterfaceValue);
                    break;
                }
            case EX_ArrayGetByRef e:
                {
                    index += GetSize(e.ArrayVariable);
                    index += GetSize(e.ArrayIndex);
                    break;
                }
            case EX_True:
            case EX_False:
            case EX_Nothing:
            case EX_NoObject:
            case EX_EndOfScript:
                {
                    break;
                }
            default:
                {
                    throw new NotImplementedException(exp.ToString());
                }
        }
        return index;
    }

    static uint GetStringSize(KismetExpression exp) {
        switch (exp) {
            case EX_StringConst e:
                {
                    return (uint) (e.Value.Length + 1);
                }
            case EX_UnicodeStringConst e:
                {
                    return (uint) (2 * (e.Value.Length + 1));
                }
            default:
                throw new Exception("ReadString called on non-string");
        }
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
    public static KismetPropertyPointer CopyKismetPropertyPointer(KismetPropertyPointer p, UAsset src, UAsset dst, FunctionExport fnSrc, FunctionExport fnDst) {
        return new KismetPropertyPointer() {
            Old = CopyImportTo((src, p.Old), dst),
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
            case EX_VectorConst e:
                {
                    return new EX_VectorConst() {
                        Value = e.Value,
                    };
                }
            case EX_RotationConst e:
                {
                    return new EX_RotationConst() {
                        Pitch = e.Pitch,
                        Yaw = e.Yaw,
                        Roll = e.Roll,
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
                    };
                }
            case EX_StructMemberContext e:
                {
                    return new EX_StructMemberContext() {
                        StructMemberExpression = CopyKismetPropertyPointer(e.StructMemberExpression, src, dst, fnSrc, fnDst),
                        StructExpression = CopyExpressionTo(e.StructExpression, src, dst, fnSrc, fnDst),
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
            default:
                {
                    throw new NotImplementedException(exp.ToString());
                }
        }
    }
}
