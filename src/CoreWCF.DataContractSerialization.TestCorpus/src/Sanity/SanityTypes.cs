// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;

namespace CoreWCF.DataContractSerialization.TestCorpus.Sanity
{
    // CoreWCF-owned contract types covering the capability surface the source generator must
    // reproduce. These exist so the harness can be proven end-to-end before any upstream
    // dotnet/runtime code is imported, separating "is the harness correct?" from "does upstream
    // code compile here?".
    //
    // Every instance must be deterministic: no DateTime.Now, Guid.NewGuid, Random, or
    // DateTimeKind.Local. Local kind is serialized with the machine's UTC offset, so a fixture
    // generated in one time zone would fail on a UTC build agent - and re-running locally would
    // never reveal it.

    /// <summary>
    /// Every primitive the generator must format via XmlConvert, as a mix of properties and
    /// public fields. Fields are pervasive in DataContract code and are a classic generator
    /// oversight.
    /// </summary>
    /// <remarks>
    /// The double and float members are the deliberate .NET Framework divergence canary:
    /// .NET Core 3.0 made floating-point ToString shortest-round-trippable, so net472 is expected
    /// to render some values differently and exercise the per-TFM fixture override mechanism.
    /// </remarks>
    [DataContract]
    public class SanityPrimitives
    {
        [DataMember]
        public bool BoolMember;

        [DataMember]
        public byte ByteMember;

        [DataMember]
        public char CharMember;

        [DataMember]
        public int IntMember { get; set; }

        [DataMember]
        public long LongMember { get; set; }

        [DataMember]
        public short ShortMember { get; set; }

        [DataMember]
        public decimal DecimalMember { get; set; }

        [DataMember]
        public double DoubleMember { get; set; }

        [DataMember]
        public float FloatMember { get; set; }

        [DataMember]
        public string StringMember { get; set; }

        [DataMember]
        public Guid GuidMember { get; set; }

        [DataMember]
        public DateTime DateTimeMember { get; set; }

        [DataMember]
        public TimeSpan TimeSpanMember { get; set; }

        [DataMember]
        public byte[] BytesMember { get; set; }

        public static SanityPrimitives Populated()
        {
            return new SanityPrimitives
            {
                BoolMember = true,
                ByteMember = 200,
                CharMember = 'Z',
                IntMember = -42,
                LongMember = 9007199254740993L,
                ShortMember = 1234,
                DecimalMember = 12345.6789m,
                DoubleMember = 0.1 + 0.2,
                FloatMember = 1.1E-5f,
                StringMember = "hello <world> & \"friends\"",
                GuidMember = new Guid("2f9e4c1a-0000-4000-8000-000000000001"),
                DateTimeMember = new DateTime(2020, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc),
                TimeSpanMember = new TimeSpan(1, 2, 3, 4, 5),
                BytesMember = new byte[] { 0, 1, 2, 250, 255 }
            };
        }
    }

    /// <summary>
    /// Member ordering and emission rules: explicit Order, renamed members, required members, and
    /// suppression of defaults. Historically the single largest source of hand-written serializer
    /// bugs.
    /// </summary>
    [DataContract]
    public class SanityMemberAttributes
    {
        [DataMember(Order = 3)]
        public string Third { get; set; }

        [DataMember(Order = 1)]
        public string First { get; set; }

        [DataMember(Order = 2, Name = "RenamedSecond")]
        public string Second { get; set; }

