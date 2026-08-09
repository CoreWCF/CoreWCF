// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.DataContractSerialization.Generator;

public sealed partial class DataContractSerializerGenerator
{
    /// <summary>One data contract, reduced to what the emitter needs to write it.</summary>
    /// <remarks>
    /// A contract the generator cannot yet write is kept in the spec with
    /// <see cref="UnsupportedReasons"/> set rather than dropped, so the emitter can record why in a
    /// comment. No serializer is emitted for it and <c>GetSerializer</c> returns null, which sends
    /// CoreWCF back to the reflection-based serializer - a correct outcome, not a failure.
    /// </remarks>
    internal sealed record ContractSpec(
        string FullyQualifiedName,
        string ContractName,
        string ContractNamespace,
        EquatableArray<MemberSpec> Members,
        EquatableArray<string> UnsupportedReasons,
        string? BaseContractFullyQualifiedName,
        bool IsRoot) : IEquatable<ContractSpec>
    {
        /// <summary>
        /// Whether this contract preserves object identity, writing <c>z:Id</c> on first sight of an
        /// instance and <c>z:Ref</c> on every later one.
        /// </summary>
        /// <remarks>
        /// Inherited: a derived contract that says nothing still gets it from its base. It is a
        /// property of the contract rather than of the member referring to it, so the decision is
        /// taken where the element is opened - at the root, or on the member element.
        /// </remarks>
        public bool IsReference { get; init; }

        /// <summary>
        /// The transitive closure of this contract's <c>[KnownType]</c> attributes.
        /// </summary>
        /// <remarks>
        /// Recorded on the spec so the generated context can answer, at run time, whether the known
        /// types CoreWCF supplies from the operation are ones this serializer already resolves.
        /// Anything beyond them has to go back to reflection.
        /// </remarks>
        public EquatableArray<string> KnownTypes { get; init; } = new EquatableArray<string>(Array.Empty<string>());

        /// <summary>
        /// Whether the type can be constructed by generated code.
        /// </summary>
        /// <remarks>
        /// DataContractSerializer does not need this - it allocates without running a constructor -
        /// but generated code has no such option, so a contract without an accessible parameterless
        /// constructor can be written and not read.
        /// </remarks>
        public bool HasParameterlessConstructor { get; init; }

        /// <summary>
        /// Whether the contract is a struct, so a reader has to take it by reference.
        /// </summary>
        /// <remarks>
        /// Passing one by value reads the whole document and then throws the result away, member by
        /// member, without failing anywhere - which is exactly how it presented.
        /// </remarks>
        public bool IsValueType { get; init; }

        /// <summary>Where the contract is declared, for a diagnostic to point at.</summary>
        /// <remarks>
        /// The one piece of syntax on the spec, and it does cost some incrementality: editing the
        /// file a contract lives in shifts its span and so invalidates the spec even when the
        /// contract's shape has not changed. The trade is deliberate - a fallback warning with no
        /// location is one the user cannot act on - and it is bounded, because a change to that file
        /// re-runs the parse for that contract anyway in every case but a moved comment.
        /// </remarks>
        public LocationInfo? Location { get; init; }

        public bool IsSupported => UnsupportedReasons.Count == 0;

        /// <summary>
        /// Same contract, with one more reason it cannot be written.
        /// </summary>
        /// <remarks>
        /// Every reason is kept rather than only the first. A wide contract can be blocked on
        /// several unrelated things at once, and reporting one at a time makes the remaining work
        /// look like a fraction of what it is - which is exactly how AllTypes was misread as one
        /// capability away when it needed eight.
        /// </remarks>
        public ContractSpec WithUnsupportedReason(string reason) =>
            this with { UnsupportedReasons = UnsupportedReasons.Add(reason) };
    }

