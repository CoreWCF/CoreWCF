// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreWCF.DataContractSerialization.Generator;

public sealed partial class DataContractSerializerGenerator
{
    /// <summary>
    /// Turns a serializer context declaration into a <see cref="ContextSpec"/>.
    /// </summary>
    /// <remarks>
    /// The only place that touches symbols. Everything it produces is plain data, so the emitter -
    /// and Roslyn's incremental cache - never see a <c>Compilation</c>.
    /// </remarks>
    internal static class Parser
    {
        internal const string ContextBaseTypeName = "CoreWCF.DataContractSerialization.DataContractSerializerContext";
        internal const string SerializableAttributeName = "CoreWCF.DataContractSerialization.DataContractSerializableAttribute";
        internal const string DataContractAttributeName = "System.Runtime.Serialization.DataContractAttribute";
        internal const string DataMemberAttributeName = "System.Runtime.Serialization.DataMemberAttribute";
        internal const string KnownTypeAttributeName = "System.Runtime.Serialization.KnownTypeAttribute";
        internal const string ClrSerializableAttributeName = "System.SerializableAttribute";
        internal const string NonSerializedAttributeName = "System.NonSerializedAttribute";

        /// <summary>The namespace a contract gets when it does not name one itself.</summary>
        private const string DefaultNamespacePrefix = "http://schemas.datacontract.org/2004/07/";

        /// <summary>The contract namespace arrays and lists of built-in types are written in.</summary>
        internal const string CollectionNamespace = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";

        /// <summary>
        /// The contract namespace of the framework types the serializer models as contracts.
        /// </summary>
        /// <remarks>
        /// <c>DateTimeOffset</c> is the one this generator writes: DataContractSerializer swaps in
        /// DateTimeOffsetAdapter, a struct contract named DateTimeOffset in this namespace with a
        /// DateTime and an OffsetMinutes member.
        /// </remarks>
        internal const string SystemContractNamespace = "http://schemas.datacontract.org/2004/07/System";

        internal static ContextSpec Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.TargetSymbol is not INamedTypeSymbol contextType)
            {
                return ContextSpec.Failed(EquatableArray<DiagnosticInfo>.Empty);
            }

            List<DiagnosticInfo> diagnostics = new();

