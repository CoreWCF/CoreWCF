// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.DataContractSerialization
{
    /// <summary>
    /// Declares that a serializer should be generated for <paramref name="type"/> and added to the
    /// annotated <see cref="DataContractSerializerContext"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// [DataContractSerializable(typeof(Order))]
    /// [DataContractSerializable(typeof(Customer))]
    /// public partial class MyContracts : DataContractSerializerContext { }
    /// </code>
    /// </example>
    /// <remarks>
    /// Modelled on <c>JsonSerializableAttribute</c>. Declaring the context is the opt-in: nothing is
    /// generated for a type until something asks for it, and the resulting list of types is rooted,
    /// which is what makes the output trimmable.
    /// <para>
    /// The context must be declared in the same assembly as the contract types whenever any of their
    /// <c>[DataMember]</c>s is not public - generated code cannot reach a private member across an
    /// assembly boundary. This is the same constraint System.Text.Json's source generator has.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class DataContractSerializableAttribute : Attribute
    {
        public DataContractSerializableAttribute(Type type)
        {
            Type = type;
        }

        /// <summary>The contract type to generate a serializer for.</summary>
        public Type Type { get; }
    }
}
