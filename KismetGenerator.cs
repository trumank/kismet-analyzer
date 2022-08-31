namespace KismetAnalyzer;

using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

public class BlueprintGenerator {
    public static string ToString(Guid guid) {
        return guid.ToString("N").ToUpper();
    }
    public static string ToString(bool value) {
        return value ? "True" : "False";
    }
    public static string ToString(string str) {
        return $"\"{str.Replace("\n", "\\n")}\"";
    }
    public static string ToString(IEnumerable<object>? obj) {
        return obj != null ? "()" : throw new NotImplementedException("Non-null object");
    }
    public static string ToString(object? obj) {
        return obj == null ? "None" : throw new NotImplementedException("Non-null object");
    }
    public class KismetGraph {
        public ICollection<KismetNode> Nodes { get; set; } = new List<KismetNode>();
        Dictionary<Type, int> NodeNameIndex = new Dictionary<Type, int>();

        string GetUniqueName(Type type) {
            if (!NodeNameIndex.ContainsKey(type)) {
                NodeNameIndex[type] = 0;
            }
            return $"{type.Name}_{NodeNameIndex[type]++}";
        }

        public void Write(TextWriter writer) {
            foreach (var node in Nodes) {
                node.Write(writer);
            }
        }

        public void LinkPins(KismetNode nodeA, KismetPin pinA, KismetNode nodeB, KismetPin pinB) {
            pinA.LinkedTo.Add(new KismetPinLink() {
                Node = nodeB,
                Pin = pinB
            });
            pinB.LinkedTo.Add(new KismetPinLink() {
                Node = nodeA,
                Pin = pinA
            });
        }

        public KismetNodeIfThenElse CreateBranch() {
            var node = new KismetNodeIfThenElse() {
                Name = GetUniqueName(typeof(KismetNodeIfThenElse)),
            };
            Nodes.Add(node);
            return node;
        }

        public KismetNodeCallFunction CreateCallFunction(KismetFunctionReference function) {
            var node = new KismetNodeCallFunction() {
                Name = GetUniqueName(typeof(KismetNodeCallFunction)),
                NodePosX = 348,
                NodePosY = -16,
                FunctionReference = function,
                Pins = new List<KismetPin>() {
                    new KismetPin() {
                        PinName = "execute",
                        PinToolTip="\nExec",
                        PinType = KismetPinType.EXEC,
                    },
                    new KismetPin() {
                        PinName = "then",
                        PinToolTip = "\nExec",
                        Direction = KismetPinDirection.EGPD_Output,
                        PinType = KismetPinType.EXEC,
                    },
                    new KismetPin() {
                        PinName = "self",
                        //PinFriendlyName=NSLOCTEXT("K2Node", "Target", "Target")
                        PinToolTip = "Target\nFSDPawn Object Reference",
                        PinType = KismetPinType.EXEC, //PinSubCategoryObject=Class'"/Script/FSD.FSDPawn"'
                    },
                    new KismetPin() {
                        PinName = "Mesh",
                        PinToolTip = "Mesh\nSkeletal Mesh Component Object Reference",
                        PinType = KismetPinType.OBJECT, //PinSubCategoryObject=Class'"/Script/Engine.SkeletalMeshComponent"'
                    }
                }
            };
            Nodes.Add(node);
            return node;
        }
    }
    public abstract class KismetNode {
        public abstract string Class { get; }
        public string Name { get; set; }
        public int NodePosX { get; set; }
        public int NodePosY { get; set; }
        public Guid NodeGuid { get; } = Guid.NewGuid();
        public IEnumerable<KismetPin> Pins { get; set; } = new List<KismetPin>();