            if (!IsPartial(context.TargetNode))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ContextMustBePartial, contextType, contextType.Name));
            }

            if (!DerivesFromContextBase(contextType))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ContextMustDeriveFromBase, contextType, contextType.Name));
            }

            if (diagnostics.Count > 0)
            {
                return ContextSpec.Failed(new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
            }

            // Contracts reachable through members are pulled in automatically, so a user lists the
            // types they serialize rather than the closure of everything those types touch. Only
            // the explicitly declared ones become GetSerializer entries; the rest exist so their
            // content can be written inline by whatever refers to them.
            Dictionary<string, ContractSpec> contracts = new(StringComparer.Ordinal);
            Dictionary<string, EnumSpec> enumSpecs = new(StringComparer.Ordinal);
            Queue<(INamedTypeSymbol Type, bool IsRoot)> pending = new();
            HashSet<string> queued = new(StringComparer.Ordinal);

            foreach (AttributeData attribute in context.Attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attribute.ConstructorArguments.Length != 1
                    || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol contractType)
                {
                    continue;
                }

                if (!IsContractType(contractType))
                {
                    diagnostics.Add(DiagnosticInfo.Create(
                        DiagnosticDescriptors.TypeIsNotADataContract, contractType, contractType.ToDisplayString()));
                    continue;
                }

                Enqueue(contractType, isRoot: true);
            }

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (INamedTypeSymbol type, bool isRoot) = pending.Dequeue();
                ContractSpec spec = ParseContract(type, contextType.ContainingAssembly, isRoot, out List<INamedTypeSymbol> referenced, out List<INamedTypeSymbol> referencedEnums, cancellationToken);
                contracts[spec.FullyQualifiedName] = spec;

                // Symbols are followed here and never stored: putting one in the spec would root a
                // Compilation and defeat the incremental caching this design exists to preserve.
                foreach (INamedTypeSymbol reference in referenced)
                {
                    Enqueue(reference, isRoot: false);
                }

                foreach (INamedTypeSymbol enumType in referencedEnums)
                {
                    string enumKey = enumType.ToDisplayString(FullyQualifiedFormat);
                    if (!enumSpecs.ContainsKey(enumKey))
                    {
                        enumSpecs[enumKey] = ParseEnum(enumType, enumKey);
                    }
                }
            }

            PropagateKnownTypes(contracts);
            PropagateUnsupported(contracts);

            string? containingNamespace = contextType.ContainingNamespace.IsGlobalNamespace
                ? null
                : contextType.ContainingNamespace.ToDisplayString();

            // Sorted so the emitted file is byte-stable across builds: dictionary order is an
            // implementation detail, and generated source that shuffles is generated source that
            // shows up as a spurious diff.
            ContractSpec[] ordered = contracts.Values
                .OrderBy(c => c.FullyQualifiedName, StringComparer.Ordinal)
                .ToArray();

            EnumSpec[] orderedEnums = enumSpecs.Values
                .OrderBy(e => e.FullyQualifiedName, StringComparer.Ordinal)
                .ToArray();

            return new ContextSpec(
                containingNamespace,
                contextType.Name,
                HintNameFor(contextType),
                new EquatableArray<ContractSpec>(ordered),
                new EquatableArray<EnumSpec>(orderedEnums),
                new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));

            void Enqueue(INamedTypeSymbol type, bool isRoot)
            {
                string key = type.ToDisplayString(FullyQualifiedFormat);
                if (isRoot)
                {
                    // A type may be reached as a member before it is declared; declaring it wins,
                    // because that is what makes it available from GetSerializer.
                    if (contracts.TryGetValue(key, out ContractSpec existing) && !existing.IsRoot)
                    {
                        contracts[key] = existing with { IsRoot = true };
                        return;
                    }
                }

                if (queued.Add(key))
                {
                    pending.Enqueue((type, isRoot));
                }
                else if (isRoot && contracts.TryGetValue(key, out ContractSpec found) && !found.IsRoot)
                {
                    contracts[key] = found with { IsRoot = true };
                }
            }
        }

        /// <summary>
        /// Rolls each contract's known types up through the graph, so a root's set is everything
        /// reachable from it rather than only what it declares itself.
        /// </summary>
        /// <remarks>
        /// This set answers one question, asked at run time: are the known types CoreWCF supplies
        /// from the operation description ones this serializer already resolves? Since the operation
        /// names types for the whole graph, the answer has to be drawn from the whole graph too - a
        /// root that declares nothing but whose member type declares a derived contract does resolve
        /// that contract, and should say so.
        /// </remarks>
        private static void PropagateKnownTypes(Dictionary<string, ContractSpec> contracts)
        {
            Dictionary<string, HashSet<string>> sets = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ContractSpec> entry in contracts)
            {
                sets[entry.Key] = new HashSet<string>(entry.Value.KnownTypes, StringComparer.Ordinal);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;

                foreach (KeyValuePair<string, ContractSpec> entry in contracts)
                {
                    HashSet<string> target = sets[entry.Key];

                    foreach (string dependency in Dependencies(entry.Value))
                    {
                        if (sets.TryGetValue(dependency, out HashSet<string> source))
                        {
                            foreach (string knownType in source)
                            {
                                changed |= target.Add(knownType);
                            }
                        }
                    }
                }
            }

            foreach (string key in contracts.Keys.ToArray())
            {
                contracts[key] = contracts[key] with
                {
                    KnownTypes = new EquatableArray<string>(sets[key]
                        .OrderBy(n => n, StringComparer.Ordinal)
                        .ToArray())
                };
            }

            static IEnumerable<string> Dependencies(ContractSpec spec)
            {
                if (spec.BaseContractFullyQualifiedName is string baseName)
                {
                    yield return baseName;
                }

                foreach (MemberSpec member in spec.Members)
                {
                    if (member.NestedContractFullyQualifiedName is string nested)
                    {
                        yield return nested;
                    }

                    foreach (string candidate in member.Candidates)
                    {
                        yield return candidate;
                    }
                }
            }
        }

        /// <summary>
        /// A contract whose nested contract or base contract cannot be written cannot be written
        /// either. Repeats until nothing changes, since the dependency can be several deep.
        /// </summary>
        private static void PropagateUnsupported(Dictionary<string, ContractSpec> contracts)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;

                foreach (string key in contracts.Keys.ToArray())
                {
                    ContractSpec spec = contracts[key];
                    if (!spec.IsSupported)
                    {
                        continue;
                    }

                    string? blocked = FirstUnsupportedDependency(spec, contracts);
                    if (blocked is not null)
                    {
                        contracts[key] = spec.WithUnsupportedReason(blocked);
                        changed = true;
                    }
                }
            }
        }

        private static string? FirstUnsupportedDependency(ContractSpec spec, Dictionary<string, ContractSpec> contracts)
        {
            if (spec.BaseContractFullyQualifiedName is string baseName
                && contracts.TryGetValue(baseName, out ContractSpec baseSpec)
                && !baseSpec.IsSupported)
            {
                return "base contract " + baseName + " is not supported (" + string.Join("; ", baseSpec.UnsupportedReasons) + ")";
            }

            foreach (MemberSpec member in spec.Members)
            {
                if (member.NestedContractFullyQualifiedName is string nested
                    && contracts.TryGetValue(nested, out ContractSpec nestedSpec)
                    && !nestedSpec.IsSupported)
                {
                    return "member '" + member.MemberName + "' has unsupported contract type " + nested +
                           " (" + string.Join("; ", nestedSpec.UnsupportedReasons) + ")";
                }

                // A polymorphic member is only writable if every type it may hold is. Missing one
                // would leave a runtime type with no branch, which is a throw at run time rather
                // than the fallback the caller is entitled to.
                foreach (string candidate in member.Candidates)
                {
                    if (contracts.TryGetValue(candidate, out ContractSpec candidateSpec)
                        && !candidateSpec.IsSupported)
                    {
                        return "member '" + member.MemberName + "' may hold unsupported contract type " + candidate +
                               " (" + string.Join("; ", candidateSpec.UnsupportedReasons) + ")";
                    }
                }
            }

            return null;
        }

        private static ContractSpec ParseContract(INamedTypeSymbol contractType, IAssemblySymbol contextAssembly, bool isRoot, out List<INamedTypeSymbol> referenced, out List<INamedTypeSymbol> enums, CancellationToken cancellationToken)
        {
            referenced = new List<INamedTypeSymbol>();
            enums = new List<INamedTypeSymbol>();
            // Null for a [Serializable] type, which names itself and is written from its fields.
            AttributeData? dataContract = GetAttribute(contractType, DataContractAttributeName);
            bool isSerializable = dataContract is null;

            string contractName = (dataContract is null ? null : GetNamedArgument(dataContract, "Name")) ?? contractType.Name;
            string contractNamespace = (dataContract is null ? null : GetNamedArgument(dataContract, "Namespace")) ?? DefaultNamespaceFor(contractType);

            List<string> unsupportedReasons = new();

            // IsReference makes the serializer emit z:Id and z:Ref to preserve object identity.
            // It is inherited, so a derived contract that says nothing still gets it from its base -
            // reading only this type's attribute would miss that and emit plausible, wrong output.
            bool isReference = InheritsIsReference(contractType);

            // A value type has no identity to preserve, and DataContract throws rather than quietly
            // ignoring the request - so decline and let the reflection path throw as it does today.
            if (isReference && contractType.IsValueType)
            {
                unsupportedReasons.Add("IsReference is not valid on a value type");
            }

            List<INamedTypeSymbol> knownTypes = new();
            if (!TryCollectKnownTypes(contractType, knownTypes, out string? knownTypeReason))
            {
                unsupportedReasons.Add(knownTypeReason!);
            }

            // A contract from another assembly is only safe if every one of its data members is
            // visible from here. Non-public members of a metadata type may not be surfaced at all,
            // in which case they would be silently dropped - producing wrong XML rather than
            // falling back. Since their absence is indistinguishable from their not existing, the
            // only sound answer is to decline the whole contract.
            if (!SymbolEqualityComparer.Default.Equals(contractType.ContainingAssembly, contextAssembly))
            {
                unsupportedReasons.Add("contract is declared in another assembly (" +
                                       contractType.ContainingAssembly.Name +
                                       "), where non-public data members may not be visible to the generator");
            }

            INamedTypeSymbol? baseContract = contractType.BaseType is INamedTypeSymbol candidate
                && IsContractType(candidate)
                    ? candidate
                    : null;

            if (baseContract is not null)
            {
                referenced.Add(baseContract);
            }

            // A contract may restate IsReference but not contradict what its base contract says.
            // DataContract rejects that with InvalidDataContractException, so declining leaves the
            // reflection path to throw exactly as it does today rather than inventing an answer.
            // Only a data-contract base participates: a contract at the root of its hierarchy is
            // free to turn IsReference on, which is how every reference-preserving graph starts.
            // See ClassDataContractCriticalHelper.EnsureIsReferenceImported in dotnet/runtime.
            if (baseContract is not null
                && dataContract is not null
                && GetNamedArgumentBoolean(dataContract, "IsReference") is bool declaredIsReference
                && declaredIsReference != InheritsIsReference(baseContract))
            {
                unsupportedReasons.Add("IsReference = " + (declaredIsReference ? "true" : "false") +
                                       " contradicts base contract " + baseContract.Name +
                                       ", which DataContractSerializer rejects");
            }

            // A base class that is not itself a data contract contributes no members but is also
            // not something this generator can reason about, so decline.
            if (baseContract is null
                && contractType.BaseType is { SpecialType: not SpecialType.System_Object } other
                && other.SpecialType != SpecialType.System_ValueType)
            {
                unsupportedReasons.Add("base type " + other.Name + " is not a data contract");
            }

            List<MemberSpec> members = new();
            foreach (ISymbol member in contractType.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (member.IsStatic)
                {
                    continue;
                }

                // A [Serializable] type has no [DataMember]s to read. Every instance field takes
                // part unless it is [NonSerialized], properties never do, and each field keeps its
                // own name. See the else branch of hasDataContract in
                // ClassDataContract.ImportDataMembers.
                AttributeData? dataMember = null;
                if (isSerializable)
                {
                    if (member is not IFieldSymbol
                        || GetAttribute(member, NonSerializedAttributeName) is not null
                        || member.Name.IndexOf('<') >= 0)
                    {
                        continue;
                    }
                }
                else
                {
                    dataMember = GetAttribute(member, DataMemberAttributeName);
                    if (dataMember is null)
                    {
                        continue;
                    }
                }

                // A property that overrides a base [DataMember] does not add a second element: the
                // base contract already contributes it, and the override only changes which getter
                // runs. Emitting it again would duplicate the member on the wire.
                if (OverridesADataMember(member))
                {
                    continue;
                }

                ITypeSymbol? memberType = member switch
                {
                    IPropertySymbol property => property.Type,
                    IFieldSymbol field => field.Type,
                    _ => null
                };

                if (memberType is null)
                {
                    continue;
                }

                // Generated code lives in the context's assembly, so it can only reach members the
                // context can see. Anything less visible keeps its contract on the reflection path.
                if (member.DeclaredAccessibility != Accessibility.Public)
                {
                    unsupportedReasons.Add("member '" + member.Name + "' is not public");
                }

                MemberKind kind = ClassifyMember(memberType, out bool isNullableValueType, out INamedTypeSymbol? nestedContract);
                if (kind == MemberKind.Unsupported)
                {
                    unsupportedReasons.Add("member '" + member.Name + "' has unsupported type '" +
                                          memberType.ToDisplayString() + "'");
                }

                List<string> candidates = new();
                List<string> enumCandidates = new();
                BoxedDeclaration boxed = BoxedDeclaration.Object;

                if (nestedContract is not null && kind == MemberKind.Contract)
                {
                    referenced.Add(nestedContract);

                    // A member declared as a contract that [KnownType] gives derived alternatives to
                    // must decide its writer at run time and announce the choice with i:type.
                    // Without this the declared type's members would be written for a derived
                    // instance - output that is well-formed, plausible and wrong.
                    //
                    // The attribute may sit on either end: on the contract holding the member, or on
                    // the member's own declared type. The serializer has both in scope while writing
                    // the member - it pushes each contract's known types as it descends - so both
                    // are collected here. Reading only one end silently loses the other's types.
                    List<INamedTypeSymbol> inScope = new(knownTypes);
                    if (!TryCollectKnownTypes(nestedContract, inScope, out string? memberKnownTypeReason))
                    {
                        unsupportedReasons.Add(memberKnownTypeReason!);
                    }

                    foreach (INamedTypeSymbol derived in PolymorphicCandidates(nestedContract, inScope))
                    {
                        referenced.Add(derived);
                        candidates.Add(derived.ToDisplayString(FullyQualifiedFormat));
                    }

                    // An abstract declared type with nothing to resolve to can only ever hold an
                    // instance of a type no [KnownType] names. Writing the declared contract's
                    // members for it would be wrong, so decline instead.
                    if (candidates.Count == 0 && nestedContract.IsAbstract)
                    {
                        unsupportedReasons.Add("member '" + member.Name + "' is declared as abstract contract " +
                                              nestedContract.Name + " with no [KnownType] to resolve it");
                    }
                }
                else if (nestedContract is not null && kind == MemberKind.Enum)
                {
                    enums.Add(nestedContract);
                }
                else if (kind == MemberKind.Object)
                {
                    boxed = UnwrapNullable(memberType).ToDisplayString() switch
                    {
                        "System.ValueType" => BoxedDeclaration.ValueType,
                        "System.Enum" => BoxedDeclaration.Enum,
                        "System.Array" => BoxedDeclaration.Array,
                        _ => BoxedDeclaration.Object
                    };

                    // object constrains nothing, so every known type in scope is a candidate. The
                    // boxed primitives are always allowed and are added by the emitter; a known type
                    // that is not a writable contract - an enum, a [Serializable] type - would leave
                    // the switch with no branch for it, so the contract is declined instead.
                    foreach (INamedTypeSymbol knownType in knownTypes)
                    {
                        if (knownType.TypeKind == TypeKind.Enum && boxed != BoxedDeclaration.Array)
                        {
                            enums.Add(knownType);
                            enumCandidates.Add(knownType.ToDisplayString(FullyQualifiedFormat));
                            continue;
                        }

                        // A known type the declared type cannot hold is not a candidate: an Enum
                        // member admits only enums, and a ValueType member only value types. This is
                        // not a nicety - the emitted cast would not compile.
                        if (boxed is BoxedDeclaration.Enum or BoxedDeclaration.Array
                            || (boxed == BoxedDeclaration.ValueType && !knownType.IsValueType))
                        {
                            continue;
                        }

                        if (!IsContractType(knownType))
                        {
                            unsupportedReasons.Add("member '" + member.Name + "' is declared as object and known type " +
                                                  knownType.Name + " is not a data contract this generator can write");
                            continue;
                        }

                        referenced.Add(knownType);
                        candidates.Add(knownType.ToDisplayString(FullyQualifiedFormat));
                    }
                }

                MemberKind elementKind = MemberKind.Unsupported;
                string? itemName = null;
                string? itemNamespace = null;
                bool elementCanBeNull = false;
                INamedTypeSymbol? elementEnum = null;
                string? nestedItemName = null;
                MemberKind nestedElementKind = MemberKind.Unsupported;
                bool nestedElementCanBeNull = false;
                string? elementClrType = null;
                bool collectionIsArray = false;
                bool collectionIsArrayList = false;
                string? nestedElementClrType = null;
                bool nestedCollectionIsArray = false;
                MemberKind keyKind = MemberKind.Unsupported;
                MemberKind valueKind = MemberKind.Unsupported;
                bool keyCanBeNull = false;
                bool valueCanBeNull = false;
                string? keyClrType = null;
                string? valueClrType = null;

                if (kind == MemberKind.Collection)
                {
                    elementKind = ClassifyCollectionElement(
                        memberType, contractNamespace, out itemName, out itemNamespace, out elementEnum, out elementCanBeNull,
                        out nestedItemName, out nestedElementKind, out nestedElementCanBeNull);

                    if (elementKind == MemberKind.Unsupported)
                    {
                        unsupportedReasons.Add("member '" + member.Name + "' has unsupported collection element type in '" +
                                              memberType.ToDisplayString() + "'");
                    }
                    else if (elementEnum is not null)
                    {
                        enums.Add(elementEnum);
                    }

                    if (CollectionElementTypeOf(UnwrapNullable(memberType)) is ITypeSymbol elementType)
                    {
                        elementClrType = elementType.ToDisplayString(FullyQualifiedFormat);
                        collectionIsArray = UnwrapNullable(memberType) is IArrayTypeSymbol;

                        if (CollectionElementTypeOf(elementType) is ITypeSymbol innerElementType)
                        {
                            nestedElementClrType = innerElementType.ToDisplayString(FullyQualifiedFormat);
                            nestedCollectionIsArray = elementType is IArrayTypeSymbol;
                        }
                    }
                    else if (IsArrayList(UnwrapNullable(memberType)))
                    {
                        // An ArrayList holds anything, so its items are objects and the container is
                        // itself rather than a List<T>.
                        elementClrType = "object";
                        collectionIsArrayList = true;
                    }
                }
                else if (kind == MemberKind.Dictionary)
                {
                    IsDictionary(UnwrapNullable(memberType), out ITypeSymbol? keyType, out ITypeSymbol? valueType);

                    if (TryClassifyDictionary(keyType!, valueType!, out itemName, out keyKind, out valueKind, out keyCanBeNull, out valueCanBeNull))
                    {
                        itemNamespace = CollectionNamespace;
                        keyClrType = keyType!.ToDisplayString(FullyQualifiedFormat);
                        valueClrType = valueType!.ToDisplayString(FullyQualifiedFormat);
                    }
                    else
                    {
                        unsupportedReasons.Add("member '" + member.Name + "' has unsupported key or value type in '" +
                                               memberType.ToDisplayString() + "'");
                    }
                }

                string? childNamespace = kind switch
                {
                    MemberKind.Collection or MemberKind.Dictionary =>
                        itemNamespace != contractNamespace ? itemNamespace : null,

                    // Its two members live in the System namespace, so the member element declares
                    // it exactly as it would for any other contract in a different namespace.
                    // DateOnly and TimeOnly declare it too, but only on the runtimes that do not
                    // know what they are - see the conditional the emitter puts around it.
                    MemberKind.DateTimeOffset or MemberKind.DateOnly or MemberKind.TimeOnly =>
                        SystemContractNamespace != contractNamespace ? SystemContractNamespace : null,

                    _ => ChildNamespaceToDeclare(memberType, contractNamespace)
                };

                // A serializable field carries no attribute to read, and upstream gives it Order 0
                // rather than the -1 an unspecified [DataMember] gets. Within one contract every
                // field has the same Order, so the sort below reduces to ordinal by name either way.
                members.Add(new MemberSpec(
                    Name: (dataMember is null ? null : GetNamedArgument(dataMember, "Name")) ?? member.Name,
                    Order: dataMember is null ? 0 : GetNamedArgumentInt32(dataMember, "Order") ?? -1,
                    EmitDefaultValue: dataMember is null || GetNamedArgumentBoolean(dataMember, "EmitDefaultValue") is not false,
                    IsRequired: dataMember is not null && GetNamedArgumentBoolean(dataMember, "IsRequired") == true,
                    MemberName: member.Name,
                    Kind: kind,
                    IsNullableValueType: isNullableValueType,
                    NestedContractFullyQualifiedName: nestedContract?.ToDisplayString(FullyQualifiedFormat),
                    ChildNamespaceToDeclare: childNamespace)
                {
                    ElementKind = elementKind,
                    ItemName = itemName,
                    ItemNamespace = itemNamespace,
                    ElementEnumFullyQualifiedName = elementEnum?.ToDisplayString(FullyQualifiedFormat),
                    ElementClrType = elementClrType,
                    CollectionIsArray = collectionIsArray,
                    CollectionIsArrayList = collectionIsArrayList,
                    NestedElementClrType = nestedElementClrType,
                    NestedCollectionIsArray = nestedCollectionIsArray,
                    IsSettable = member switch
                    {
                        IPropertySymbol property => property.SetMethod is { DeclaredAccessibility: Accessibility.Public },
                        IFieldSymbol field => !field.IsReadOnly,
                        _ => false
                    },
                    NestedItemName = nestedItemName,
                    NestedElementKind = nestedElementKind,
                    NestedElementCanBeNull = nestedElementCanBeNull,
                    KeyKind = keyKind,
                    ValueKind = valueKind,
                    KeyClrType = keyClrType,
                    ValueClrType = valueClrType,
                    KeyCanBeNull = keyCanBeNull,
                    ValueCanBeNull = valueCanBeNull,
                    ElementCanBeNull = elementCanBeNull,
                    Candidates = new EquatableArray<string>(candidates.ToArray()),
                    EnumCandidates = new EquatableArray<string>(enumCandidates.ToArray()),
                    Boxed = boxed
                });
            }

            // Order ascending, then ordinal by contract name. Unspecified Order is -1 and cannot be
            // written explicitly, so unordered members always precede ordered ones - including
            // Order = 0. Mirrors ClassDataContract.DataMemberComparer in dotnet/runtime.
            members.Sort(static (x, y) =>
            {
                int byOrder = x.Order.CompareTo(y.Order);
                return byOrder != 0 ? byOrder : string.CompareOrdinal(x.Name, y.Name);
            });

            return new ContractSpec(
                contractType.ToDisplayString(FullyQualifiedFormat),
                contractName,
                contractNamespace,
                new EquatableArray<MemberSpec>(members.ToArray()),
                new EquatableArray<string>(unsupportedReasons.ToArray()),
                baseContract?.ToDisplayString(FullyQualifiedFormat),
                isRoot)
            {
                Location = contractType.Locations.Length > 0 ? LocationInfo.From(contractType.Locations[0]) : null,
                IsReference = isReference,
                IsValueType = contractType.IsValueType,
                HasParameterlessConstructor = contractType.IsValueType
                    || contractType.InstanceConstructors.Any(c =>
                        c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public),
                KnownTypes = new EquatableArray<string>(knownTypes
                    .Select(t => t.ToDisplayString(FullyQualifiedFormat))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray())
            };
        }

        /// <summary>The element type of a supported collection, or null if this is not one.</summary>
        private static ITypeSymbol? CollectionElementTypeOf(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol { Rank: 1 } array)
            {
                return array.ElementType.SpecialType == SpecialType.System_Byte ? null : array.ElementType;
            }

            if (type is INamedTypeSymbol { IsGenericType: true } generic
                && generic.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.List<T>")
            {
                return generic.TypeArguments[0];
            }

            return null;
        }

        /// <summary>
        /// How the items of a collection are written, and what element name each gets.
        /// </summary>
        /// <remarks>
        /// The names are XSD-derived and several are not what the CLR type name suggests - sbyte is
        /// "byte", byte is "unsignedByte", TimeSpan is "duration". They are recorded in the
        /// SanityPrimitiveArrays fixture, which was produced by the real serializer, so this table
        /// is checked against the serializer rather than against memory.
        /// </remarks>
        private static MemberKind ClassifyCollectionElement(
            ITypeSymbol collectionType,
            string containingNamespace,
            out string? itemName,
            out string? itemNamespace,
            out INamedTypeSymbol? elementEnum,
            out bool canBeNull,
            out string? nestedItemName,
            out MemberKind nestedElementKind,
            out bool nestedElementCanBeNull)
        {
            itemName = null;
            itemNamespace = CollectionNamespace;
            elementEnum = null;
            canBeNull = false;
            nestedItemName = null;
            nestedElementKind = MemberKind.Unsupported;
            nestedElementCanBeNull = false;

            if (IsArrayList(collectionType))
            {
                itemName = "anyType";
                itemNamespace = CollectionNamespace;

                // WriteAnyType writes i:nil itself, so the caller must not also test for null.
                canBeNull = false;
                return MemberKind.Object;
            }

            ITypeSymbol? element = CollectionElementTypeOf(collectionType);
            if (element is null)
            {
                return MemberKind.Unsupported;
            }

            // A jagged collection: each outer item is itself an array, written as an ArrayOf element
            // holding the innermost items. byte[] does not qualify - CollectionElementTypeOf
            // deliberately declines it, so byte[][] stays a collection of base64 primitives.
            if (CollectionElementTypeOf(element) is ITypeSymbol innerElement)
            {
                MemberKind innerKind = ClassifyMember(innerElement, out bool innerIsNullable, out INamedTypeSymbol? _);
                string? innerName = innerIsNullable ? null : XsdNameOf(innerKind);
                if (innerName is null)
                {
                    return MemberKind.Unsupported;
                }

                nestedItemName = innerName;
                nestedElementKind = innerKind;
                nestedElementCanBeNull = innerKind is MemberKind.String or MemberKind.ByteArray or MemberKind.Uri;

                itemName = "ArrayOf" + innerName;
                itemNamespace = CollectionNamespace;

                // The outer item is an array, so a null one is written as i:nil like any other
                // missing reference.
                canBeNull = true;
                return MemberKind.Collection;
            }

            MemberKind kind = ClassifyMember(element, out bool elementIsNullable, out INamedTypeSymbol? elementContract);

            // An enum item is named after its own contract and lives in its own namespace, not in
            // the Arrays namespace the built-in types use - which is why AllTypes.enumArrayData
            // writes <a:MyEnum1> beside its containing contract rather than <b:...>.
            if (kind == MemberKind.Enum && !elementIsNullable && elementContract is not null)
            {
                elementEnum = elementContract;
                itemName = ContractNameOf(elementContract);
                itemNamespace = ContractNamespaceOf(elementContract);
                canBeNull = false;
                return MemberKind.Enum;
            }

            // Like an enum, DateTimeOffset is a contract rather than a built-in, so its items are
            // named after it and stay in the System namespace instead of the Arrays one.
            if (kind == MemberKind.DateTimeOffset && !elementIsNullable)
            {
                itemName = "DateTimeOffset";
                itemNamespace = SystemContractNamespace;
                canBeNull = false;
                return MemberKind.DateTimeOffset;
            }

            if (kind == MemberKind.Unsupported || kind == MemberKind.Collection || kind == MemberKind.Contract
                || kind == MemberKind.Enum || kind == MemberKind.Object || elementIsNullable)
            {
                // Nested collections, contract items and boxed items each need more than the item
                // name to write, so they stay on the reflection path for now.
                return MemberKind.Unsupported;
            }

            itemName = XsdNameOf(kind);

            if (itemName is null)
            {
                return MemberKind.Unsupported;
            }

            canBeNull = kind is MemberKind.String or MemberKind.ByteArray or MemberKind.Uri;
            return kind;
        }

        /// <summary>
        /// Builds the value-to-wire-name table for an enum, in declaration order.
        /// </summary>
        /// <remarks>
        /// Mirrors EnumDataContract.ImportDataMembers: when the enum carries [DataContract] only
        /// fields with [EnumMember] participate, and an explicitly set Value replaces the field
        /// name; otherwise every public constant participates under its own name. Declaration order
        /// is preserved because the flags decomposition consumes members in that order.
        /// </remarks>
        private static EnumSpec ParseEnum(INamedTypeSymbol enumType, string fullyQualifiedName)
        {
            bool hasDataContract = GetAttribute(enumType, DataContractAttributeName) is not null;
            bool isUnsignedLong = enumType.EnumUnderlyingType?.SpecialType == SpecialType.System_UInt64;
            bool isFlags = enumType.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");

            List<EnumMemberSpec> members = new();
            foreach (ISymbol member in enumType.GetMembers())
            {
                if (member is not IFieldSymbol { IsConst: true, HasConstantValue: true } field)
                {
                    continue;
                }

                string name = field.Name;
                if (hasDataContract)
                {
                    AttributeData? enumMember = field.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.Serialization.EnumMemberAttribute");
                    if (enumMember is null)
                    {
                        continue;
                    }

                    if (GetNamedArgument(enumMember, "Value") is string explicitValue && explicitValue.Length > 0)
                    {
                        name = explicitValue;
                    }
                }

                members.Add(new EnumMemberSpec(name, ToInt64(field.ConstantValue, isUnsignedLong)));
            }

            return new EnumSpec(fullyQualifiedName, isFlags, isUnsignedLong, new EquatableArray<EnumMemberSpec>(members.ToArray()))
            {
                ContractName = ContractNameOf(enumType),
                ContractNamespace = ContractNamespaceOf(enumType)
            };
        }

        /// <summary>
        /// Normalises an enum constant to the long the write algorithm compares against, matching
        /// EnumDataContract's use of Convert.ToUInt64 for ulong-backed enums and Convert.ToInt64
        /// otherwise.
        /// </summary>
        private static long ToInt64(object? constant, bool isUnsignedLong) => constant switch
        {
            null => 0L,
            ulong value when isUnsignedLong => unchecked((long)value),
            ulong value => unchecked((long)value),
            long value => value,
            uint value => value,
            int value => value,
            ushort value => value,
            short value => value,
            byte value => value,
            sbyte value => value,
            _ => 0L
        };

        /// <summary>
        /// Whether this member overrides one that is already a data member of a base contract.
        /// </summary>
        private static bool OverridesADataMember(ISymbol member)
        {
            for (IPropertySymbol? property = (member as IPropertySymbol)?.OverriddenProperty;
                 property is not null;
                 property = property.OverriddenProperty)
            {
                if (GetAttribute(property, DataMemberAttributeName) is not null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a type or anything in its hierarchy declares known types the generator cannot
        /// enumerate at compile time.
        /// </summary>
        /// <remarks>
        /// <c>[KnownType]</c> naming a type is handled - see TryCollectKnownTypes. The service-level
        /// attributes are not: they are resolved against the operation at run time, so a member of
        /// such a type may hold an instance no attribute here names. Writing the declared type's
        /// members for one would produce wrong XML rather than falling back.
        /// </remarks>
        private static bool DeclaresRuntimeKnownTypes(INamedTypeSymbol type)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                foreach (AttributeData attribute in current.GetAttributes())
                {
                    string? name = attribute.AttributeClass?.ToDisplayString();
                    if (name is "CoreWCF.ServiceKnownTypeAttribute"
                        or "System.ServiceModel.ServiceKnownTypeAttribute")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Every type reachable from this contract's <c>[KnownType]</c> attributes.
        /// </summary>
        /// <remarks>
        /// Mirrors DataContract.ImportKnownTypeAttributes in dotnet/runtime, which is more than a
        /// read of the type's own attributes: the base chain is walked, so a derived contract
        /// inherits what its base declared, and the closure is transitive, so a known type's own
        /// <c>[KnownType]</c>s join it. Missing either would silently narrow the set of runtime
        /// types a member is allowed to hold, turning a document the real serializer writes into an
        /// exception here.
        /// </remarks>
        private static bool TryCollectKnownTypes(INamedTypeSymbol contractType, List<INamedTypeSymbol> knownTypes, out string? unsupportedReason)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            Queue<INamedTypeSymbol> pending = new();
            pending.Enqueue(contractType);
            seen.Add(contractType.ToDisplayString(FullyQualifiedFormat));

            while (pending.Count > 0)
            {
                for (INamedTypeSymbol? current = pending.Dequeue(); current is not null; current = current.BaseType)
                {
                    foreach (AttributeData attribute in current.GetAttributes())
                    {
                        if (attribute.AttributeClass?.ToDisplayString() != KnownTypeAttributeName)
                        {
                            continue;
                        }

                        if (attribute.ConstructorArguments.Length != 1
                            || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol knownType)
                        {
                            // The methodName overload names a method returning the known types at
                            // run time. Nothing here can evaluate it, and guessing would produce a
                            // serializer that rejects instances the real one accepts.
                            unsupportedReason = "[KnownType] on " + current.Name +
                                                " names a method, which the generator cannot evaluate";
                            return false;
                        }

                        if (seen.Add(knownType.ToDisplayString(FullyQualifiedFormat)))
                        {
                            knownTypes.Add(knownType);
                            pending.Enqueue(knownType);
                        }
                    }
                }
            }

            unsupportedReason = null;
            return true;
        }

        /// <summary>
        /// The runtime types a member declared as <paramref name="declaredType"/> may hold, or an
        /// empty list when only the declared type itself is possible.
        /// </summary>
        /// <remarks>
        /// A non-empty result is what makes the member polymorphic and puts <c>i:type</c> on the
        /// wire. The declared type is included unless it is abstract, since an abstract contract
        /// can never be the runtime type.
        /// </remarks>
        private static List<INamedTypeSymbol> PolymorphicCandidates(INamedTypeSymbol declaredType, List<INamedTypeSymbol> knownTypes)
        {
            List<INamedTypeSymbol> candidates = new();
            HashSet<string> added = new(StringComparer.Ordinal);

            foreach (INamedTypeSymbol knownType in knownTypes)
            {
                if (!SymbolEqualityComparer.Default.Equals(knownType, declaredType)
                    && DerivesFrom(knownType, declaredType)
                    && IsContractType(knownType)
                    && added.Add(knownType.ToDisplayString(FullyQualifiedFormat)))
                {
                    candidates.Add(knownType);
                }
            }

            if (candidates.Count == 0)
            {
                return candidates;
            }

            if (!declaredType.IsAbstract)
            {
                candidates.Insert(0, declaredType);
            }

            return candidates;
        }

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol candidateBase)
        {
            for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, candidateBase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether this is the non-generic <c>ArrayList</c>.</summary>
        private static bool IsArrayList(ITypeSymbol type) =>
            type.ToDisplayString() == "System.Collections.ArrayList";

        /// <summary>Whether this is a <c>Dictionary&lt;K,V&gt;</c>, and what its arguments are.</summary>
        private static bool IsDictionary(ITypeSymbol type, out ITypeSymbol? key, out ITypeSymbol? value)
        {
            key = null;
            value = null;

            if (type is not INamedTypeSymbol { TypeArguments.Length: 2 } named
                || named.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.Dictionary<TKey, TValue>")
            {
                return false;
            }

            key = named.TypeArguments[0];
            value = named.TypeArguments[1];
            return true;
        }

        /// <summary>
        /// Classifies a dictionary's key and value, and builds the name its entries are written
        /// under.
        /// </summary>
        /// <remarks>
        /// An entry is named <c>KeyValueOf</c> followed by the XSD name of each type argument, which
        /// is why <c>Dictionary&lt;string, string&gt;</c> writes KeyValueOfstringstring and
        /// <c>Dictionary&lt;byte[], byte[]&gt;</c> writes KeyValueOfbase64Binarybase64Binary. Both
        /// are pinned by fixtures. Only built-in arguments are supported: a contract or enum
        /// argument would contribute its own contract name and, where the argument is itself
        /// generic, a hash - neither of which is worth guessing at.
        /// </remarks>
        private static bool TryClassifyDictionary(
            ITypeSymbol keyType,
            ITypeSymbol valueType,
            out string? entryName,
            out MemberKind keyKind,
            out MemberKind valueKind,
            out bool keyCanBeNull,
            out bool valueCanBeNull)
        {
            entryName = null;

            keyKind = ClassifyMember(keyType, out bool keyNullable, out INamedTypeSymbol? _);
            valueKind = ClassifyMember(valueType, out bool valueNullable, out INamedTypeSymbol? _);

            keyCanBeNull = keyKind is MemberKind.String or MemberKind.ByteArray or MemberKind.Uri;
            valueCanBeNull = valueKind is MemberKind.String or MemberKind.ByteArray or MemberKind.Uri;

            if (keyNullable || valueNullable)
            {
                return false;
            }

            string? keyName = XsdNameOf(keyKind);
            string? valueName = XsdNameOf(valueKind);
            if (keyName is null || valueName is null)
            {
                return false;
            }

            entryName = "KeyValueOf" + keyName + valueName;
            return true;
        }

        /// <summary>
        /// The XSD name a built-in type is written under, or null if it has none.
        /// </summary>
        /// <remarks>
        /// One table serving three jobs: the element name of a collection item, the local name of an
        /// <c>i:type</c>, and half of a dictionary entry's name. Several entries are not what the CLR
        /// type suggests - sbyte is "byte", byte is "unsignedByte", TimeSpan is "duration" - and all
        /// of them are pinned by the SanityPrimitiveArrays and SanityBoxedPrimitives fixtures.
        /// </remarks>
        private static string? XsdNameOf(MemberKind kind) => kind switch
        {
            MemberKind.Boolean => "boolean",
            MemberKind.Byte => "unsignedByte",
            MemberKind.SByte => "byte",
            MemberKind.Int16 => "short",
            MemberKind.UInt16 => "unsignedShort",
            MemberKind.Int32 => "int",
            MemberKind.UInt32 => "unsignedInt",
            MemberKind.Int64 => "long",
            MemberKind.UInt64 => "unsignedLong",
            MemberKind.Single => "float",
            MemberKind.Double => "double",
            MemberKind.Decimal => "decimal",
            MemberKind.Char => "char",
            MemberKind.String => "string",
            MemberKind.Guid => "guid",
            MemberKind.DateTime => "dateTime",
            MemberKind.TimeSpan => "duration",
            MemberKind.ByteArray => "base64Binary",
            MemberKind.Uri => "anyURI",
            _ => null
        };

        /// <summary>The wire name of a contract or enum: its <c>Name</c>, or the type's own name.</summary>
        private static string ContractNameOf(INamedTypeSymbol type) =>
            (GetAttribute(type, DataContractAttributeName) is AttributeData contract
                ? GetNamedArgument(contract, "Name")
                : null) ?? type.Name;

        /// <summary>The wire namespace of a contract or enum.</summary>
        private static string ContractNamespaceOf(INamedTypeSymbol type) =>
            (GetAttribute(type, DataContractAttributeName) is AttributeData contract
                ? GetNamedArgument(contract, "Namespace")
                : null) ?? DefaultNamespaceFor(type);

        /// <summary>
        /// Whether this type is a contract the generator recognises - either an explicit
        /// <c>[DataContract]</c> or a <c>[Serializable]</c> type.
        /// </summary>
        private static bool IsContractType(INamedTypeSymbol type) =>
            GetAttribute(type, DataContractAttributeName) is not null || IsSerializableContract(type);

        /// <summary>
        /// Whether this type is written from its fields because it carries <c>[Serializable]</c> and
        /// no <c>[DataContract]</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>[DataContract]</c> wins when a type carries both, which is why it is checked first -
        /// BaseSerializable in the corpus carries both and is written from its <c>[DataMember]</c>s.
        /// See ClassDataContract.ImportDataMembers in dotnet/runtime, where the serializable branch
        /// is the else of hasDataContract.
        /// </para>
        /// <para>
        /// Restricted to types declared in source. <c>[Serializable]</c> is common in the framework -
        /// Uri, ArrayList and Dictionary all carry it - and treating those as contracts would mean
        /// claiming to write types whose fields are an implementation detail and whose wire format
        /// is nothing like their field layout. A type from metadata keeps the answer it had before.
        /// </para>
        /// </remarks>
        private static bool IsSerializableContract(INamedTypeSymbol type) =>
            GetAttribute(type, DataContractAttributeName) is null
            && GetAttribute(type, ClrSerializableAttributeName) is not null
            && type.TypeKind is TypeKind.Class or TypeKind.Struct
            && type.DeclaringSyntaxReferences.Length > 0
            && !ImplementsISerializable(type);

        /// <summary>
        /// Whether the type takes over its own serialization, which is a different write algorithm
        /// entirely and one the generator does not implement.
        /// </summary>
        private static bool ImplementsISerializable(INamedTypeSymbol type)
        {
            foreach (INamedTypeSymbol iface in type.AllInterfaces)
            {
                if (iface.ToDisplayString() == "System.Runtime.Serialization.ISerializable")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether this contract or any of its bases sets <c>IsReference</c>.</summary>
        private static bool InheritsIsReference(INamedTypeSymbol type)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (GetAttribute(current, DataContractAttributeName) is AttributeData contract
                    && GetNamedArgumentBoolean(contract, "IsReference") == true)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The namespace to declare on a member element, or null for none.
        /// </summary>
        /// <remarks>
        /// Mirrors ClassDataContract.GetChildNamespaceToDeclare: built-in contracts, enums and
        /// IXmlSerializable declare nothing, and otherwise the child's namespace is declared only
        /// when it differs from the containing contract's. This is what puts xmlns:b on the member
        /// element rather than the root.
        /// </remarks>
        private static string? ChildNamespaceToDeclare(ITypeSymbol memberType, string containingNamespace)
        {
            ITypeSymbol type = UnwrapNullable(memberType);

            if (type.TypeKind == TypeKind.Enum || type is not INamedTypeSymbol named || !IsContractType(named))
            {
                return null;
            }

            string ns = GetAttribute(named, DataContractAttributeName) is AttributeData contract
                ? GetNamedArgument(contract, "Namespace") ?? DefaultNamespaceFor(named)
                : DefaultNamespaceFor(named);

            return ns.Length > 0 && ns != containingNamespace ? ns : null;
        }

        private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments.Length == 1
                ? nullable.TypeArguments[0]
                : type;

        /// <summary>
        /// The contract namespace a type gets when its [DataContract] does not name one:
        /// the CLR namespace appended to a fixed prefix. An empty CLR namespace yields the prefix
        /// with no trailing segment.
        /// </summary>
        private static string DefaultNamespaceFor(INamedTypeSymbol type) =>
            type.ContainingNamespace.IsGlobalNamespace
                ? DefaultNamespacePrefix
                : DefaultNamespacePrefix + type.ContainingNamespace.ToDisplayString();

        /// <summary>
        /// Maps a member's CLR type onto how its value is written, or
        /// <see cref="MemberKind.Unsupported"/> when this generator cannot write it yet.
        /// </summary>
        private static MemberKind ClassifyMember(ITypeSymbol type, out bool isNullableValueType, out INamedTypeSymbol? nestedContract)
        {
            isNullableValueType = false;
            nestedContract = null;

            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                && nullable.TypeArguments.Length == 1)
            {
                isNullableValueType = true;
                type = nullable.TypeArguments[0];
            }

            if (type is IArrayTypeSymbol { Rank: 1 } array
                && array.ElementType.SpecialType == SpecialType.System_Byte)
            {
                return isNullableValueType ? MemberKind.Unsupported : MemberKind.ByteArray;
            }

            MemberKind kind = type.SpecialType switch
            {
                SpecialType.System_Boolean => MemberKind.Boolean,
                SpecialType.System_Byte => MemberKind.Byte,
                SpecialType.System_SByte => MemberKind.SByte,
                SpecialType.System_Int16 => MemberKind.Int16,
                SpecialType.System_UInt16 => MemberKind.UInt16,
                SpecialType.System_Int32 => MemberKind.Int32,
                SpecialType.System_UInt32 => MemberKind.UInt32,
                SpecialType.System_Int64 => MemberKind.Int64,
                SpecialType.System_UInt64 => MemberKind.UInt64,
                SpecialType.System_Single => MemberKind.Single,
                SpecialType.System_Double => MemberKind.Double,
                SpecialType.System_Decimal => MemberKind.Decimal,
                SpecialType.System_Char => MemberKind.Char,
                SpecialType.System_String => MemberKind.String,
                SpecialType.System_DateTime => MemberKind.DateTime,

                // Declared as object, so the runtime type decides everything and has to be written
                // out as i:type. Nullable<object> does not exist, so no guard is needed here.
                SpecialType.System_Object => MemberKind.Object,
                _ => MemberKind.Unsupported
            };

            if (kind != MemberKind.Unsupported)
            {
                if (isNullableValueType && !type.IsValueType)
                {
                    isNullableValueType = false;
                }

                return kind;
            }

            switch (type.ToDisplayString())
            {
                case "System.Guid":
                    return MemberKind.Guid;
                case "System.TimeSpan":
                    return MemberKind.TimeSpan;
                case "System.Uri":
                    return isNullableValueType ? MemberKind.Unsupported : MemberKind.Uri;
                case "System.DateOnly":
                    return MemberKind.DateOnly;
                case "System.TimeOnly":
                    return MemberKind.TimeOnly;
                case "System.Xml.XmlQualifiedName":
                    return isNullableValueType ? MemberKind.Unsupported : MemberKind.QName;
                case "System.DateTimeOffset":
                    return MemberKind.DateTimeOffset;

                // Declared as an abstract base rather than a concrete type, so the runtime type
                // decides everything - the same shape as object, over a narrower candidate set.
                case "System.ValueType":
                case "System.Enum":
                case "System.Array":
                    return isNullableValueType ? MemberKind.Unsupported : MemberKind.Object;
            }

            if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
            {
                nestedContract = enumType;
                return MemberKind.Enum;
            }

            // A one-dimensional array of anything but byte, or a List<T>, is written as a
            // collection. byte[] is deliberately excluded above: the serializer treats it as a
            // primitive written as base64, not as an array of bytes.
            if (!isNullableValueType && IsDictionary(type, out ITypeSymbol? _, out ITypeSymbol? _))
            {
                return MemberKind.Dictionary;
            }

            // ArrayList holds anything, so it is written as a sequence of anyType items that each
            // announce their own runtime type - the same shape as an object member, one per item.
            if (!isNullableValueType && (IsArrayList(type) || CollectionElementTypeOf(type) is not null))
            {
                return MemberKind.Collection;
            }

            // A member that is itself a data contract is written inline by that contract's content
            // writer. Structs are excluded for now: a nullable struct contract would need the
            // nullable unwrapping and the nil handling to compose, which is untested.
            if (type is INamedTypeSymbol named
                && named.TypeKind == TypeKind.Class
                && IsContractType(named))
            {
                if (DeclaresRuntimeKnownTypes(named))
                {
                    return MemberKind.Unsupported;
                }

                nestedContract = named;
                return MemberKind.Contract;
            }

            return MemberKind.Unsupported;
        }

        private static bool IsPartial(SyntaxNode node) =>
            node is TypeDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        private static bool DerivesFromContextBase(INamedTypeSymbol type)
        {
            for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.ToDisplayString() == ContextBaseTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static AttributeData? GetAttribute(ISymbol symbol, string attributeMetadataName) =>
            symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == attributeMetadataName);

        private static string? GetNamedArgument(AttributeData attribute, string name) =>
            attribute.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value as string;

        private static bool? GetNamedArgumentBoolean(AttributeData attribute, string name) =>
            attribute.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value as bool?;

        private static int? GetNamedArgumentInt32(AttributeData attribute, string name) =>
            attribute.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value as int?;

        /// <summary>
        /// A file name unique per context. Nested and generic types would otherwise collide, so the
        /// characters that are illegal or ambiguous in a hint name are folded to underscores.
        /// </summary>
        private static string HintNameFor(INamedTypeSymbol contextType)
        {
            string full = contextType.ToDisplayString();
            char[] buffer = full.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (c is '<' or '>' or ',' or ' ' or '`' or '+' or ':' or '/' or '\\')
                {
                    buffer[i] = '_';
                }
            }

            return new string(buffer) + ".DataContractSerializers.g.cs";
        }

        internal static readonly SymbolDisplayFormat FullyQualifiedFormat =
            SymbolDisplayFormat.FullyQualifiedFormat.RemoveMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
    }
}
