// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CoreWCF.DataContractSerialization.Generator.Tests
{
    public class DataContractSerializerGeneratorTests
    {
        /// <summary>Wraps a snippet in the usings and namespace every test needs.</summary>
        private static string Source(string body) => @"
using System.Runtime.Serialization;
using CoreWCF.DataContractSerialization;

namespace App
{
" + body + @"
}
";

        private static void AssertCompiles(GeneratorResult result) =>
            Assert.True(!result.Errors.Any(), "Generated code did not compile:" + Environment.NewLine + result.ErrorReport);

        private const string OrderContract = @"
    [DataContract]
    public class Order
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name;
    }
";

        [Fact]
        public void ContextWithoutAnySerializableAttribute_IsNotDiscovered()
        {
            // The attribute is the opt-in. A context carrying none is not a context the generator
            // knows about, so it emits nothing and the class keeps the base implementation, which
            // returns null and sends CoreWCF to the reflection-based serializer.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            Assert.Empty(result.GeneratedSources);
            Assert.Empty(result.GeneratorDiagnostics);
        }

        [Fact]
        public void FlatContract_EmitsASerializerAndCompiles()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContractSerializable(typeof(Order))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Empty(result.GeneratorDiagnostics);
            Assert.Contains("if (type == typeof(global::App.Order))", result.SingleSource);
            // A property and a field must both be picked up.
            Assert.Contains("writer.WriteValue(value.Id);", result.SingleSource);
            Assert.Contains("writer.WriteValue(value.Name);", result.SingleSource);
        }

        [Fact]
        public void Members_AreOrderedByOrderThenOrdinalName()
        {
            // Unspecified Order is -1 and cannot be written explicitly, so unordered members precede
            // every ordered one - including Order = 0. See ClassDataContract.DataMemberComparer.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Ordered
    {
        [DataMember(Order = 0)] public int ExplicitZero { get; set; }
        [DataMember] public int Zebra { get; set; }
        [DataMember] public int Apple { get; set; }
        [DataMember(Order = 5)] public int Later { get; set; }
    }

    [DataContractSerializable(typeof(Ordered))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            string emitted = result.SingleSource;

            int apple = emitted.IndexOf("\"Apple\"", StringComparison.Ordinal);
            int zebra = emitted.IndexOf("\"Zebra\"", StringComparison.Ordinal);
            int explicitZero = emitted.IndexOf("\"ExplicitZero\"", StringComparison.Ordinal);
            int later = emitted.IndexOf("\"Later\"", StringComparison.Ordinal);

            Assert.True(apple > 0 && zebra > 0 && explicitZero > 0 && later > 0, "All four members should be emitted.");
            Assert.True(apple < zebra, "Unordered members sort ordinally by name.");
            Assert.True(zebra < explicitZero, "Unordered members precede Order = 0.");
            Assert.True(explicitZero < later, "Ordered members sort by Order.");
        }

        [Fact]
        public void RenamedContractAndMember_UseTheirWireNames()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(Name = ""RenamedContract"", Namespace = ""http://example/ns"")]
    public class Renamed
    {
        [DataMember(Name = ""OnTheWire"")] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(Renamed))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("\"OnTheWire\"", result.SingleSource);
            Assert.Contains("\"http://example/ns\"", result.SingleSource);
        }

        [Fact]
        public void ContractWithoutExplicitNamespace_GetsTheDefaultDerivedFromItsClrNamespace()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContractSerializable(typeof(Order))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("http://schemas.datacontract.org/2004/07/App", result.SingleSource);
        }

        [Fact]
        public void EmitDefaultValueFalse_OmitsTheMember()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Sparse
    {
        [DataMember(EmitDefaultValue = false)] public int Maybe { get; set; }
    }

    [DataContractSerializable(typeof(Sparse))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("// omitted", result.SingleSource);
        }

        [Fact]
        public void EmitDefaultValueFalseOnARequiredMember_Throws()
        {
            // DataContractSerializer treats this as an error rather than an omission. See
            // ReflectionXmlFormatWriter.ReflectionWriteMembers.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Contradictory
    {
        [DataMember(EmitDefaultValue = false, IsRequired = true)] public int Required { get; set; }
    }

    [DataContractSerializable(typeof(Contradictory))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("SerializationException", result.SingleSource);
        }

        [Fact]
        public void CollectionMember_WritesItemsInTheCollectionNamespace()
        {
            // Items are named after their XSD type, not the CLR type - and several differ. The
            // names are pinned by the SanityPrimitiveArrays fixture, produced by the real
            // serializer.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class WithCollections
    {
        [DataMember] public int[] Numbers { get; set; }
        [DataMember] public System.Collections.Generic.List<string> Words { get; set; }
        [DataMember] public sbyte[] Signed { get; set; }
    }

    [DataContractSerializable(typeof(WithCollections))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteXmlnsAttribute(null, \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.Contains("writer.WriteStartElement(\"int\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.Contains("writer.WriteStartElement(\"string\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            // sbyte is "byte" on the wire, and byte is "unsignedByte" - the opposite of the guess.
            Assert.Contains("writer.WriteStartElement(\"byte\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
        }

        [Fact]
        public void ByteArrayMember_IsAPrimitiveNotACollection()
        {
            // byte[] is written as base64 in the containing contract's namespace, with no child
            // namespace declaration and no per-item elements.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class WithBytes
    {
        [DataMember] public byte[] Payload { get; set; }
    }

    [DataContractSerializable(typeof(WithBytes))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteBase64(value.Payload, 0, value.Payload.Length);", result.SingleSource);
            Assert.DoesNotContain("Serialization/Arrays", result.SingleSource);
        }

        [Fact]
        public void UnsupportedMemberType_LeavesTheContractToReflectionAndSaysSo()
        {
            // Falling back stays a correct outcome - no serializer is emitted and GetSerializer
            // returns null, so CoreWCF uses the reflection-based one and nothing breaks. What
            // changed is that it is no longer silent: under Native AOT the fallback is the broken
            // path, so a clean build that throws at run time is the worst of both.
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContract]
    public class HasCollection
    {
        [DataMember] public System.Collections.Generic.Dictionary<string, Order> Values { get; set; }
    }

    [DataContractSerializable(typeof(HasCollection))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("unsupported key or value type", result.SingleSource);
            Assert.DoesNotContain("if (type == typeof(global::App.HasCollection))", result.SingleSource);

            Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics);
            Assert.Equal("COREWCF_0403", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);

            string message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("App.HasCollection", message);

            // The reason has to name the member, or it is not something anyone can act on.
            Assert.Contains("'Values'", message);
        }

        [Fact]
        public void ContractThatCanBeWrittenButNotRead_WarnsSeparately()
        {
            // Half a fallback, and worth its own id: a service that only returns this contract is
            // unaffected, while one that accepts it is not.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class NoDefaultConstructor
    {
        public NoDefaultConstructor(int seed)
        {
            Value = seed;
        }

        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(NoDefaultConstructor))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // Still written by generated code - only the read half falls back.
            Assert.Contains("if (type == typeof(global::App.NoDefaultConstructor))", result.SingleSource);
            Assert.DoesNotContain("CanReadObject => true", result.SingleSource);

            Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics);
            Assert.Equal("COREWCF_0404", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains(
                "no accessible parameterless constructor",
                diagnostic.GetMessage(CultureInfo.InvariantCulture));
        }

        [Fact]
        public void FallbackOfANestedContract_IsReportedOnceOnTheTypeThatWasListed()
        {
            // The container is unsupported because its member is, so reporting both would bury the
            // one line the user can act on. The reason text carries the cause instead.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Inner
    {
        [DataMember] private int _hidden;
    }

    [DataContract]
    public class Outer
    {
        [DataMember] public Inner Value { get; set; }
    }

    [DataContractSerializable(typeof(Outer))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            Diagnostic diagnostic = Assert.Single(result.GeneratorDiagnostics);
            Assert.Equal("COREWCF_0403", diagnostic.Id);

            string message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            Assert.Contains("App.Outer", message);
            Assert.Contains("App.Inner", message);
        }

        [Fact]
        public void DerivedContract_WritesBaseMembersFirst()
        {
            // Ordering is per contract and base-first, not a single merged sort across the
            // hierarchy - so a base member named Zulu still precedes a derived member named Alpha.
            // Mirrors the recursion in ReflectionXmlClassWriter.ReflectionWriteMembers.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class BaseContract
    {
        [DataMember] public int Zulu { get; set; }
    }

    [DataContract]
    public class DerivedContract : BaseContract
    {
        [DataMember] public int Alpha { get; set; }
    }

    [DataContractSerializable(typeof(DerivedContract))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("if (type == typeof(global::App.DerivedContract))", result.SingleSource);

            // The derived content writer delegates to the base one before writing its own members.
            int delegation = result.SingleSource.IndexOf("__WriteContent", StringComparison.Ordinal);
            int alpha = result.SingleSource.IndexOf("\"Alpha\"", StringComparison.Ordinal);
            int zulu = result.SingleSource.IndexOf("\"Zulu\"", StringComparison.Ordinal);
            Assert.True(delegation > 0 && alpha > 0 && zulu > 0, "Both contracts should be emitted.");
        }

        [Fact]
        public void DerivedContract_ReadsAFlattenedMemberListBaseFirst()
        {
            // The writer recurses, one call per level. The reader cannot: it is one loop over one
            // monotonically advancing index, and a per-level loop would let the base's loop swallow
            // the derived members as elements it does not recognise. Upstream draws the same
            // distinction - ReflectionGetMembers flattens the chain base-first.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(Namespace = ""http://base"")]
    public class BaseContract
    {
        [DataMember] public int Zulu { get; set; }
    }

    [DataContract(Namespace = ""http://derived"")]
    public class DerivedContract : BaseContract
    {
        [DataMember] public int Alpha { get; set; }
    }

    [DataContractSerializable(typeof(DerivedContract))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // The derived contract's reader is the only one with a second member to match.
            string derivedReader = result.SingleSource
                .Split(new[] { "private static void __ReadContent" }, StringSplitOptions.None)
                .Single(chunk => chunk.Contains("__matched < 1"));

            Assert.True(
                derivedReader.IndexOf("\"Zulu\"", StringComparison.Ordinal)
                    < derivedReader.IndexOf("\"Alpha\"", StringComparison.Ordinal),
                "The inherited member should be matched before the derived one.");

            // And it keeps the namespace of the contract that declares it, which is how
            // ClassDataContract builds MemberNamespaces - copying the base's entries before
            // appending its own.
            Assert.Contains("reader.NamespaceURI == \"http://base\"", derivedReader);
            Assert.Contains("reader.NamespaceURI == \"http://derived\"", derivedReader);
        }

        [Fact]
        public void EnumMember_ReadsItsNameTableBack()
        {
            // A flags enum and a plain one are parsed differently - a flags list is space separated
            // and may be empty, a single name may not be - so the table lookup is told which it is.
            // Mirrors EnumDataContract.ReadEnumValue.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    public enum Plain { None = 0, One = 1 }

    [System.Flags]
    public enum Marks : ulong { None = 0, Alpha = 1, Beta = 2 }

    [DataContract]
    public class Holder
    {
        [DataMember] public Plain Single { get; set; }
        [DataMember] public Marks Several { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            Assert.Contains(", false, \"global::App.Plain\")", result.SingleSource);
            Assert.Contains(", true, \"global::App.Marks\")", result.SingleSource);

            // A ulong-backed enum reinterprets its bits rather than converting them, the same way
            // round the writer does, or every value past long.MaxValue comes back a different
            // number.
            Assert.Contains("(global::App.Marks)unchecked((ulong)ReadEnum(", result.SingleSource);
            Assert.Contains("(global::App.Plain)ReadEnum(", result.SingleSource);
        }

        [Fact]
        public void DateOnlyAndTimeOnlyMembers_ReadTheFormatTheRuntimeWrote()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public System.DateOnly Day { get; set; }
        [DataMember] public System.TimeOnly Moment { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // The same run-time test the writer uses, because the format is a property of the
            // runtime rather than of the contract: before .NET 10 the serializer writes a contract
            // with no members, so the element carries no value and default is what the document
            // says.
            Assert.Contains("if (!__DateOnlyIsPrimitive)", result.SingleSource);
            Assert.Contains("global::System.DateOnly.ParseExact(", result.SingleSource);

            // Deliberately not TimeOnly.ParseExact. ReadElementContentAsTimeOnly goes through
            // XmlConvert.ToDateTimeOffset, which accepts a Z or an offset that "HH:mm:ss.FFFFFFF"
            // would reject; the ParseTimeOnly helper next to it upstream is never called.
            Assert.Contains("global::System.TimeOnly.FromTimeSpan(", result.SingleSource);
            Assert.Contains("ToDateTimeOffset(text).TimeOfDay", result.SingleSource);
            Assert.DoesNotContain("global::System.TimeOnly.ParseExact(", result.SingleSource);
        }

        [Fact]
        public void DateTimeOffsetMember_ReadsBackThroughTheAdapterContract()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public System.DateTimeOffset When { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // The two members of the adapter contract, in the namespace neither type mentions.
            Assert.Contains("reader.LocalName == \"DateTime\"", result.SingleSource);
            Assert.Contains("reader.LocalName == \"OffsetMinutes\"", result.SingleSource);
            Assert.Contains(
                "reader.NamespaceURI == \"http://schemas.datacontract.org/2004/07/System\"",
                result.SingleSource);

            // Mirrors DateTimeOffsetAdapter.GetDateTimeOffset: an Unspecified DateTime is paired
            // with the offset, anything else is converted to it. The writer recorded UtcDateTime and
            // the offset separately, so treating both the same way would shift every value that
            // carries an offset by that offset.
            Assert.Contains("__dateTime.Kind == global::System.DateTimeKind.Unspecified", result.SingleSource);
            Assert.Contains("new global::System.DateTimeOffset(__dateTime).ToOffset(__offset)", result.SingleSource);
        }

        [Fact]
        public void XmlQualifiedNameMember_ResolvesItsPrefixBeforeTheElementCloses()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public System.Xml.XmlQualifiedName Name { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // ReadElementContentAsString would consume the end tag and pop the scope that declares
            // the prefix, leaving nothing to resolve against - so the read is split the way
            // XmlReaderDelegator.ReadElementContentAsQName splits it.
            Assert.Contains("reader.ReadContentAsString();", result.SingleSource);
            Assert.Contains("reader.LookupNamespace(__prefix);", result.SingleSource);

            // The writer emits no content at all for the empty name, so an empty element is how it
            // comes back.
            Assert.Contains("return global::System.Xml.XmlQualifiedName.Empty;", result.SingleSource);
        }

        [Fact]
        public void NullXmlQualifiedNameMember_DoesNotGetItsOwnPrefix()
        {
            // The prefix belongs to the path that writes a value; a null member is written by
            // WriteNull, which opens the element with whatever prefix is already bound. Both halves
            // are pinned by fixtures - SanityQualifiedNames records the null case, AllTypes the
            // value case - and getting it wrong costs a byte-exact match on one of them.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public System.Xml.XmlQualifiedName Name { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            int nullBranch = result.SingleSource.IndexOf("if (value.Name == null)", StringComparison.Ordinal);
            Assert.True(nullBranch > 0, "The member element should be opened from a null test.");

            string opening = result.SingleSource.Substring(nullBranch, 400);
            int plain = opening.IndexOf("writer.WriteStartElement(\"Name\"", StringComparison.Ordinal);
            int prefixed = opening.IndexOf("writer.WriteStartElement(\"q\", \"Name\"", StringComparison.Ordinal);

            Assert.True(plain > 0 && prefixed > plain, "The unprefixed element belongs to the null branch.");
        }

        [Fact]
        public void DictionaryMember_ReadsEachEntryByKeyAndValue()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public System.Collections.Generic.Dictionary<int, string> Map { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // The part names and their namespace come from the same spec the writer proved against
            // a fixture, so the reader looks for exactly what was written.
            Assert.Contains("reader.LocalName == \"Key\"", result.SingleSource);
            Assert.Contains("reader.LocalName == \"Value\"", result.SingleSource);
            Assert.Contains(
                "reader.NamespaceURI == \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\"",
                result.SingleSource);
            Assert.Contains("__pairs.Add(__key, __value);", result.SingleSource);
        }

        [Fact]
        public void BaseContractNamingItsDerivedTypes_DispatchesOnTheXsiType()
        {
            // Reading a document through the base's reader alone would drop every member the derived
            // contract adds and report success, so the name is resolved instead. Both directions are
            // here: the root announces the runtime contract on the way out and resolves it on the
            // way back in.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    [KnownType(typeof(DerivedContract))]
    public class BaseContract
    {
        [DataMember] public int Zulu { get; set; }
    }

    [DataContract]
    public class DerivedContract : BaseContract
    {
        [DataMember] public int Alpha { get; set; }
    }

    [DataContractSerializable(typeof(BaseContract))]
    [DataContractSerializable(typeof(DerivedContract))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // Both serializers read now, because the base can tell the two apart.
            Assert.Equal(
                2,
                result.SingleSource.Split(new[] { "CanReadObject => true" }, StringSplitOptions.None).Length - 1);

            // Out: the root branches on the runtime type and announces anything but the declared one.
            Assert.Contains("global::System.Type __runtimeType = graph.GetType();", result.SingleSource);
            Assert.Contains("__runtimeType == typeof(global::App.DerivedContract)", result.SingleSource);

            // In: the i:type is resolved by name, and a name that resolves to nothing throws rather
            // than falling back to the declared contract.
            Assert.Contains("ReadXsiType(reader, out string __typeName, out string __typeNamespace);", result.SingleSource);
            Assert.Contains("__typeName == \"DerivedContract\"", result.SingleSource);
            Assert.Contains("The deserializer has no knowledge of any type that maps to this name.", result.SingleSource);
        }

        [Fact]
        public void IsReferenceContract_ResolvesARefAndRecordsAnIdBeforeReadingMembers()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(IsReference = true)]
    public class Node
    {
        [DataMember] public Node Next { get; set; }
    }

    [DataContractSerializable(typeof(Node))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // A z:Ref carries nothing but the attribute, so the element is skipped whole.
            Assert.Contains(
                "string __ref = reader.GetAttribute(\"Ref\", \"http://schemas.microsoft.com/2003/10/Serialization/\");",
                result.SingleSource);
            Assert.Contains("scope.Get(__ref);", result.SingleSource);

            // The id is recorded before the members are read, which is the whole of what makes a
            // cycle work - an instance inside its own graph is referred to while it is still being
            // filled in. Recording it after would turn that reference into a lookup that fails.
            int construct = result.SingleSource.IndexOf(
                "global::App.Node __typed = new global::App.Node();",
                StringComparison.Ordinal);
            Assert.True(construct > 0, "The instance should be constructed before anything else.");

            int record = result.SingleSource.IndexOf(
                "scope.Add(reader.GetAttribute(\"Id\"", construct, StringComparison.Ordinal);
            int readMembers = result.SingleSource.IndexOf("__ReadContent0(reader", construct, StringComparison.Ordinal);

            Assert.True(record > construct, "The id is recorded once the instance exists.");
            Assert.True(readMembers > record, "The id is recorded before any member is read.");
        }

        [Fact]
        public void JaggedArrayMember_ReadsEachInnerArrayAsACollectionOfItsOwn()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public int[][] Rows { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // The ArrayOf element is the inner collection's own element, so the read is the same
            // loop one level down rather than a special case.
            Assert.Contains("__inner", result.SingleSource);
            Assert.Contains("__item = __inner.ToArray();", result.SingleSource);
            Assert.Contains("__items.Add(__item);", result.SingleSource);
            Assert.Contains("value.Rows = __items.ToArray();", result.SingleSource);
        }

        [Fact]
        public void ArrayListMember_ReadsIntoAnArrayListRatherThanAList()
        {
            // The one container a read cannot infer from its items: everything else accumulates into
            // a List<T> and either assigns it or calls ToArray on it, and an ArrayList is neither.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public System.Collections.ArrayList Items { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            Assert.Contains(
                "global::System.Collections.ArrayList __items = new global::System.Collections.ArrayList();",
                result.SingleSource);

            // Its items are untyped, so each announces its own type on the way out and resolves it
            // on the way back in.
            Assert.Contains("__item = ReadAnyType(reader);", result.SingleSource);
            Assert.Contains("value.Items = __items;", result.SingleSource);
        }

        [Fact]
        public void ObjectMember_ReadsEachXsdNameBackToItsPrimitive()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public object Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // The swap survives the inverse. "byte" is sbyte and "unsignedByte" is byte, which is
            // the pair most likely to be quietly "corrected" into agreeing with the CLR names.
            Assert.Contains(
                "if (name == \"byte\" && ns == \"http://www.w3.org/2001/XMLSchema\")",
                result.SingleSource);
            Assert.Contains(
                "if (name == \"unsignedByte\" && ns == \"http://www.w3.org/2001/XMLSchema\")",
                result.SingleSource);

            // And the three that XML Schema has no name for stay in the serialization namespace.
            Assert.Contains(
                "if (name == \"char\" && ns == \"http://schemas.microsoft.com/2003/10/Serialization/\")",
                result.SingleSource);
            Assert.Contains(
                "if (name == \"duration\" && ns == \"http://schemas.microsoft.com/2003/10/Serialization/\")",
                result.SingleSource);

            // A bare object carries neither i:type nor content, so there is nothing to recover but
            // the fact that something was there.
            Assert.Contains("return new object();", result.SingleSource);
        }

        [Fact]
        public void ValueTypeMember_RejectsAPrimitiveItCannotHold()
        {
            // The declared type buys a check: a string named by an i:type here is refused with a
            // SerializationException rather than surfacing as an InvalidCastException out of
            // generated code.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public System.ValueType Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            Assert.Contains("if (!(__boxed is global::System.ValueType))", result.SingleSource);
            Assert.Contains("which this member cannot hold.", result.SingleSource);
        }

        [Fact]
        public void PolymorphicMember_ReadsEachCandidateAndDefaultsToTheDeclaredContract()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    [KnownType(typeof(DerivedContract))]
    public class BaseContract
    {
        [DataMember] public int Zulu { get; set; }
    }

    [DataContract]
    public class DerivedContract : BaseContract
    {
        [DataMember] public int Alpha { get; set; }
    }

    [DataContract]
    public class Holder
    {
        [DataMember] public BaseContract Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // No i:type means the declared contract - the writer omits it for exactly that case - so
            // the absent name and the declared name share one branch.
            Assert.Contains("if (__typeName == null", result.SingleSource);
            Assert.Contains("|| (__typeName == \"BaseContract\"", result.SingleSource);
            Assert.Contains("else if (__typeName == \"DerivedContract\"", result.SingleSource);
        }

        [Fact]
        public void ContractWithNonContractBaseClass_LeavesTheContractToReflection()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    public class PlainBase
    {
        public int Ignored { get; set; }
    }

    [DataContract]
    public class DerivedFromPlain : PlainBase
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(DerivedFromPlain))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("is not a data contract", result.SingleSource);
        }

        [Fact]
        public void NestedContractMember_IsWrittenInlineWithItsOwnNamespaceDeclared()
        {
            // A contract-typed member has no second wrapping element: the nested contract's members
            // are written straight inside the member element. Its namespace is declared there rather
            // than at the root, which is what produces xmlns:b on the member. Mirrors
            // ClassDataContract.GetChildNamespaceToDeclare.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(Namespace = ""http://outer/ns"")]
    public class Outer
    {
        [DataMember] public Inner Child { get; set; }
    }

    [DataContract(Namespace = ""http://inner/ns"")]
    public class Inner
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(Outer))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteXmlnsAttribute(null, \"http://inner/ns\");", result.SingleSource);
            // Inner is pulled in transitively even though only Outer was declared.
            Assert.Contains("\"Value\"", result.SingleSource);
        }

        [Fact]
        public void NestedContractInTheSameNamespace_DeclaresNothingExtra()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(Namespace = ""http://shared/ns"")]
    public class Outer
    {
        [DataMember] public Inner Child { get; set; }
    }

    [DataContract(Namespace = ""http://shared/ns"")]
    public class Inner
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(Outer))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.DoesNotContain("writer.WriteXmlnsAttribute(null, \"http://shared/ns\");\r\n            writer.WriteXmlnsAttribute", result.SingleSource);
        }

        [Fact]
        public void UnsupportedNestedContract_MakesTheContainerUnsupportedToo()
        {
            // A container is only writable if everything it writes is. Emitting a serializer that
            // silently skipped an unwritable member would produce wrong XML rather than falling back.
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContract]
    public class Container
    {
        [DataMember] public Problem Child { get; set; }
    }

    [DataContract]
    public class Problem
    {
        [DataMember] public System.Collections.Generic.Dictionary<string, Order> Values { get; set; }
    }

    [DataContractSerializable(typeof(Container))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.DoesNotContain("if (type == typeof(global::App.Container))", result.SingleSource);
            Assert.Contains("unsupported contract type", result.SingleSource);
        }

        [Fact]
        public void InheritedIsReference_IsDetectedThroughTheBaseChain()
        {
            // IsReference is inherited. A derived contract that says nothing still gets it, and
            // reading only its own attribute would miss that and emit output without z:Id.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(IsReference = true)]
    public class ReferencedBase
    {
        [DataMember] public int BaseValue { get; set; }
    }

    [DataContract]
    public class QuietDerived : ReferencedBase
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(QuietDerived))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("if (type == typeof(global::App.QuietDerived))", result.SingleSource);
            Assert.Contains("scope.WriteIdOrRef(writer, graph);", result.SingleSource);
        }

        [Fact]
        public void ContractWithoutIsReference_WritesNoId()
        {
            // The counterpart to the test above: a plain contract must not acquire a z:Id, which
            // would be a visible difference in every document it appears in.
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContractSerializable(typeof(Order))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("if (type == typeof(global::App.Order))", result.SingleSource);

            // The scope type is always emitted because every content writer takes one; what must
            // be absent is any call that would put an id on the wire.
            Assert.DoesNotContain("scope.WriteIdOrRef", result.SingleSource);
        }

        [Fact]
        public void IsReferenceMemberSeenTwice_WritesARefInsteadOfTheContent()
        {
            // The whole point of IsReference: the second sight of an instance is a z:Ref with no
            // content. Writing the members again would both duplicate data and, for a cycle,
            // recurse forever.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(IsReference = true)]
    public class Node
    {
        [DataMember] public Node Next { get; set; }
    }

    [DataContractSerializable(typeof(Node))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("if (!scope.WriteIdOrRef(writer, value.Next))", result.SingleSource);
        }

        [Fact]
        public void ContradictoryIsReference_LeavesTheContractToReflection()
        {
            // A derived contract cannot disagree with its base about IsReference:
            // DataContractSerializer throws InvalidDataContractException. Declining keeps that
            // throw, where emitting a serializer would silently accept an invalid contract.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class PlainBase
    {
        [DataMember] public int BaseValue { get; set; }
    }

    [DataContract(IsReference = true)]
    public class LoudDerived : PlainBase
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(LoudDerived))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("contradicts base contract PlainBase", result.SingleSource);
            Assert.DoesNotContain("if (type == typeof(global::App.LoudDerived))", result.SingleSource);
        }

        [Fact]
        public void NonPublicMember_LeavesTheContractToReflection()
        {
            // Generated code lives in the context's assembly and cannot reach a private member.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Hidden
    {
        [DataMember] private int _value;
    }

    [DataContractSerializable(typeof(Hidden))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            Assert.Contains("is not public", result.SingleSource);
        }

        [Fact]
        public void ContractInAnotherAssemblyWithAPrivateMember_LeavesTheContractToReflection()
        {
            // SerializationTestTypes.BaseDCNoIsRef has a single [DataMember] on a private field.
            // Compiled from metadata rather than source, that member may not be visible to the
            // generator at all - in which case it would happily emit a serializer that silently
            // drops it, producing XML that is wrong rather than absent. Whatever Roslyn surfaces,
            // the outcome must be a fallback, never a partial serializer.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContractSerializable(typeof(SerializationTestTypes.BaseDCNoIsRef))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.DoesNotContain(
                "if (type == typeof(global::SerializationTestTypes.BaseDCNoIsRef))",
                result.SingleSource);
        }

        [Fact]
        public void PolymorphicMember_WritesAnXsiTypeAndTheDerivedContractsMembers()
        {
            // A member declared as a base contract may hold a derived one. The serializer announces
            // that with i:type and writes the derived contract's members; writing the declared
            // type's members instead is well-formed, plausible and wrong.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    [KnownType(typeof(Derived))]
    public class Base
    {
        [DataMember] public int BaseValue { get; set; }
    }

    [DataContract]
    public class Derived : Base
    {
        [DataMember] public int DerivedValue { get; set; }
    }

    [DataContract]
    public class Holder
    {
        [DataMember] public Base Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);

            // Exact type equality, not a type pattern: a pattern would let the base branch swallow
            // a derived instance depending on the order the candidates happen to be in.
            Assert.Contains("__runtimeType == typeof(global::App.Derived)", result.SingleSource);
            Assert.Contains("__runtimeType == typeof(global::App.Base)", result.SingleSource);
            Assert.Contains("writer.WriteQualifiedName(\"Derived\"", result.SingleSource);

            // ...and not for the declared type, which is what the reader already assumes.
            Assert.DoesNotContain("writer.WriteQualifiedName(\"Base\"", result.SingleSource);
        }

        [Fact]
        public void KnownTypeOnTheMemberType_IsFound()
        {
            // The attribute may sit on either end - the contract holding the member, or the member's
            // own declared type. The serializer has both in scope while writing the member, so
            // reading only the holder would silently lose the other's types and emit the base
            // contract's members for a derived instance.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    [KnownType(typeof(Derived))]
    public class Base
    {
        [DataMember] public int BaseValue { get; set; }
    }

    [DataContract]
    public class Derived : Base
    {
        [DataMember] public int DerivedValue { get; set; }
    }

    [DataContract]
    public class Holder
    {
        [DataMember] public Base Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("__runtimeType == typeof(global::App.Derived)", result.SingleSource);

            // The root declares no [KnownType] of its own, but resolves one - so it must say so when
            // CoreWCF asks whether the operation's known types are covered.
            Assert.Contains("knownTypes[i] == typeof(global::App.Derived)", result.SingleSource);
        }

        [Fact]
        public void AbstractMemberTypeWithoutAKnownType_LeavesTheContractToReflection()
        {
            // Nothing can ever be an instance of the declared type, so every value this member holds
            // is one no [KnownType] names. Writing the abstract contract's members would be wrong.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public abstract class Shape
    {
        [DataMember] public int Sides { get; set; }
    }

    [DataContract]
    public class Holder
    {
        [DataMember] public Shape Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("declared as abstract contract Shape", result.SingleSource);
            Assert.DoesNotContain("if (type == typeof(global::App.Holder))", result.SingleSource);
        }

        [Fact]
        public void KnownTypeNamingAMethod_LeavesTheContractToReflection()
        {
            // The methodName overload returns its types at run time. Nothing here can evaluate it,
            // and assuming there are none would make the serializer reject instances the real one
            // accepts.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    [KnownType(""GetKnownTypes"")]
    public class Holder
    {
        [DataMember] public int Value { get; set; }

        public static System.Type[] GetKnownTypes() => new System.Type[0];
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("names a method", result.SingleSource);
            Assert.DoesNotContain("if (type == typeof(global::App.Holder))", result.SingleSource);
        }

        [Fact]
        public void ContractWithoutKnownTypes_CoversNoOperationKnownTypes()
        {
            // CoreWCF supplies known types from the operation description, which no attribute
            // reveals. A serializer compiled against none resolves none, so the honest answer is
            // false and the caller falls back.
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContractSerializable(typeof(Order))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("public override bool CoversKnownTypes", result.SingleSource);
            Assert.DoesNotContain("knownTypes[i] == typeof(", result.SingleSource);
        }

        [Fact]
        public void SerializableType_IsWrittenFromItsFields()
        {
            // A [Serializable] type has no [DataMember]s to read. Every instance field takes part,
            // properties never do, and [NonSerialized] opts a field out. See the else branch of
            // hasDataContract in ClassDataContract.ImportDataMembers.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [System.Serializable]
    public class Legacy
    {
        public string Kept;
        [System.NonSerialized] public string Dropped;
        public string AlsoKept { get; set; }
    }

    [DataContractSerializable(typeof(Legacy))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteStartElement(\"Kept\"", result.SingleSource);
            Assert.DoesNotContain("writer.WriteStartElement(\"Dropped\"", result.SingleSource);

            // A property is not a field, so it contributes nothing - and neither does its compiler
            // generated backing field, whose name is not a legal element name anyway.
            Assert.DoesNotContain("AlsoKept", result.SingleSource);
        }

        [Fact]
        public void TypeWithBothAttributes_IsWrittenAsADataContract()
        {
            // [DataContract] wins when a type carries both, so only the annotated members take part.
            // BaseSerializable in the corpus is exactly this shape.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [System.Serializable]
    [DataContract]
    public class Both
    {
        [DataMember] public string Annotated;
        public string Bare;
    }

    [DataContractSerializable(typeof(Both))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteStartElement(\"Annotated\"", result.SingleSource);
            Assert.DoesNotContain("writer.WriteStartElement(\"Bare\"", result.SingleSource);
        }

        [Fact]
        public void SerializableTypeImplementingISerializable_LeavesTheContractToReflection()
        {
            // ISerializable takes over serialization entirely - a different write algorithm, not a
            // different member list - so the fields are not what would go on the wire.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [System.Serializable]
    public class Custom : System.Runtime.Serialization.ISerializable
    {
        public string Value;

        public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
        }
    }

    [DataContractSerializable(typeof(Custom))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("COREWCF_0402", result.DiagnosticIds);
            Assert.DoesNotContain("if (type == typeof(global::App.Custom))", result.SingleSource);
        }

        [Fact]
        public void DateOnlyAndTimeOnlyMembers_DecideTheirFormatAtRunTime()
        {
            // The only members whose wire format is decided by the runtime rather than the contract.
            // Up to .NET 9 the serializer did not recognise them and wrote a memberless contract -
            // an empty element that drops the value - and .NET 10 writes them as primitives. A
            // net8.0 assembly run on .NET 10 produces the .NET 10 format, so the choice cannot be
            // made at compile time.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Schedule
    {
        [DataMember] public System.DateOnly Day { get; set; }
        [DataMember] public System.TimeOnly At { get; set; }
    }

    [DataContractSerializable(typeof(Schedule))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("global::System.Environment.Version.Major >= 10;", result.SingleSource);

            // Optional fractional digits, so trailing zeros and the dot are omitted rather than
            // padded - matching XmlWriterDelegator.
            Assert.Contains("value.ToString(\"yyyy-MM-dd\", global::System.Globalization.CultureInfo.InvariantCulture)", result.SingleSource);
            Assert.Contains("value.ToString(\"HH:mm:ss.FFFFFFF\", global::System.Globalization.CultureInfo.InvariantCulture)", result.SingleSource);

            // The System namespace is declared only where the serializer does not recognise them.
            Assert.Contains("if (!__DateOnlyIsPrimitive)", result.SingleSource);
        }

        [Fact]
        public void JaggedArrayMember_WrapsEachInnerArrayInAnArrayOfElement()
        {
            // Each outer item is an array in its own right, written as ArrayOf plus the XSD name of
            // the innermost type, holding the items themselves. byte[][] is deliberately not this
            // shape: byte[] is a primitive written as base64, so it stays a flat collection.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Jagged
    {
        [DataMember] public int[][] Numbers { get; set; }
        [DataMember] public byte[][] Blobs { get; set; }
    }

    [DataContractSerializable(typeof(Jagged))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteStartElement(\"ArrayOfint\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.Contains("foreach (var innerItem in item)", result.SingleSource);

            // byte[][] keeps its flat base64 items, with no ArrayOf wrapper.
            Assert.Contains("writer.WriteStartElement(\"base64Binary\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.DoesNotContain("ArrayOfbase64Binary", result.SingleSource);
        }

        [Fact]
        public void DictionaryMember_NamesEntriesAfterBothTypeArguments()
        {
            // An entry is named KeyValueOf followed by the XSD name of each argument, which is why
            // Dictionary<string, string> writes KeyValueOfstringstring and Dictionary<byte[], byte[]>
            // writes KeyValueOfbase64Binarybase64Binary. Both are pinned by fixtures.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Maps
    {
        [DataMember] public System.Collections.Generic.Dictionary<string, string> Names { get; set; }
        [DataMember] public System.Collections.Generic.Dictionary<byte[], byte[]> Blobs { get; set; }
    }

    [DataContractSerializable(typeof(Maps))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteStartElement(\"KeyValueOfstringstring\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.Contains("writer.WriteStartElement(\"KeyValueOfbase64Binarybase64Binary\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);

            // Key and Value are ordinary primitive writes in the same namespace.
            Assert.Contains("writer.WriteStartElement(\"Key\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.Contains("writer.WriteStartElement(\"Value\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
        }

        [Fact]
        public void ArrayListMember_WritesAnyTypeItems()
        {
            // ArrayList holds anything, so each item announces its own runtime type - the same shape
            // as an object member, once per item. Unlike a System.Array member, the namespace is
            // declared once on the member element, so the items carry a prefix rather than each
            // binding a default xmlns of its own.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Bag
    {
        [DataMember] public System.Collections.ArrayList Items { get; set; }
    }

    [DataContractSerializable(typeof(Bag))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteXmlnsAttribute(null, \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.Contains("writer.WriteStartElement(\"anyType\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
            Assert.Contains("WriteAnyType(writer, item);", result.SingleSource);
        }

        [Fact]
        public void DictionaryWithAContractValue_LeavesTheContractToReflection()
        {
            // Only built-in arguments are supported. A contract argument would contribute its own
            // contract name to the entry name and, if it were generic, a hash - neither of which is
            // worth guessing at.
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContract]
    public class Maps
    {
        [DataMember] public System.Collections.Generic.Dictionary<string, Order> Orders { get; set; }
    }

    [DataContractSerializable(typeof(Maps))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("has unsupported key or value type", result.SingleSource);
            Assert.DoesNotContain("if (type == typeof(global::App.Maps))", result.SingleSource);
        }

        [Fact]
        public void AContractBlockedOnSeveralThings_ReportsEveryReason()
        {
            // The coverage report used to record the first reason and stop, which made a wide
            // contract read like a single blocker when it was one of many - and did exactly that
            // once, for AllTypes.
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContract]
    public class Awkward
    {
        [DataMember] public System.Collections.Generic.List<Order> Orders { get; set; }
        [DataMember] public System.Collections.Generic.Dictionary<string, Order> Map { get; set; }
        [DataMember] private int _hidden;
    }

    [DataContractSerializable(typeof(Awkward))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("member 'Orders' has unsupported collection element type", result.SingleSource);
            Assert.Contains("member 'Map' has unsupported key or value type", result.SingleSource);
            Assert.Contains("member '_hidden' is not public", result.SingleSource);
        }

        [Fact]
        public void ByValueContractMember_IsGuardedAgainstCycles()
        {
            // A contract written by value can be cyclic, and the generated writer would recurse
            // until the stack ran out. DataContractSerializer throws past a depth of 512 instead,
            // and a StackOverflowException cannot be caught - so the generated path matches it.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Node
    {
        [DataMember] public Node Next { get; set; }
    }

    [DataContractSerializable(typeof(Node))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("scope.EnterByValue(value.Next);", result.SingleSource);
            Assert.Contains("scope.ExitByValue(value.Next);", result.SingleSource);
            Assert.Contains("private const int DepthToCheckCyclicReference = 512;", result.SingleSource);
        }

        [Fact]
        public void IsReferenceContractMember_IsNotGuarded()
        {
            // A reference-preserving contract cannot recurse forever: the second sight of an
            // instance is a z:Ref with no content, so the guard would be dead weight.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(IsReference = true)]
    public class Node
    {
        [DataMember] public Node Next { get; set; }
    }

    [DataContractSerializable(typeof(Node))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.DoesNotContain("scope.EnterByValue", result.SingleSource);
        }

        [Fact]
        public void XmlQualifiedNameMember_GetsItsOwnElementPrefix()
        {
            // The one member type whose element carries a prefix of its own rather than reusing
            // what the writer has bound, so a second prefix ends up on the contract's namespace
            // beside the one already in scope. Mirrors NeedsPrefix in ReflectionXmlFormatWriter,
            // which forces it for this type alone and only when the namespace is non-empty.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Named
    {
        [DataMember] public System.Xml.XmlQualifiedName Which { get; set; }
    }

    [DataContractSerializable(typeof(Named))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteStartElement(\"q\", \"Which\", \"http://schemas.datacontract.org/2004/07/App\");", result.SingleSource);

            // The empty name writes nothing at all, not an empty string.
            Assert.Contains("if (value != global::System.Xml.XmlQualifiedName.Empty)", result.SingleSource);
        }

        [Fact]
        public void ValueTypeMember_OffersOnlyValueTypeCandidates()
        {
            // A member declared as ValueType is the boxed switch over a narrower set. The filtering
            // is not cosmetic: casting a ValueType to string is a compile error, so an unfiltered
            // table would emit generated code that does not build.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Boxy
    {
        [DataMember] public System.ValueType Value { get; set; }
    }

    [DataContractSerializable(typeof(Boxy))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("__runtimeType == typeof(int)", result.SingleSource);
            Assert.DoesNotContain("((string)value.Value)", result.SingleSource);
            Assert.DoesNotContain("((byte[])value.Value)", result.SingleSource);
        }

        [Fact]
        public void UriMember_IsWrittenFromItsSerializationComponents()
        {
            // Not ToString(). SerializationInfoString is the round-trippable form, and it is what
            // normalises an authority-only Uri to carry a trailing slash - which the
            // SanityUriAndOffset fixture records.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Endpoint
    {
        [DataMember] public System.Uri Address { get; set; }
        [DataMember] public System.Uri[] Fallbacks { get; set; }
    }

    [DataContractSerializable(typeof(Endpoint))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("GetComponents(global::System.UriComponents.SerializationInfoString, global::System.UriFormat.UriEscaped)", result.SingleSource);

            // In a collection it is an XSD name in the Arrays namespace, like any other built-in.
            Assert.Contains("writer.WriteStartElement(\"anyURI\", \"http://schemas.microsoft.com/2003/10/Serialization/Arrays\");", result.SingleSource);
        }

        [Fact]
        public void DateTimeOffsetMember_IsWrittenAsATwoMemberContract()
        {
            // DateTimeOffset is not a value on the wire at all: DataContractSerializer swaps in
            // DateTimeOffsetAdapter, a contract with a DateTime and an OffsetMinutes member living
            // in a namespace neither type mentions. The DateTime written is the UTC one, so the
            // offset is recorded once rather than baked into both.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Stamped
    {
        [DataMember] public System.DateTimeOffset When { get; set; }
        [DataMember] public System.DateTimeOffset? Maybe { get; set; }
        [DataMember] public System.Collections.Generic.List<System.DateTimeOffset> Many { get; set; }
    }

    [DataContractSerializable(typeof(Stamped))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteStartElement(\"DateTime\", \"http://schemas.datacontract.org/2004/07/System\");", result.SingleSource);
            Assert.Contains("writer.WriteValue(value.UtcDateTime);", result.SingleSource);
            Assert.Contains("writer.WriteValue((short)value.Offset.TotalMinutes);", result.SingleSource);

            // An item is named after the contract and stays in the System namespace, unlike the
            // built-in types which all go into the Arrays namespace.
            Assert.Contains("writer.WriteStartElement(\"DateTimeOffset\", \"http://schemas.datacontract.org/2004/07/System\");", result.SingleSource);
        }

        [Fact]
        public void UnsignedLongMember_GoesThroughWriteRaw()
        {
            // XmlWriter has no WriteValue(ulong): ulong converts implicitly to float, double and
            // decimal and to none of them better than the others, so WriteValue(ulong) is ambiguous
            // rather than missing. The mistake surfaces as CS0121 in generated code, which the
            // compiler reported by exiting without a diagnostic - so AssertCompiles is the test that
            // matters here as much as the string assertions.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Counts
    {
        [DataMember] public ulong Total { get; set; }
        [DataMember] public ulong[] Buckets { get; set; }
    }

    [DataContractSerializable(typeof(Counts))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteRaw(global::System.Xml.XmlConvert.ToString(value.Total));", result.SingleSource);
            Assert.Contains("writer.WriteRaw(global::System.Xml.XmlConvert.ToString(item));", result.SingleSource);
        }

        [Fact]
        public void ObjectMember_WritesTheRuntimeTypeAsAnXsiType()
        {
            // object constrains nothing, so the runtime type decides the element's type, its value
            // and its namespace. char, Guid and TimeSpan are named in the serialization namespace
            // rather than XML Schema, which has no equivalent for them - a split that is invisible
            // until a document is compared byte for byte.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract]
    public class Holder
    {
        [DataMember] public object Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("writer.WriteQualifiedName(\"boolean\", \"http://www.w3.org/2001/XMLSchema\")", result.SingleSource);
            Assert.Contains("writer.WriteQualifiedName(\"char\", \"http://schemas.microsoft.com/2003/10/Serialization/\")", result.SingleSource);
            Assert.Contains("writer.WriteQualifiedName(\"duration\", \"http://schemas.microsoft.com/2003/10/Serialization/\")", result.SingleSource);

            // sbyte is "byte" and byte is "unsignedByte" - the reverse of the obvious guess.
            Assert.Contains("__runtimeType == typeof(sbyte)", result.SingleSource);
            Assert.Contains("writer.WriteQualifiedName(\"unsignedByte\", \"http://www.w3.org/2001/XMLSchema\")", result.SingleSource);

            // A bare object is anyType: an empty element with no i:type at all.
            Assert.Contains("// anyType: neither i:type nor content", result.SingleSource);
        }

        [Fact]
        public void ObjectMemberWithAnEnumKnownType_WritesTheEnumWithAnXsiType()
        {
            // Every known type in scope is a candidate for an object member, enums included. The
            // enum is announced with i:type like any other contract, then written from its own
            // value/name table rather than by a content writer.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    public enum Colour { Red }

    [DataContract]
    [KnownType(typeof(Colour))]
    public class Holder
    {
        [DataMember] public object Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("__runtimeType == typeof(global::App.Colour)", result.SingleSource);
            Assert.Contains("writer.WriteQualifiedName(\"Colour\", \"http://schemas.datacontract.org/2004/07/App\")", result.SingleSource);
            Assert.Contains("WriteEnum(writer, (long)((global::App.Colour)value.Value)", result.SingleSource);
        }

        [Fact]
        public void EnumCollection_NamesItemsAfterTheEnumContractNotTheArraysNamespace()
        {
            // An enum item is named after its own contract and stays in its own namespace, unlike
            // the built-in types which all go into the Arrays namespace. AllTypes.enumArrayData in
            // the corpus writes <a:MyEnum1> beside its containing contract for exactly this reason,
            // with no xmlns declaration on the member element at all.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    public enum Colour { Red, Green }

    [DataContract]
    public class Palette
    {
        [DataMember] public Colour[] Colours { get; set; }
    }

    [DataContractSerializable(typeof(Palette))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains(
                "writer.WriteStartElement(\"Colour\", \"http://schemas.datacontract.org/2004/07/App\");",
                result.SingleSource);
            Assert.DoesNotContain("http://schemas.microsoft.com/2003/10/Serialization/Arrays", result.SingleSource);
        }

        [Fact]
        public void IsReferenceOnAValueType_LeavesTheContractToReflection()
        {
            // A struct has no identity to preserve, and DataContractSerializer rejects the
            // combination rather than ignoring it. Declining keeps that behaviour.
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    [DataContract(IsReference = true)]
    public struct Referenced
    {
        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(Referenced))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            AssertCompiles(result);
            Assert.Contains("IsReference is not valid on a value type", result.SingleSource);
            Assert.DoesNotContain("if (type == typeof(global::App.Referenced))", result.SingleSource);
        }

        [Fact]
        public void NonPartialContext_ReportsCOREWCF_0400()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContractSerializable(typeof(Order))]
    public class MyContext : DataContractSerializerContext
    {
    }
"));

            Assert.Contains("COREWCF_0400", result.DiagnosticIds);
            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public void ContextNotDerivingFromBase_ReportsCOREWCF_0401()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(OrderContract + @"
    [DataContractSerializable(typeof(Order))]
    public partial class MyContext
    {
    }
"));

            Assert.Contains("COREWCF_0401", result.DiagnosticIds);
            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public void TypeWithoutDataContract_ReportsCOREWCF_0402()
        {
            GeneratorResult result = GeneratorTestHarness.Run(Source(@"
    public class NotAContract
    {
        public int Value { get; set; }
    }

    [DataContractSerializable(typeof(NotAContract))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            Assert.Contains("COREWCF_0402", result.DiagnosticIds);
        }

        [Fact]
        public void Disabled_EmitsNothing()
        {
            // The target framework gate is what keeps emitted code free to use a modern language
            // version; when it is off the generator must produce nothing at all.
            GeneratorResult result = GeneratorTestHarness.Run(
                Source(OrderContract + @"
    [DataContractSerializable(typeof(Order))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"),
                enabled: false);

            Assert.Empty(result.GeneratedSources);
            Assert.Empty(result.GeneratorDiagnostics);
        }
    }
}