        public void Write(TextWriter writer) {
            writer.WriteLine($"Begin Object Class={Class} Name={BlueprintGenerator.ToString(Name)}");
            WriteAttributes(writer);
            writer.WriteLine($"End Object");
        }
        public virtual void WriteAttributes(TextWriter writer) {
            writer.WriteLine($"    NodePosX={NodePosX}");
            writer.WriteLine($"    NodePosX={NodePosY}");
            writer.WriteLine($"    NodeGuid={BlueprintGenerator.ToString(NodeGuid)}");

            foreach (var pin in Pins) {
                pin.Write(writer);
            }
        }
        public KismetPin FindPin(string name, KismetPinDirection direction) {
            return Pins.First(p => p.PinName == name && p.Direction == direction);
        }
    }
    public class KismetNodeCallFunction : KismetNode {
        public override string Class {
            get {
                return "/Script/BlueprintGraph.K2Node_CallFunction";
            }
        }
        public KismetFunctionReference FunctionReference { get; set; }
        public override void WriteAttributes(TextWriter writer) {
            writer.Write($"    FunctionReference=(");
            FunctionReference.Write(writer);
            writer.WriteLine($"),");
            base.WriteAttributes(writer);
        }
    }
    public class KismetNodeIfThenElse : KismetNode {
        public override string Class {
            get {
                return "/Script/BlueprintGraph.K2Node_IfThenElse";
            }
        }
        public KismetNodeIfThenElse() {
            Pins = new List<KismetPin>() {
                new KismetPin() {
                    PinName = "execute",
                    PinType = KismetPinType.EXEC,
                },
                new KismetPin() {
                    PinName = "Condition",
                    PinType = KismetPinType.BOOL,
                },
                new KismetPin() {
                    PinName = "then",
                    PinType = KismetPinType.EXEC,
                    Direction = KismetPinDirection.EGPD_Output,
                },
                new KismetPin() {
                    PinName = "else",
                    PinType = KismetPinType.EXEC,
                    Direction = KismetPinDirection.EGPD_Output,
                },
            };
        }
    }
    public class KismetFunctionReference {
        public string Parent { get; set; }
        public string Name { get; set; }

        public void Write(TextWriter writer) {
            writer.Write($"MemberParent={Parent},");
            writer.Write($"MemberName={BlueprintGenerator.ToString(Name)}");
        }
    }
    public class KismetPin {
        public Guid PinId { get; } = Guid.NewGuid();
        public string PinName { get; set; }
        public string? PinToolTip { get; set; }
        public KismetPinDirection Direction { get; set; } = KismetPinDirection.EGPD_Input;
        public KismetPinType PinType { get; set; }
        public ICollection<KismetPinLink> LinkedTo { get; set; } = new List<KismetPinLink>();
        public Guid PersistentGuid { get; } = Guid.Empty;
        public bool bHidden { get; set; } = false;
        public bool bNotConnectable { get; set; } = false;
        public bool bDefaultValueIsReadOnly { get; set; } = false;
        public bool bDefaultValueIsIgnored { get; set; } = false;
        public bool bAdvancedView { get; set; } = false;
        public bool bOrphanedPin { get; set; } = false;

        public void Write(TextWriter writer) {
            writer.Write($"    CustomProperties Pin (");
            writer.Write($"PinId={BlueprintGenerator.ToString(PinId)},");
            writer.Write($"PinName={BlueprintGenerator.ToString(PinName)},");
            //if (PinToolTip != null) writer.Write($"PinToolTip={BlueprintGenerator.ToString(PinToolTip)},");
            //writer.Write($"Direction={BlueprintGenerator.ToString(Direction.ToString())},");
            //PinType.Write(writer);
            writer.Write($"LinkedTo=({String.Join("", LinkedTo.Select(l => $"{l.Node.Name} {BlueprintGenerator.ToString(l.Pin.PinId)}"))}),");
            //writer.Write($"bHidden={BlueprintGenerator.ToString(bHidden)},");
            //writer.Write($"bNotConnectable={BlueprintGenerator.ToString(bNotConnectable)},");
            //writer.Write($"bDefaultValueIsReadOnly={BlueprintGenerator.ToString(bDefaultValueIsReadOnly)},");
            //writer.Write($"bDefaultValueIsIgnored={BlueprintGenerator.ToString(bDefaultValueIsIgnored)},");
            //writer.Write($"bAdvancedView={BlueprintGenerator.ToString(bAdvancedView)},");
            //writer.Write($"bOrphanedPin={BlueprintGenerator.ToString(bOrphanedPin)},");
            writer.WriteLine(")");
        }
    }
    public class KismetPinLink {
        public KismetNode Node { get; set; }
        public KismetPin Pin { get; set; }
    }
    public enum KismetPinDirection {
        EGPD_Output,
        EGPD_Input,
    }
    public class KismetPinType {
        public static readonly KismetPinType EXEC = new KismetPinType() {
            PinCategory = "exec",
            PinSubCategory = "",
        };
        public static readonly KismetPinType OBJECT = new KismetPinType() {
            PinCategory = "object",
            PinSubCategory = "",
        };
        public static readonly KismetPinType BOOL = new KismetPinType() {
            PinCategory = "bool",
            PinSubCategory = "",
        };
        public string PinCategory { get; set; }
        public string PinSubCategory { get; set; }
        public object? PinSubCategoryObject { get; set; } = null;
        public IEnumerable<object> PinSubCategoryMemberReference { get; set; } = new List<object>();
        public IEnumerable<object> PinValueType { get; set; } = new List<object>();
        public object? ContainerType { get; set; } = null;
        public bool bIsReference { get; set; } = false;
        public bool bIsConst { get; set; } = false;
        public bool bIsWeakPointer { get; set; } = false;
        public bool bIsUObjectWrapper { get; set; } = false;
        public void Write(TextWriter writer) {
            writer.Write($"PinType.PinCategory={BlueprintGenerator.ToString(PinCategory)},");
            writer.Write($"PinType.PinSubCategory={BlueprintGenerator.ToString(PinSubCategory)},");
            writer.Write($"PinType.PinSubCategoryMemberReference={BlueprintGenerator.ToString(PinSubCategoryMemberReference)},");
            writer.Write($"PinType.PinValueType={BlueprintGenerator.ToString(PinValueType)},");
            writer.Write($"PinType.ContainerType={BlueprintGenerator.ToString(ContainerType)},");
            writer.Write($"PinType.bIsReference={BlueprintGenerator.ToString(bIsReference)},");
            writer.Write($"PinType.bIsConst={BlueprintGenerator.ToString(bIsConst)},");
            writer.Write($"PinType.bIsWeakPointer={BlueprintGenerator.ToString(bIsWeakPointer)},");
            writer.Write($"PinType.bIsUObjectWrapper={BlueprintGenerator.ToString(bIsUObjectWrapper)},");
        }
    }

