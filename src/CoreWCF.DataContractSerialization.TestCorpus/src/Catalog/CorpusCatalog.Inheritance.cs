// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SerializationTestTypes;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    /// <summary>
    /// Registers the contract types imported from dotnet/runtime's
    /// SerializationTestTypes/InheritanceCases.cs and InheritanceObjectRef.cs.
    /// </summary>
    /// <remarks>
    /// The value here is the interaction between <c>IsReference</c> and inheritance. Whether a
    /// derived contract emits <c>z:Id</c>/<c>z:Ref</c> depends on the setting at every level of the
    /// hierarchy, and the rules are not obvious - which makes this the densest set of behaviours a
    /// hand-written serializer is likely to get wrong.
    /// </remarks>
    public static partial class CorpusCatalog
    {
        static partial void RegisterInheritance(CorpusBuilder builder)
        {
            // IsReference = true at the root, then five levels that say nothing, then one that
            // turns it back on explicitly.
            builder.Add<BaseWithIsRefTrue>("init", () => new BaseWithIsRefTrue(true)).WithTags("upstream", "isreference");
            builder.Add<DerivedNoIsRef>("init", () => new DerivedNoIsRef(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedNoIsRef2>("init", () => new DerivedNoIsRef2(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedNoIsRef3>("init", () => new DerivedNoIsRef3(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedNoIsRef4>("init", () => new DerivedNoIsRef4(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedNoIsRef5>("init", () => new DerivedNoIsRef5(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedNoIsRefWithIsRefTrue6>("init", () => new DerivedNoIsRefWithIsRefTrue6(true)).WithTags("upstream", "isreference", "inheritance");

            // The same depth, but every level restates the contract without IsReference.
            builder.Add<DerivedWithIsRefFalse>("init", () => new DerivedWithIsRefFalse(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedWithIsRefFalse2>("init", () => new DerivedWithIsRefFalse2(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedWithIsRefFalse3>("init", () => new DerivedWithIsRefFalse3(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedWithIsRefFalse4>("init", () => new DerivedWithIsRefFalse4(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedWithIsRefFalse5>("init", () => new DerivedWithIsRefFalse5(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedWithIsRefTrue6>("init", () => new DerivedWithIsRefTrue6(true)).WithTags("upstream", "isreference", "inheritance");

            builder.Add<DerivedWithIsRefTrueExplicit>("init", () => new DerivedWithIsRefTrueExplicit(true)).WithTags("upstream", "isreference", "inheritance");
            builder.Add<DerivedWithIsRefTrueExplicit2>("init", () => new DerivedWithIsRefTrueExplicit2(true)).WithTags("upstream", "isreference", "inheritance");

            // The mirror image: a root without IsReference, derived types that turn it on and off.
            builder.Add<BaseNoIsRef>("init", () => new BaseNoIsRef(true)).WithTags("upstream", "isreference");
            builder.Add<DerivedWithIsRefFalseExplicit>("init", () => new DerivedWithIsRefFalseExplicit(true)).WithTags("upstream", "isreference", "inheritance");

            // An upstream negative case: IsReference = true under a base that leaves it false is an
            // invalid contract, and DataContractSerializer throws InvalidDataContractException
            // rather than producing XML. There is nothing to record as a golden fixture. The
            // generator will need to reject it too, but that is a diagnostic test, not this suite.
            builder.Skip<DerivedWithIsRefTrue>("Invalid by design: IsReference mismatch with its base throws InvalidDataContractException.");

            // Members declared as a base contract holding derived instances, resolved by [KnownType].
            builder.Add<TestInheritance>("init", () => new TestInheritance(true))
                   .WithKnownTypes(typeof(DerivedDC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance2>("init", () => new TestInheritance2(true))
                   .WithKnownTypes(typeof(DerivedDC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance3>("init", () => new TestInheritance3(true))
                   .WithKnownTypes(typeof(DerivedDC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance4>("init", () => new TestInheritance4(true))
                   .WithKnownTypes(typeof(DerivedDC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance5>("init", () => new TestInheritance5(true))
                   .WithKnownTypes(typeof(DerivedDC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance6>("init", () => new TestInheritance6(true))
                   .WithKnownTypes(typeof(DerivedDC), typeof(Derived2DC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance7>("init", () => new TestInheritance7(true))
                   .WithKnownTypes(typeof(Derived2DC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance8>("init", () => new TestInheritance8(true))
                   .WithKnownTypes(typeof(Derived2DC)).WithTags("upstream", "knowntype", "inheritance");
            builder.Add<TestInheritance10>("init", () => new TestInheritance10(true)).WithTags("upstream", "inheritance");
            builder.Add<TestInheritance11>("init", () => new TestInheritance11(true)).WithTags("upstream", "inheritance");
            builder.Add<TestInheritance12>("init", () => new TestInheritance12(true)).WithTags("upstream", "inheritance");
            builder.Add<TestInheritance14>("init", () => new TestInheritance14(true)).WithTags("upstream", "inheritance");
            builder.Add<TestInheritance16>("init", () => new TestInheritance16(true)).WithTags("upstream", "inheritance");

            // The plain data-contract hierarchy the TestInheritance cases point at.
            builder.Add<BaseDC>("init", () => new BaseDC(true)).WithTags("upstream", "inheritance");
            builder.Add<DerivedDC>("init", () => new DerivedDC(true)).WithTags("upstream", "inheritance");
            builder.Add<Derived2DC>("init", () => new Derived2DC(true)).WithTags("upstream", "inheritance");
            builder.Add<BaseDCNoIsRef>("default", () => new BaseDCNoIsRef()).WithTags("upstream", "inheritance");

            // Reference preservation across two members pointing at one instance.
            builder.Add<SimpleDC>("init", () => new SimpleDC(true)).WithTags("upstream", "isreference");
            builder.Add<SimpleDCWithRef>("init", () => new SimpleDCWithRef(true)).WithTags("upstream", "isreference", "shared-instance");

            // Out of scope for v1: [Serializable] without [DataContract] takes the formatter-based
            // path, which the generator does not target. These types are the base or the known type
            // of the cases below, so the cases go with them.
            builder.Skip<BaseSerializable>("[Serializable] contract; the formatter-based path is out of scope for v1.");
            builder.Skip<DerivedDCIsRefBaseSerializable>("Derives from the [Serializable] BaseSerializable; out of scope for v1.");
            builder.Skip<DerivedDCBaseSerializable>("Derives from the [Serializable] BaseSerializable; out of scope for v1.");
            builder.Skip<Derived2Derived2Serializable>("Derives from the [Serializable]-only Derived2Serializable; out of scope for v1.");
            builder.Skip<Derived3Derived2Serializable>("Derives from the [Serializable]-only Derived2Serializable; out of scope for v1.");
            builder.Skip<Derived4Derived2Serializable>("Derives from the [Serializable]-only Derived2Serializable; out of scope for v1.");
            builder.Skip<TestInheritance9>("Known types are [Serializable]; the formatter-based path is out of scope for v1.");
            builder.Skip<TestInheritance91>("Known types are [Serializable]; the formatter-based path is out of scope for v1.");
        }
    }
}
