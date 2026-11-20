// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.Runtime.Serialization;

namespace CoreWCF.DataContractSerialization
{
    /// <summary>
    /// An operation behavior that serves source-generated serializers from a
    /// <see cref="DataContractSerializerContext"/>, falling back to the reflection-based serializer
    /// for anything the context does not cover.
    /// </summary>
    /// <remarks>
    /// Replace the built-in behavior rather than adding alongside it:
    /// <c>OperationDescription.Behaviors</c> is a keyed collection where the first entry wins, so
    /// <c>Remove&lt;DataContractSerializerOperationBehavior&gt;()</c> then <c>Add(...)</c>.
    /// </remarks>
    public class GeneratedDataContractSerializerOperationBehavior : DataContractSerializerOperationBehavior
    {
        private readonly DataContractSerializerContext _context;

        public GeneratedDataContractSerializerOperationBehavior(OperationDescription operation, DataContractSerializerContext context)
            : base(operation)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public DataContractSerializerContext Context => _context;

        public override AotXmlObjectSerializer CreateAotSerializer(Type type, XmlDictionaryString name, XmlDictionaryString ns, IList<Type> knownTypes)
        {
            // Known types mean a member may hold a derived instance, which the serializer signals
            // with i:type and the derived contract's members. A generated serializer resolves the
            // closure of the [KnownType] attributes it was compiled against and nothing else, so a
            // known type supplied by the operation description - which no attribute reveals to the
            // generator - has to be checked against that closure before the work is taken on.
            // Declining sends the operation to the reflection-based serializer, which is always
            // correct; taking it and failing later would fail with the element already open.
            if (!_context.CoversKnownTypes(type, knownTypes))
            {
                return null;
            }

            return _context.GetSerializer(type, name, ns);
        }
    }
}
