// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Runtime.Serialization;

namespace CoreWCF.DataContractSerialization
{
    /// <summary>
    /// Base class for a user-declared partial class that the source generator fills in with one
    /// reflection-free serializer per <see cref="DataContractSerializableAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GetSerializer"/> is deliberately <c>virtual</c> returning <c>null</c> rather than
    /// <c>abstract</c>. The generator is gated to target frameworks whose default language version
    /// supports the code it emits, so on .NET Framework and netstandard it never runs and a user's
    /// partial class is never completed. An abstract member would leave that class uncompilable;
    /// returning null instead means the same source compiles everywhere and simply contributes
    /// nothing where generation did not happen, falling back to the reflection-based serializer with
    /// no conditional compilation in user code.
    /// </para>
    /// <para>
    /// That is the same shape as <c>DataContractSerializerOperationBehavior.CreateAotSerializer</c>
    /// returning null, and together they are what make the generated path an optimization rather
    /// than a replacement.
    /// </para>
    /// </remarks>
    public abstract class DataContractSerializerContext
    {
        /// <summary>
        /// Returns a generated serializer for <paramref name="type"/> rooted at the given element
        /// name and namespace, or null when this context has none.
        /// </summary>
        public virtual AotXmlObjectSerializer GetSerializer(Type type, XmlDictionaryString name, XmlDictionaryString ns)
        {
            return null;
        }

        /// <summary>
        /// Whether the serializer this context would return for <paramref name="type"/> already
        /// resolves every one of <paramref name="knownTypes"/>.
        /// </summary>
        /// <remarks>
        /// CoreWCF supplies known types from the operation description, which no attribute reveals
        /// to the generator. A generated serializer resolves exactly the closure of the
        /// <c>[KnownType]</c> attributes it was compiled against, so anything outside that closure
        /// is a type it would refuse at run time - and refusing mid-document is far worse than not
        /// taking the work. Answering false sends the operation back to the reflection-based
        /// serializer, which is always correct.
        /// </remarks>
        public virtual bool CoversKnownTypes(Type type, IList<Type> knownTypes)
        {
            return knownTypes == null || knownTypes.Count == 0;
        }
    }
}