        [DataMember(IsRequired = true)]
        public int Required { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public int OmittedWhenDefault { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string OmittedStringWhenNull { get; set; }

        public string NotAMember { get; set; }

        public static SanityMemberAttributes Populated()
        {
            return new SanityMemberAttributes
            {
                First = "1",
                Second = "2",
                Third = "3",
                Required = 7,
                OmittedWhenDefault = 0,
                OmittedStringWhenNull = null,
                NotAMember = "must not appear"
            };
        }
    }

    /// <summary>Custom contract name and namespace, driving xmlns declaration and prefix assignment.</summary>
    [DataContract(Name = "RenamedContract", Namespace = "http://corewcf.example/sanity")]
    public class SanityCustomNaming
    {
        [DataMember(Name = "RenamedValue")]
        public string Value { get; set; }

        [DataMember]
        public SanityNestedNamespace Nested { get; set; }

        public static SanityCustomNaming Populated()
        {
            return new SanityCustomNaming
            {
                Value = "outer",
                Nested = new SanityNestedNamespace { Inner = "inner" }
            };
        }
    }

    /// <summary>A member type in a different contract namespace, to pin namespace inference through the graph.</summary>
    [DataContract(Namespace = "http://corewcf.example/sanity/nested")]
    public class SanityNestedNamespace
    {
        [DataMember]
        public string Inner { get; set; }
    }

    /// <summary>Base of the inheritance pair. Base members are emitted before derived members.</summary>
    [DataContract]
    [KnownType(typeof(SanityDerived))]
    [KnownType(typeof(SanityFurtherDerived))]
    public class SanityBase
    {
        [DataMember]
        public string BaseMember { get; set; }

        [DataMember]
        public int BaseOrdinal { get; set; }
    }

    /// <summary>Derived contract. Serialized through the base declared type it emits an i:type attribute.</summary>
    [DataContract]
    public class SanityDerived : SanityBase
    {
        [DataMember]
        public string DerivedMember { get; set; }

        public static SanityDerived Populated()
        {
            return new SanityDerived
            {
                BaseMember = "base",
                BaseOrdinal = 1,
                DerivedMember = "derived"
            };
        }
    }

    /// <summary>Second level of the chain, so an i:type has more than one candidate to resolve against.</summary>
    [DataContract]
    public class SanityFurtherDerived : SanityDerived
    {
        [DataMember]
        public int FurtherOrdinal { get; set; }

        public static SanityFurtherDerived Populated()
        {
            return new SanityFurtherDerived
            {
                BaseMember = "base",
                BaseOrdinal = 1,
                DerivedMember = "derived",
                FurtherOrdinal = 2
            };
        }
    }

    /// <summary>
    /// One member declared as the base, holding each of the shapes an i:type has to tell apart.
    /// </summary>
    /// <remarks>
    /// A single derived instance does not exercise a dispatch: it cannot tell "resolved the name"
    /// from "took the only branch". Here the declared type carries no i:type at all, two different
    /// derived types carry different ones, and a null carries none - four different documents.
    /// </remarks>
    [DataContract]
    public class SanityPolymorphic
    {
        [DataMember]
        public SanityBase AsDeclared { get; set; }

        [DataMember]
        public SanityBase AsDerived { get; set; }

        [DataMember]
        public SanityBase AsFurtherDerived { get; set; }

        [DataMember]
        public SanityBase Missing { get; set; }

        public static SanityPolymorphic Populated()
        {
            return new SanityPolymorphic
            {
                AsDeclared = new SanityBase { BaseMember = "plain", BaseOrdinal = 7 },
                AsDerived = SanityDerived.Populated(),
                AsFurtherDerived = SanityFurtherDerived.Populated(),
                Missing = null
            };
        }
    }

    /// <summary>Holder whose member is declared as the base type but holds a derived instance.</summary>
    [DataContract]
    public class SanityKnownTypeHolder
    {
        [DataMember]
        public SanityBase Value { get; set; }

        public static SanityKnownTypeHolder Populated()
        {
            return new SanityKnownTypeHolder { Value = SanityDerived.Populated() };
        }
    }

    /// <summary>Arrays, lists and dictionaries, whose element naming rules no generator gets right by accident.</summary>
    [DataContract]
    public class SanityCollections
    {
        [DataMember]
        public string[] StringArray { get; set; }

        [DataMember]
        public List<int> IntList { get; set; }

        [DataMember]
        public Dictionary<string, string> StringMap { get; set; }

        [DataMember]
        public int[] EmptyArray { get; set; }

        public static SanityCollections Populated()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            map.Add("alpha", "1");

            return new SanityCollections
            {
                StringArray = new string[] { "a", "b" },
                IntList = new List<int> { 1, 2, 3 },
                StringMap = map,
                EmptyArray = new int[0]
            };
        }
    }