    public class Address {
        public FPackageIndex PackageIndex { get; }
        public uint Offset { get; }
        public Address(FPackageIndex packageIndex, uint offset) {
            PackageIndex = packageIndex;
            Offset = offset;
        }
        public override bool Equals(Object? other) {
            if (other is Address a) {
                return PackageIndex.Index == a.PackageIndex.Index
                    && Offset == a.Offset;
            }
            return false;
        }
        public override int GetHashCode() {
            return (PackageIndex.Index, Offset).GetHashCode();
        }
    }

    UEContext Context;

    UAsset Asset;
    TextWriter Output;
    Dictionary<Address, KismetExpression> ExpressionMap;

    public BlueprintGenerator(UEContext context, UAsset asset) {
        Context = context;
        Asset = asset;
        Output = Console.Out;
        ExpressionMap = new Dictionary<Address, KismetExpression>();
    }

    public void Generate() {
        /*
        var classExport = Asset.GetClassExport();
        for (var exportIndex = 0; exportIndex < Asset.Exports.Count; exportIndex++) {
            var export = Asset.Exports[exportIndex];
            if (export is FunctionExport e) {
                Output.WriteLine("FunctionExport " + e.ObjectName);

                string functionName = e.ObjectName.ToString();

                //var functionLines = new Lines("Function " + functionName);
                foreach (var prop in e.LoadedProperties) {
                    //functionLines.Add(new Lines(prop.SerializedType.ToString() + " " + prop.Name.ToString()));
                }


                int index = 0;
                foreach (var exp in e.ScriptBytecode) {
                    var pi = new Address(FPackageIndex.FromExport(exportIndex), index);
                    ExpressionMap.Add(new Address(FPackageIndex.FromExport(exportIndex), index), exp);
                    Output.WriteLine(ExpressionMap[new Address(FPackageIndex.FromExport(exportIndex), index)]);
                    index += Kismet.GetSize(exp);
                }
            }
        }
        foreach (var exp in ExpressionMap) {
            Output.WriteLine($"{exp.Key.PackageIndex}:{exp.Key.Offset}: {exp.Value}");
        }
        foreach (var export in Asset.Exports.OfType<FunctionExport>()) {
            foreach (var exp in export.ScriptBytecode) {
                switch (exp) {
                    case EX_LocalFinalFunction e:
                        {
                            if (e.Parameters[0] is EX_IntConst offset) {
                                Output.WriteLine($"{e.StackNode}:{offset.Value}");
                                Output.WriteLine(ExpressionMap[new Address(e.StackNode, offset.Value)]);
                            }
                            break;
                        }
                }
            }
        }
        */
        var graph = new KismetGraph();
        var branch = graph.CreateBranch();
        var function = graph.CreateCallFunction(
            new KismetFunctionReference() {
                Parent = "Class'\"/Script/FSD.FSDPawn\"'",
                Name = "MakeRagdollMesh",
            }
        );
        graph.LinkPins(
            branch,
            branch.FindPin("then", KismetPinDirection.EGPD_Output),
            function,
            function.FindPin("execute", KismetPinDirection.EGPD_Input)
        );
        //var writer = new StringWriter();
        graph.Write(Output);
        //writer.ToString();

    }
}
