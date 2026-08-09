// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.DataContractSerialization.TestCorpus.Sanity;

namespace CoreWCF.DataContractSerialization.TestCorpus
{
    public static partial class CorpusCatalog
    {
        static partial void RegisterSanity(CorpusBuilder builder)
        {
            builder.Add<SanityPrimitives>("populated", SanityPrimitives.Populated)
                   .WithTags("primitives", "fields", "floating-point");

            builder.Add<SanityPrimitives>("default", () => new SanityPrimitives())
                   .WithTags("primitives", "defaults");

            builder.Add<SanityMemberAttributes>("populated", SanityMemberAttributes.Populated)
                   .WithTags("member-order", "emit-default", "renaming");

            builder.Add<SanityCustomNaming>("populated", SanityCustomNaming.Populated)
                   .WithTags("contract-naming", "namespaces");

            builder.Add<SanityDerived>("populated", SanityDerived.Populated)
                   .WithTags("inheritance");

            // Declared as the base type but holding a derived instance: this is the i:type case,
            // the only form of polymorphism in scope for v1.
            builder.Add<SanityKnownTypeHolder>("derived-instance", SanityKnownTypeHolder.Populated)
                   .WithKnownTypes(typeof(SanityDerived))
                   .WithTags("knowntype", "polymorphism");

            builder.Add<SanityCollections>("populated", SanityCollections.Populated)
                   .WithTags("collections");

            builder.Add<SanityPrimitiveArrays>("populated", SanityPrimitiveArrays.Populated)
                   .WithTags("collections", "primitives");

#if !NETFRAMEWORK
            // DateOnly and TimeOnly do not exist on .NET Framework, so the type is guarded and this
            // registration carries the same condition.
            builder.Add<SanityDateAndTimeOnly>("populated", SanityDateAndTimeOnly.Populated)
                   .WithTags("primitives", "runtime-specific");
#endif

            builder.Add<SanityUriAndOffset>("populated", SanityUriAndOffset.Populated)
                   .WithTags("primitives", "namespaces");

            builder.Add<SanityBoxedPrimitives>("populated", SanityBoxedPrimitives.Populated)
                   .WithKnownTypes(typeof(SanityNestedNamespace))
                   .WithTags("polymorphism", "primitives");

            builder.Add<SanityEnums>("populated", SanityEnums.Populated)
                   .WithTags("enums");

            builder.Add<SanityEnumCollections>("populated", SanityEnumCollections.Populated)
                   .WithTags("enums", "collections");

            builder.Add<SanityDictionaries>("populated", SanityDictionaries.Populated)
                   .WithTags("collections", "nil");

            builder.Add<SanityUntypedCollections>("populated", SanityUntypedCollections.Populated)
                   .WithTags("collections", "polymorphism", "nil");

            builder.Add<SanityQualifiedNames>("populated", SanityQualifiedNames.Populated)
                   .WithTags("primitives", "namespaces");

            builder.Add<SanityNullable>("populated", SanityNullable.Populated)
                   .WithTags("nil", "nullable");

            builder.Add<SanityReferenceNode>("cycle", SanityReferenceNode.Populated)
                   .WithTags("isreference", "cycle");

            // Registered indirectly: SanityBase is only ever serialized through SanityKnownTypeHolder,
            // and SanityNestedNamespace through SanityCustomNaming / SanityNullable. Both are covered
            // as member types rather than as roots.
            // Registered as roots in their own right, because a root has no member to carry an
            // i:type and so decides for itself: the declared type writes none, a derived instance
            // writes one.
            builder.Add<SanityBase>("base-instance", () => new SanityBase { BaseMember = "plain", BaseOrdinal = 7 })
                   .WithKnownTypes(typeof(SanityDerived), typeof(SanityFurtherDerived))
                   .WithTags("knowntype", "polymorphism");

            builder.Add<SanityBase>("derived-instance", () => SanityFurtherDerived.Populated())
                   .WithKnownTypes(typeof(SanityDerived), typeof(SanityFurtherDerived))
                   .WithTags("knowntype", "polymorphism");

            builder.Add<SanityPolymorphic>("populated", SanityPolymorphic.Populated)
                   .WithKnownTypes(typeof(SanityDerived), typeof(SanityFurtherDerived))
                   .WithTags("knowntype", "polymorphism", "inheritance");

            builder.Skip<SanityFurtherDerived>("Covered through SanityBase and SanityPolymorphic.");
            builder.Skip<SanityNestedNamespace>("Covered as a member type of SanityCustomNaming and SanityNullable.");
            builder.Skip<SanityRenamedEnum>("Enum contract covered as a member of SanityEnums.");
        }
    }
}