    /// <summary>
    /// One <c>object</c>-declared member per supported primitive, so the fixture records the
    /// <c>i:type</c> name and namespace DataContractSerializer gives each.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="SanityPrimitiveArrays"/>, and needed separately: a collection
    /// item name and a boxed value's xsi type are not the same table. Most names come from XML
    /// Schema, but the types XSD has no equivalent for do not, and which is which is exactly what
    /// this fixture pins. Also carries the three shapes that are not a boxed primitive at all - a
    /// bare object, a null, and a data contract - since each is written differently.
    /// </remarks>
    [DataContract]
    [KnownType(typeof(SanityNestedNamespace))]
    public class SanityBoxedPrimitives
    {
        [DataMember]
        public object BareObject { get; set; }

        [DataMember]
        public object Boolean { get; set; }

        [DataMember]
        public object BoxedUri { get; set; }

        [DataMember]
        public object ByteArray { get; set; }

        [DataMember]
        public object Char { get; set; }

        [DataMember]
        public object Contract { get; set; }

        [DataMember]
        public object DateTime { get; set; }

        [DataMember]
        public object Decimal { get; set; }

        [DataMember]
        public object Double { get; set; }

        [DataMember]
        public object Guid { get; set; }

        [DataMember]
        public object Int16 { get; set; }

        [DataMember]
        public object Int32 { get; set; }

        [DataMember]
        public object Int64 { get; set; }

        [DataMember]
        public object Null { get; set; }

        [DataMember]
        public object SByte { get; set; }

        [DataMember]
        public object Single { get; set; }

        [DataMember]
        public object String { get; set; }

        [DataMember]
        public object TimeSpan { get; set; }

        [DataMember]
        public object UInt16 { get; set; }

        [DataMember]
        public object UInt32 { get; set; }

        [DataMember]
        public object UInt64 { get; set; }

        [DataMember]
        public object UnsignedByte { get; set; }

        public static SanityBoxedPrimitives Populated()
        {
            return new SanityBoxedPrimitives
            {
                BareObject = new object(),
                Boolean = true,
                BoxedUri = new Uri("http://corewcf.example"),
                ByteArray = new byte[] { 7, 8 },
                Char = 'a',
                Contract = new SanityNestedNamespace { Inner = "nested" },
                DateTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                Decimal = 1.5m,
                Double = 0.1d,
                Guid = new Guid("2f9e4c1a-0000-4000-8000-000000000003"),
                Int16 = (short)-1,
                Int32 = 2,
                Int64 = 3L,
                Null = null,
                SByte = (sbyte)-8,
                Single = 2.5f,
                String = "text",
                TimeSpan = new TimeSpan(1, 2, 3),
                UInt16 = (ushort)4,
                UInt32 = 5u,
                UInt64 = 6ul,
                UnsignedByte = (byte)7
            };
        }
    }

#if !NETFRAMEWORK
    /// <summary>
    /// <c>DateOnly</c> and <c>TimeOnly</c>, whose wire format changed between runtimes.
    /// </summary>
    /// <remarks>
    /// Up to .NET 9 DataContractSerializer had no idea what these were and wrote them as a contract
    /// with no members - an empty element that drops the value entirely. .NET 10 writes them as
    /// primitives. The upstream DateTimeOnlyWrapper only ever holds default values, so it cannot
    /// tell a lost value from a zero one; this type carries real ones so the fixture records the
    /// actual format on each runtime.
    /// </remarks>
    [DataContract]
    public class SanityDateAndTimeOnly
    {
        [DataMember]
        public DateOnly Date { get; set; }

        [DataMember]
        public TimeOnly Time { get; set; }

        [DataMember]
        public TimeOnly Precise { get; set; }

        [DataMember]
        public DateOnly? NullableDate { get; set; }

        [DataMember]
        public TimeOnly? MissingTime { get; set; }

