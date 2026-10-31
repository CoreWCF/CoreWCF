// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SerializationTestTypes;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// Registers the contract types imported from dotnet/runtime's
    /// SerializationTestTypes/Primitives.cs. See SerializationTestTypes/UPSTREAM.md.
    /// </summary>
    public static partial class CorpusCatalog
    {
        static partial void RegisterPrimitives(CorpusBuilder builder)
        {
            // The upstream conventions: a populating constructor taking (bool init) or
            // (string variation), alongside a parameterless one for deserialization.
            builder.Add<Person>("variation", () => new Person("v1")).WithTags("upstream", "fields");
            builder.Add<Person>("default", () => new Person()).WithTags("upstream", "defaults");
            builder.Add<ArrayContainer>("init", () => new ArrayContainer(true))
                   .WithKnownTypes(typeof(string))
                   .WithTags("upstream", "arraylist");

            // Primitive coverage at the extremes: MinValue/MaxValue for every numeric type,
            // char boundaries, hardcoded Guids and default DateTime.
            builder.Add<AllTypes>("default", () => new AllTypes())
                   .WithKnownTypes(typeof(MyEnum1), typeof(PublicDCStruct))
                   .WithTags("upstream", "primitives", "extremes");
            builder.Add<AllTypes2>("default", () => new AllTypes2())
                   .WithKnownTypes(typeof(MyEnum1), typeof(PublicDCStruct))
                   .WithTags("upstream", "primitives", "nullable");
            builder.Add<CharClass>("default", () => new CharClass()).WithTags("upstream", "char");

            builder.Add<DictContainer>("default", () => new DictContainer()).WithTags("upstream", "dictionary");
            builder.Add<ListContainer>("default", () => new ListContainer()).WithTags("upstream", "list");
            builder.Add<Array3>("default", () => new Array3()).WithTags("upstream", "jagged-array");
            builder.Add<Array22>("default", () => new Array22()).WithTags("upstream", "arrays");

            builder.Add<EmptyDC>("default", () => new EmptyDC()).WithTags("upstream", "empty");
            builder.Add<Properties>("default", () => new Properties()).WithTags("upstream", "properties");
            builder.Add<Temp>("default", () => new Temp()).WithTags("upstream");
            builder.Add<WithStatic>("default", () => new WithStatic()).WithTags("upstream", "static");
            builder.Add<VT>("value", () => new VT(10)).WithTags("upstream", "struct");
            builder.Add<PublicDCStruct>("init", () => new PublicDCStruct(true)).WithTags("upstream", "struct");

            // Nested contract type: exercises the '+' in Type.FullName reaching the fixture name.
            builder.Add<OutClass>("default", () => new OutClass()).WithTags("upstream", "nested-type");
            builder.Add<OutClass.NestedClass>("default", () => new OutClass.NestedClass()).WithTags("upstream", "nested-type");

            // Inheritance across an internal interface.
            builder.Add<Base>("default", () => new Base()).WithTags("upstream", "inheritance");
            builder.Add<Derived>("default", () => new Derived()).WithTags("upstream", "inheritance");

            // Non-public constructors: the generator cannot rely on a public parameterless ctor.
            builder.Add<PrivateCstor>("public-ctor", () => new PrivateCstor(int.MaxValue)).WithTags("upstream", "ctor");
            builder.Add<DerivedFromPriC>("default", () => new DerivedFromPriC()).WithTags("upstream", "ctor", "inheritance");

            // Boxed primitives declared as object, resolved through [KnownType].
            builder.Add<BoxedPrim>("default", () => new BoxedPrim())
                   .WithKnownTypes(typeof(VT))
                   .WithTags("upstream", "boxed", "knowntype");

            // Self-referencing linked list without IsReference: the graph is a chain, not a cycle.
            builder.Add<List>("chain", () => new List { value = 1, next = new List { value = 2 } })
                   .WithTags("upstream", "recursive");

            builder.Add<EnumContainer1>("default", () => new EnumContainer1()).WithTags("upstream", "enums");
            builder.Add<EnumContainer2>("default", () => new EnumContainer2()).WithTags("upstream", "enums");
            builder.Add<EnumContainer3>("default", () => new EnumContainer3()).WithTags("upstream", "enums");
            builder.Add<SeasonsEnumContainer>("default", () => new SeasonsEnumContainer()).WithTags("upstream", "enums", "flags");

#if !NETFRAMEWORK
            // DateOnly/TimeOnly do not exist on .NET Framework; the type itself carries the same guard.
            builder.Add<DateTimeOnlyWrapper>("default", () => new DateTimeOnlyWrapper()).WithTags("upstream", "dateonly");
#endif

            // Out of scope for v1: these depend on implicit/POCO contracts (NotSer and MyStruct
            // carry no [DataContract]), which the generator does not target.
            // Its a4 member is a 10,000-element array, producing a 160 KB fixture that is 93% of the
            // whole corpus by size and structurally identical to the 1- and 4-element arrays in the
            // same type. Empty, single and multi-element arrays are all still covered by Array3,
            // Array22 and SanityCollections. Shrinking a4 was rejected: editing an imported type
            // defeats the point of reusing upstream instances.
            builder.Skip<Arrays>("10,000-element array produces a 160 KB fixture with no coverage the smaller array cases lack.");

            builder.Skip<EnumStructContainer>("Contains POCO structs (NotSer, MyStruct); implicit contracts are out of scope for v1.");
            builder.Skip<HaveNS>("Member type NotSer is a POCO struct; implicit contracts are out of scope for v1.");

            // Enum contracts, covered as members rather than as roots.
            builder.Skip<MyEnum1>("Covered as a known type of AllTypes and via EnumContainer1.");
            builder.Skip<MyEnum>("Covered as a member of EnumContainer2.");
            builder.Skip<MyEnum2>("Covered as a member of EnumContainer3.");
            builder.Skip<MyEnum3>("Enum contract covered by the EnumContainer cases.");
            builder.Skip<MyEnum4>("Enum contract covered by the EnumContainer cases.");
            builder.Skip<MyEnum7>("Enum contract covered by the EnumContainer cases.");
            builder.Skip<MyEnum8>("Enum contract covered by the EnumContainer cases.");
            builder.Skip<Seasons3>("Flags enum covered as a member of SeasonsEnumContainer.");
        }
    }
}
