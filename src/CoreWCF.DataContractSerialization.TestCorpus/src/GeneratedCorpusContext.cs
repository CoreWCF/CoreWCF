// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.DataContractSerialization.TestCorpus.Sanity;
using SerializationTestTypes;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// Every contract type in the corpus, offered to the source generator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately lists everything rather than only what the generator can currently emit. The
    /// generator decides per contract whether it can write it and silently declines the rest, so
    /// this list is the <em>ask</em> and the skip reasons in <c>GeneratedGoldenRecordTests</c>
    /// output are the <em>answer</em>. Curating this list to what already works would make the
    /// coverage figure self-fulfilling.
    /// </para>
    /// <para>
    /// Declared here rather than in the corpus project on purpose. Attaching the generator to the
    /// corpus would couple it to the oracle: generated code that failed to compile would break the
    /// corpus build and fail every reflection-based test too - the very tests that prove the
    /// generator wrong. Keeping it here means a generator bug fails only the generated tests.
    /// </para>
    /// <para>
    /// The cost is that generated code cannot reach a non-public <c>[DataMember]</c> across the
    /// assembly boundary; <c>BaseDCNoIsRef</c> is the one corpus type affected. When that matters
    /// this moves into the corpus and the trade reverses.
    /// </para>
    /// </remarks>
    [DataContractSerializable(typeof(SanityPrimitives))]
    [DataContractSerializable(typeof(SanityMemberAttributes))]
    [DataContractSerializable(typeof(SanityCustomNaming))]
    [DataContractSerializable(typeof(SanityNestedNamespace))]
    [DataContractSerializable(typeof(SanityCollections))]
    [DataContractSerializable(typeof(SanityPrimitiveArrays))]
    [DataContractSerializable(typeof(SanityBoxedPrimitives))]
    [DataContractSerializable(typeof(SanityUriAndOffset))]
#if !NETFRAMEWORK
    [DataContractSerializable(typeof(SanityDateAndTimeOnly))]
#endif
    [DataContractSerializable(typeof(SanityEnums))]
    [DataContractSerializable(typeof(SanityEnumCollections))]
    [DataContractSerializable(typeof(SanityDictionaries))]
    [DataContractSerializable(typeof(SanityUntypedCollections))]
    [DataContractSerializable(typeof(SanityQualifiedNames))]
    [DataContractSerializable(typeof(SanityNullable))]
    [DataContractSerializable(typeof(SanityDerived))]
    [DataContractSerializable(typeof(SanityFurtherDerived))]
    [DataContractSerializable(typeof(SanityBase))]
    [DataContractSerializable(typeof(SanityPolymorphic))]
    [DataContractSerializable(typeof(SanityKnownTypeHolder))]
    [DataContractSerializable(typeof(SanityReferenceNode))]

    [DataContractSerializable(typeof(AllTypes))]
    [DataContractSerializable(typeof(AllTypes2))]
    [DataContractSerializable(typeof(Array3))]
    [DataContractSerializable(typeof(Array22))]
    [DataContractSerializable(typeof(ArrayContainer))]
    [DataContractSerializable(typeof(BoxedPrim))]
    [DataContractSerializable(typeof(CharClass))]
    // DateOnly/TimeOnly do not exist on .NET Framework, so the type itself is guarded and this
    // attribute carries the same condition - matching CorpusCatalog.Primitives.
#if !NETFRAMEWORK
    [DataContractSerializable(typeof(DateTimeOnlyWrapper))]
#endif
    [DataContractSerializable(typeof(DerivedFromPriC))]
    [DataContractSerializable(typeof(DictContainer))]
    [DataContractSerializable(typeof(EmptyDC))]
    [DataContractSerializable(typeof(EnumContainer1))]
    [DataContractSerializable(typeof(EnumContainer2))]
    [DataContractSerializable(typeof(EnumContainer3))]
    [DataContractSerializable(typeof(ListContainer))]
    [DataContractSerializable(typeof(OutClass))]
    [DataContractSerializable(typeof(OutClass.NestedClass))]
    [DataContractSerializable(typeof(Person))]
    [DataContractSerializable(typeof(PrivateCstor))]
    [DataContractSerializable(typeof(Properties))]
    [DataContractSerializable(typeof(PublicDCStruct))]
    [DataContractSerializable(typeof(SeasonsEnumContainer))]
    [DataContractSerializable(typeof(Temp))]
    [DataContractSerializable(typeof(VT))]
    [DataContractSerializable(typeof(WithStatic))]
    [DataContractSerializable(typeof(SerializationTestTypes.List))]

    [DataContractSerializable(typeof(Base))]
    [DataContractSerializable(typeof(Derived))]
    [DataContractSerializable(typeof(BaseDC))]
    [DataContractSerializable(typeof(BaseDCNoIsRef))]
    [DataContractSerializable(typeof(DerivedDC))]
    [DataContractSerializable(typeof(Derived2DC))]
    [DataContractSerializable(typeof(SimpleDC))]
    [DataContractSerializable(typeof(SimpleDCWithRef))]
    [DataContractSerializable(typeof(BaseNoIsRef))]
    [DataContractSerializable(typeof(BaseWithIsRefTrue))]
    [DataContractSerializable(typeof(DerivedNoIsRef))]
    [DataContractSerializable(typeof(DerivedNoIsRef2))]
    [DataContractSerializable(typeof(DerivedNoIsRef3))]
    [DataContractSerializable(typeof(DerivedNoIsRef4))]
    [DataContractSerializable(typeof(DerivedNoIsRef5))]
    [DataContractSerializable(typeof(DerivedNoIsRefWithIsRefTrue6))]
    [DataContractSerializable(typeof(DerivedWithIsRefFalse))]
    [DataContractSerializable(typeof(DerivedWithIsRefFalse2))]
    [DataContractSerializable(typeof(DerivedWithIsRefFalse3))]
    [DataContractSerializable(typeof(DerivedWithIsRefFalse4))]
    [DataContractSerializable(typeof(DerivedWithIsRefFalse5))]
    [DataContractSerializable(typeof(DerivedWithIsRefFalseExplicit))]
    [DataContractSerializable(typeof(DerivedWithIsRefTrue6))]
    [DataContractSerializable(typeof(DerivedWithIsRefTrueExplicit))]
    [DataContractSerializable(typeof(DerivedWithIsRefTrueExplicit2))]
    [DataContractSerializable(typeof(TestInheritance))]
    [DataContractSerializable(typeof(TestInheritance2))]
    [DataContractSerializable(typeof(TestInheritance3))]
    [DataContractSerializable(typeof(TestInheritance4))]
    [DataContractSerializable(typeof(TestInheritance5))]
    [DataContractSerializable(typeof(TestInheritance6))]
    [DataContractSerializable(typeof(TestInheritance7))]
    [DataContractSerializable(typeof(TestInheritance8))]
    [DataContractSerializable(typeof(TestInheritance10))]
    [DataContractSerializable(typeof(TestInheritance11))]
    [DataContractSerializable(typeof(TestInheritance12))]
    [DataContractSerializable(typeof(TestInheritance14))]
    [DataContractSerializable(typeof(TestInheritance16))]
    public partial class GeneratedCorpusContext : DataContractSerializerContext
    {
    }
}