        public static SanityDateAndTimeOnly Populated()
        {
            return new SanityDateAndTimeOnly
            {
                Date = new DateOnly(2020, 1, 2),
                Time = new TimeOnly(3, 4, 5),

                // Sub-second precision, so the fixture records how far the format goes.
                Precise = new TimeOnly(6, 7, 8, 9),
                NullableDate = new DateOnly(2021, 11, 12),
                MissingTime = null
            };
        }
    }
#endif

    /// <summary>
    /// <c>Uri</c> and <c>DateTimeOffset</c>, neither of which is what it looks like on the wire.
    /// </summary>
    /// <remarks>
    /// A Uri is written from its serialization components rather than ToString, which normalises it.
    /// A DateTimeOffset is not a value at all: DataContractSerializer swaps in an adapter contract
    /// with a DateTime and an OffsetMinutes member, in a namespace neither type mentions. Both the
    /// element shapes and the namespace come from this fixture rather than from memory.
    /// </remarks>
    [DataContract]
    public class SanityUriAndOffset
    {
        [DataMember]
        public Uri Absolute { get; set; }

        [DataMember]
        public Uri MissingUri { get; set; }

        [DataMember]
        public DateTimeOffset Offset { get; set; }

        [DataMember]
        public DateTimeOffset Utc { get; set; }

        [DataMember]
        public DateTimeOffset? NullableSet { get; set; }

        [DataMember]
        public DateTimeOffset? NullableMissing { get; set; }

        [DataMember]
        public List<DateTimeOffset> Offsets { get; set; }

        [DataMember]
        public Uri[] Uris { get; set; }