    /// <summary>
    /// One <c>[DataMember]</c>, already resolved to its wire name and ordering key.
    /// </summary>
    /// <remarks>
    /// <paramref name="Order"/> is the raw attribute value: -1 when unspecified, and never negative
    /// otherwise because DataMemberAttribute's setter rejects negatives. Members therefore sort by
    /// Order ascending then by ordinal comparison of <paramref name="Name"/>, which puts every
    /// unordered member ahead of every ordered one - including Order = 0. See
    /// ClassDataContract.DataMemberComparer in dotnet/runtime.
    /// </remarks>
    internal sealed record MemberSpec(
        string Name,
        int Order,
        bool EmitDefaultValue,
        bool IsRequired,
        string MemberName,
        MemberKind Kind,
        bool IsNullableValueType,
        string? NestedContractFullyQualifiedName,
        string? ChildNamespaceToDeclare) : IEquatable<MemberSpec>
    {
        /// <summary>For <see cref="MemberKind.Collection"/>, how each item is written.</summary>
        public MemberKind ElementKind { get; init; }

        /// <summary>
        /// For <see cref="MemberKind.Collection"/>, the element name each item gets - the XSD name
        /// of the item type, recorded from the serializer itself rather than assumed.
        /// </summary>
        public string? ItemName { get; init; }

        /// <summary>
        /// For <see cref="MemberKind.Collection"/>, the namespace each item element sits in.
        /// </summary>
        /// <remarks>
        /// The Arrays namespace for the built-in types, but an enum item is named after its own
        /// contract and stays in its own namespace instead.
        /// </remarks>
        public string? ItemNamespace { get; init; }

        /// <summary>
        /// For a collection of enums, the enum whose value/name table writes each item.
        /// </summary>
        public string? ElementEnumFullyQualifiedName { get; init; }

        /// <summary>
        /// For a jagged collection, the element name each innermost item gets.
        /// </summary>
        /// <remarks>
        /// The outer items are named <c>ArrayOf</c> plus this, which is how int[][] writes
        /// ArrayOfint entries each holding int elements.
        /// </remarks>
        public string? NestedItemName { get; init; }

        /// <summary>For a jagged collection, how each innermost item is written.</summary>
        public MemberKind NestedElementKind { get; init; }

        /// <summary>For a jagged collection, whether an innermost item may be null.</summary>
        public bool NestedElementCanBeNull { get; init; }

        /// <summary>
        /// For <see cref="MemberKind.Collection"/>, the CLR type of one item.
        /// </summary>
        /// <remarks>
        /// Writing a collection never needs this - foreach infers it - but reading has to name the
        /// list it accumulates into before it has anything to put in it.
        /// </remarks>
        public string? ElementClrType { get; init; }

        /// <summary>Whether the collection is an array rather than a List, which decides how a read ends.</summary>
        public bool CollectionIsArray { get; init; }

        /// <summary>Whether the collection is an <c>ArrayList</c>, which is its own container.</summary>
        /// <remarks>
        /// Writing one needs nothing but foreach. Reading has to build it, and an ArrayList is
        /// neither a List&lt;T&gt; nor an array, so it is the one container the read cannot infer
        /// from <see cref="CollectionIsArray"/> alone.
        /// </remarks>
        public bool CollectionIsArrayList { get; init; }

        /// <summary>For a jagged collection, the CLR type of one innermost item.</summary>
        public string? NestedElementClrType { get; init; }

        /// <summary>For a jagged collection, whether each inner collection is an array.</summary>
        public bool NestedCollectionIsArray { get; init; }

        /// <summary>Whether generated code can assign this member, as opposed to only read it.</summary>
        public bool IsSettable { get; init; }

        /// <summary>For <see cref="MemberKind.Dictionary"/>, how the key is written.</summary>
        public MemberKind KeyKind { get; init; }

        /// <summary>For <see cref="MemberKind.Dictionary"/>, how the value is written.</summary>
        public MemberKind ValueKind { get; init; }

        /// <summary>For <see cref="MemberKind.Dictionary"/>, the CLR type of the key.</summary>
        /// <remarks>
        /// Writing a dictionary never needs this - foreach infers both type arguments - but reading
        /// has to name the pair it is building before it has either half of one.
        /// </remarks>
        public string? KeyClrType { get; init; }

        /// <summary>For <see cref="MemberKind.Dictionary"/>, the CLR type of the value.</summary>
        public string? ValueClrType { get; init; }

        /// <summary>For <see cref="MemberKind.Dictionary"/>, whether the key may be null.</summary>
        public bool KeyCanBeNull { get; init; }

        /// <summary>For <see cref="MemberKind.Dictionary"/>, whether the value may be null.</summary>
        public bool ValueCanBeNull { get; init; }

        /// <summary>For <see cref="MemberKind.Collection"/>, whether an item may be null.</summary>
        public bool ElementCanBeNull { get; init; }

        /// <summary>
        /// For <see cref="MemberKind.Contract"/>, every runtime type this member may hold - empty
        /// when only the declared type is possible.
        /// </summary>
        /// <remarks>
        /// Non-empty makes the member polymorphic: the writer is chosen by exact runtime type and
        /// the choice is announced with <c>i:type</c>, except for the declared type itself.
        /// </remarks>
        public EquatableArray<string> Candidates { get; init; } = new EquatableArray<string>(Array.Empty<string>());

        /// <summary>
        /// For <see cref="MemberKind.Object"/>, the enums the member may hold.
        /// </summary>
        /// <remarks>
        /// Kept apart from <see cref="Candidates"/> because an enum is written from its value/name
        /// table rather than by a content writer, and it has no <c>ContractSpec</c> to look up.
        /// </remarks>
        public EquatableArray<string> EnumCandidates { get; init; } = new EquatableArray<string>(Array.Empty<string>());

        /// <summary>
        /// For <see cref="MemberKind.Object"/>, which abstract type the member is declared as.
        /// </summary>
        /// <remarks>
        /// All three are written the same way - a switch on the runtime type, announced with
        /// i:type - but they admit different candidates, and the difference is not cosmetic:
        /// casting a <c>ValueType</c> to <c>string</c> is a compile error, so emitting the full
        /// table for one would produce generated code that does not build.
        /// </remarks>
        public BoxedDeclaration Boxed { get; init; }
    }

