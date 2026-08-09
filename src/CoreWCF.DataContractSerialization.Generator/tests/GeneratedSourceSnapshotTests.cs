// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using static VerifyXunit.Verifier;

namespace CoreWCF.DataContractSerialization.Generator.Tests
{
    /// <summary>
    /// The generated code itself, checked in so it can be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everywhere else the emitted source is asserted a line at a time, which is right for pinning a
    /// rule and wrong for seeing the shape of the whole thing. Nobody reviewing a change to this
    /// generator can hold six hundred lines of output in their head from a diff of the emitter; here
    /// they read the output, and a change to it turns up in the diff of the pull request that caused
    /// it.
    /// </para>
    /// <para>
    /// A snapshot is a record, not an assertion. It cannot say whether output is <em>correct</em> -
    /// only the golden-record corpus does that, by comparing bytes against the real serializer. What
    /// it says is that output changed, and by how much, which is the question a reviewer has and the
    /// assertions cannot answer.
    /// </para>
    /// <para>
    /// Four cases rather than one per feature: each snapshot carries the shared helpers as well as
    /// the contract's own code, so they overlap heavily and a fifth would mostly repeat the first.
    /// These four are chosen to cover the structurally distinct shapes. Adding another is one method.
    /// </para>
    /// </remarks>
    public class GeneratedSourceSnapshotTests
    {
        /// <summary>
        /// The baseline: one flat contract, so the snapshot is mostly the shared helpers every
        /// context gets.
        /// </summary>
        [Fact]
        public Task FlatContract()
        {
            GeneratorDriver driver = GeneratorTestHarness.RunForSnapshot(Source(@"
    [DataContract]
    public class Order
    {
        [DataMember] public int Id { get; set; }
        [DataMember(Name = ""customer"", Order = 2)] public string Customer { get; set; }
        [DataMember(EmitDefaultValue = false)] public decimal? Discount { get; set; }
        [DataMember] public byte[] Signature { get; set; }
    }

    [DataContractSerializable(typeof(Order))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            return Verify(driver).UseDirectory("Snapshots");
        }

        /// <summary>
        /// Inheritance and i:type in both directions: the flattened read, the runtime-type switch on
        /// the way out and the name lookup on the way back in.
        /// </summary>
        [Fact]
        public Task InheritanceAndPolymorphism()
        {
            GeneratorDriver driver = GeneratorTestHarness.RunForSnapshot(Source(@"
    [DataContract]
    [KnownType(typeof(Derived))]
    public class Base
    {
        [DataMember] public string Zulu { get; set; }
    }

    [DataContract]
    public class Derived : Base
    {
        [DataMember] public int Alpha { get; set; }
    }

    [DataContract]
    public class Holder
    {
        [DataMember] public Base Value { get; set; }
    }

    [DataContractSerializable(typeof(Holder))]
    [DataContractSerializable(typeof(Base))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            return Verify(driver).UseDirectory("Snapshots");
        }

        /// <summary>
        /// The container shapes, which share one sequence loop but differ in what they build.
        /// </summary>
        [Fact]
        public Task CollectionsDictionaryAndEnum()
        {
            GeneratorDriver driver = GeneratorTestHarness.RunForSnapshot(Source(@"
    [System.Flags]
    public enum Marks { None = 0, Alpha = 1, Beta = 2 }

    [DataContract]
    public class Bag
    {
        [DataMember] public string[] Tags { get; set; }
        [DataMember] public System.Collections.Generic.List<int> Counts { get; set; }
        [DataMember] public int[][] Rows { get; set; }
        [DataMember] public System.Collections.ArrayList Untyped { get; set; }
        [DataMember] public System.Collections.Generic.Dictionary<string, int> Map { get; set; }
        [DataMember] public Marks Flags { get; set; }
    }

    [DataContractSerializable(typeof(Bag))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            return Verify(driver).UseDirectory("Snapshots");
        }

        /// <summary>
        /// Object identity: both reference scopes, the z:Id/z:Ref pair, and the by-value cycle guard.
        /// </summary>
        [Fact]
        public Task ReferencePreservingGraph()
        {
            GeneratorDriver driver = GeneratorTestHarness.RunForSnapshot(Source(@"
    [DataContract(IsReference = true)]
    public class Node
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public Node Next { get; set; }
    }

    [DataContractSerializable(typeof(Node))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            return Verify(driver).UseDirectory("Snapshots");
        }

        /// <summary>
        /// A contract that falls back, so the diagnostics are in the record too.
        /// </summary>
        /// <remarks>
        /// Verify renders the driver's diagnostics alongside its sources, which makes this the one
        /// place the wording of COREWCF_0403 and COREWCF_0404 is visible as a user would read it.
        /// </remarks>
        [Fact]
        public Task ContractsThatFallBackToReflection()
        {
            GeneratorDriver driver = GeneratorTestHarness.RunForSnapshot(Source(@"
    [DataContract]
    public class HasPrivateMember
    {
        [DataMember] private int _hidden;
    }

    [DataContract]
    public class NoDefaultConstructor
    {
        public NoDefaultConstructor(int seed) { Value = seed; }

        [DataMember] public int Value { get; set; }
    }

    [DataContractSerializable(typeof(HasPrivateMember))]
    [DataContractSerializable(typeof(NoDefaultConstructor))]
    public partial class MyContext : DataContractSerializerContext
    {
    }
"));

            return Verify(driver).UseDirectory("Snapshots");
        }

        private static string Source(string body) =>
            @"using System.Runtime.Serialization;
using CoreWCF.DataContractSerialization;

namespace App
{
" + body + @"
}
";
    }
}