        public static SanityUriAndOffset Populated()
        {
            return new SanityUriAndOffset
            {
                // No trailing slash here, so the fixture records whether one is added.
                Absolute = new Uri("http://corewcf.example"),
                MissingUri = null,
                Offset = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromMinutes(90)),
                Utc = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero),
                NullableSet = new DateTimeOffset(2021, 6, 7, 8, 9, 10, TimeSpan.FromMinutes(-300)),
                NullableMissing = null,
                Offsets = new List<DateTimeOffset>
                {
                    new DateTimeOffset(2022, 2, 3, 4, 5, 6, TimeSpan.Zero)
                },
                Uris = new Uri[] { new Uri("http://corewcf.example/a"), null }
            };
        }
    }

    /// <summary>
    /// One array per supported primitive, so the fixture records the element name
    /// DataContractSerializer gives each. Those names are XSD-derived and not guessable with
    /// confidence - "byte" is sbyte and "unsignedByte" is byte, TimeSpan is "duration" - so the
    /// oracle is the specification here rather than memory.
    /// </summary>
    [DataContract]
    public class SanityPrimitiveArrays
    {
        [DataMember]
        public bool[] Booleans { get; set; }

        [DataMember]
        public byte[][] ByteArrays { get; set; }

        [DataMember]
        public char[] Chars { get; set; }

        [DataMember]
        public DateTime[] DateTimes { get; set; }

        [DataMember]
        public decimal[] Decimals { get; set; }

        [DataMember]
        public double[] Doubles { get; set; }

        [DataMember]
        public float[] Floats { get; set; }

        [DataMember]
        public Guid[] Guids { get; set; }

        [DataMember]
        public short[] Int16s { get; set; }

        [DataMember]
        public int[] Int32s { get; set; }

        [DataMember]
        public long[] Int64s { get; set; }

        [DataMember]
        public sbyte[] SBytes { get; set; }

        [DataMember]
        public string[] Strings { get; set; }

        [DataMember]
        public TimeSpan[] TimeSpans { get; set; }

        [DataMember]
        public byte[] UnsignedBytes { get; set; }

        [DataMember]
        public ushort[] UInt16s { get; set; }

        [DataMember]
        public uint[] UInt32s { get; set; }

        [DataMember]
        public ulong[] UInt64s { get; set; }

        [DataMember]
        public List<int> IntList { get; set; }

        public static SanityPrimitiveArrays Populated()
        {
            return new SanityPrimitiveArrays
            {
                Booleans = new bool[] { true, false },
                ByteArrays = new byte[][] { new byte[] { 1, 2 } },
                Chars = new char[] { 'a', 'Z' },
                DateTimes = new DateTime[] { new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc) },
                Decimals = new decimal[] { 1.5m },
                Doubles = new double[] { 0.1 },
                Floats = new float[] { 2.5f },
                Guids = new Guid[] { new Guid("2f9e4c1a-0000-4000-8000-000000000002") },
                Int16s = new short[] { -1, 1 },
                Int32s = new int[] { 1, 2, 3 },
                Int64s = new long[] { 4L },
                SBytes = new sbyte[] { -8 },
                Strings = new string[] { "a", null },
                TimeSpans = new TimeSpan[] { new TimeSpan(1, 2, 3) },
                UnsignedBytes = new byte[] { 7, 8 },
                UInt16s = new ushort[] { 9 },
                UInt32s = new uint[] { 10 },
                UInt64s = new ulong[] { 11 },
                IntList = new List<int> { 12, 13 }
            };
        }
    }

    public enum SanityEnum
    {
        None = 0,
        First = 1,
        Second = 2
    }

    [Flags]
    public enum SanityFlagsEnum
    {
        None = 0,
        Alpha = 1,
        Beta = 2,
        Gamma = 4
    }

    [DataContract]
    public enum SanityRenamedEnum
    {
        [EnumMember(Value = "wire-none")]
        None = 0,

        [EnumMember(Value = "wire-one")]
        One = 1
    }

    /// <summary>Enum value to wire-text mapping, including EnumMember renaming.</summary>
    [DataContract]
    public class SanityEnums
    {
        [DataMember]
        public SanityEnum Plain { get; set; }

        [DataMember]
        public SanityFlagsEnum Flags { get; set; }

        [DataMember]
        public SanityRenamedEnum Renamed { get; set; }

        public static SanityEnums Populated()
        {
            return new SanityEnums
            {
                Plain = SanityEnum.Second,
                Flags = SanityFlagsEnum.Alpha | SanityFlagsEnum.Gamma,
                Renamed = SanityRenamedEnum.One
            };
        }
    }

    /// <summary>
    /// <c>XmlQualifiedName</c>, the one member type whose element carries a prefix of its own.
    /// </summary>
    /// <remarks>
    /// The prefix is declared on the member element and nowhere else, so the value cannot be read
    /// back from the text alone - the element that defines the prefix has to still be open. The
    /// empty name is a separate shape again: the writer emits no content for it at all, so it comes
    /// back as an empty element rather than as an empty string.
    /// </remarks>
    [DataContract]
    public class SanityQualifiedNames
    {
        [DataMember]
        public XmlQualifiedName Named { get; set; }

        [DataMember]
        public XmlQualifiedName EmptyName { get; set; }

        [DataMember]
        public XmlQualifiedName MissingName { get; set; }

        public static SanityQualifiedNames Populated()
        {
            return new SanityQualifiedNames
            {
                Named = new XmlQualifiedName("WCF", "http://corewcf.example/schema"),
                EmptyName = XmlQualifiedName.Empty,
                MissingName = null
            };
        }
    }

    /// <summary>
    /// The two collections whose container a read cannot infer from its items.
    /// </summary>
    /// <remarks>
    /// Everything else on the read side accumulates into a <c>List&lt;T&gt;</c> and either assigns
    /// it or calls ToArray on it. An <c>ArrayList</c> is neither, and a jagged array is two of them
    /// nested. The upstream cases that cover these - ArrayContainer and Array3 - do not reach the
    /// read side: ArrayContainer declares a constructor taking a bool and so has no parameterless
    /// one, and Array3 has no null inner row.
    /// </remarks>
    [DataContract]
    public class SanityUntypedCollections
    {
        [DataMember]
        public ArrayList Mixed { get; set; }

        [DataMember]
        public ArrayList EmptyList { get; set; }

        [DataMember]
        public ArrayList MissingList { get; set; }

        [DataMember]
        public int[][] Jagged { get; set; }

        public static SanityUntypedCollections Populated()
        {
            ArrayList mixed = new ArrayList();
            mixed.Add("text");
            mixed.Add(42);
            mixed.Add(new Guid("2f9e4c1a-0000-4000-8000-000000000004"));
            mixed.Add(null);

            return new SanityUntypedCollections
            {
                Mixed = mixed,
                EmptyList = new ArrayList(),
                MissingList = null,

                // A populated row, an empty one and a missing one: three different documents, and
                // only the middle two are reachable from an array that is never null.
                Jagged = new int[][] { new int[] { 1, 2 }, new int[0], null }
            };
        }
    }

    /// <summary>
    /// Dictionary shapes one populated string map does not reach.
    /// </summary>
    /// <remarks>
    /// An empty map, a missing one, and a map holding a null value are three different documents -
    /// an empty element, an i:nil element, and an entry whose Value carries i:nil - and telling
    /// them apart is exactly what a round-trip tests and a write-only fixture does not.
    /// </remarks>
    [DataContract]
    public class SanityDictionaries
    {
        [DataMember]
        public Dictionary<int, DateTime> IntToDateTime { get; set; }

        [DataMember]
        public Dictionary<string, byte[]> StringToBytes { get; set; }

        [DataMember]
        public Dictionary<string, string> EmptyMap { get; set; }

        [DataMember]
        public Dictionary<string, string> MissingMap { get; set; }

        public static SanityDictionaries Populated()
        {
            Dictionary<int, DateTime> byNumber = new Dictionary<int, DateTime>();
            byNumber.Add(7, new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc));

            Dictionary<string, byte[]> byName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            byName.Add("payload", new byte[] { 9, 8, 7 });
            byName.Add("absent", null);

            return new SanityDictionaries
            {
                IntToDateTime = byNumber,
                StringToBytes = byName,
                EmptyMap = new Dictionary<string, string>(StringComparer.Ordinal),
                MissingMap = null
            };
        }
    }

    /// <summary>
    /// Enums inside a collection, whose items are named after the enum's own contract rather than
    /// after an XSD type, and so sit in its namespace instead of the Arrays one.
    /// </summary>
    [DataContract]
    public class SanityEnumCollections
    {
        [DataMember]
        public SanityEnum[] Plain { get; set; }

        [DataMember]
        public List<SanityFlagsEnum> Flags { get; set; }

        public static SanityEnumCollections Populated()
        {
            return new SanityEnumCollections
            {
                Plain = new SanityEnum[] { SanityEnum.First, SanityEnum.None },
                Flags = new List<SanityFlagsEnum> { SanityFlagsEnum.Beta | SanityFlagsEnum.Gamma }
            };
        }
    }

    /// <summary>Null references and nullable value types, driving i:nil emission.</summary>
    [DataContract]
    public class SanityNullable
    {
        [DataMember]
        public string NullString { get; set; }

        [DataMember]
        public SanityNestedNamespace NullReference { get; set; }

        [DataMember]
        public int? NullInt { get; set; }

        [DataMember]
        public int? SetInt { get; set; }

        [DataMember]
        public DateTime? NullDateTime { get; set; }

        public static SanityNullable Populated()
        {
            return new SanityNullable
            {
                NullString = null,
                NullReference = null,
                NullInt = null,
                SetInt = 5,
                NullDateTime = null
            };
        }
    }

    /// <summary>
    /// Reference-preserving contract containing a cycle, driving z:Id / z:Ref emission and the
    /// serialization namespace declaration. The hardest single behaviour for a generator to match.
    /// </summary>
    [DataContract(IsReference = true)]
    public class SanityReferenceNode
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public SanityReferenceNode Next { get; set; }

        [DataMember]
        public SanityReferenceNode Self { get; set; }

        public static SanityReferenceNode Populated()
        {
            SanityReferenceNode first = new SanityReferenceNode { Name = "first" };
            SanityReferenceNode second = new SanityReferenceNode { Name = "second" };

            first.Next = second;
            first.Self = first;
            second.Next = first;
            second.Self = second;

            return first;
        }
    }
}