    /// <summary>
    /// An enum reachable from a contract, reduced to the value-to-wire-name table needed to write it.
    /// </summary>
    /// <remarks>
    /// Members are in declaration order, which the flags decomposition depends on. When the enum
    /// itself carries <c>[DataContract]</c> only fields with <c>[EnumMember]</c> participate;
    /// otherwise every public static field does. See EnumDataContract.ImportDataMembers.
    /// </remarks>
    internal sealed record EnumSpec(
        string FullyQualifiedName,
        bool IsFlags,
        bool IsUnsignedLong,
        EquatableArray<EnumMemberSpec> Members) : IEquatable<EnumSpec>
    {
        /// <summary>The wire name, for the <c>i:type</c> of an enum in a boxed position.</summary>
        public string ContractName { get; init; } = string.Empty;

        /// <summary>The wire namespace, for the same reason.</summary>
        public string ContractNamespace { get; init; } = string.Empty;
    }

    internal sealed record EnumMemberSpec(string Name, long Value) : IEquatable<EnumMemberSpec>;

    /// <summary>
    /// The abstract type a boxed member is declared as, which decides what it may hold.
    /// </summary>
    internal enum BoxedDeclaration
    {
        /// <summary>Anything: every primitive, every known contract, every known enum.</summary>
        Object = 0,

        /// <summary>Value types only, so the reference-typed primitives are not candidates.</summary>
        ValueType,

        /// <summary>Enums only.</summary>
        Enum,

        /// <summary>
        /// Arrays. Only <c>object[]</c> is written, as a sequence of anyType items.
        /// </summary>
        Array
    }

    /// <summary>
    /// How a member's value is written. Mirrors the primitive cases XmlWriterDelegator handles in
    /// dotnet/runtime, plus <see cref="Contract"/> for a member that is itself a data contract;
    /// anything not listed here makes its contract unsupported for now.
    /// </summary>
    internal enum MemberKind
    {
        Unsupported = 0,
        Boolean,
        Byte,
        SByte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
        Decimal,
        Char,
        String,
        Guid,
        DateTime,
        TimeSpan,
        ByteArray,
        Uri,

        /// <summary>
        /// <c>DateOnly</c>, whose wire format depends on the runtime rather than the contract.
        /// </summary>
        DateOnly,

        /// <summary><c>TimeOnly</c>, with the same runtime dependence as DateOnly.</summary>
        TimeOnly,

        /// <summary>
        /// <c>XmlQualifiedName</c>, the one member type whose element carries a prefix of its own.
        /// </summary>
        QName,

        /// <summary>
        /// <c>DateTimeOffset</c>, which is not a primitive at all: it is written as a two-member
        /// contract in the System namespace, from the adapter DataContractSerializer swaps in.
        /// </summary>
        DateTimeOffset,
        Contract,
        Enum,
        Collection,

        /// <summary>
        /// A <c>Dictionary&lt;K,V&gt;</c>, written as entries named after both type arguments.
        /// </summary>
        Dictionary,

        /// <summary>
        /// A member declared as <c>object</c>, whose runtime type is announced with <c>i:type</c>.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Contract"/> the declared type constrains nothing, so the candidates are
        /// every boxed primitive plus whatever <c>[KnownType]</c> names.
        /// </remarks>
        Object
    }
}
