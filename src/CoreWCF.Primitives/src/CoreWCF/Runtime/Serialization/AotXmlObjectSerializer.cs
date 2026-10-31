// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.Runtime.Serialization
{
    /// <summary>
    /// A serializer that already knows its contract at compile time, and therefore needs no
    /// reflection and no runtime code generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because <see cref="System.Runtime.Serialization.XmlObjectSerializer"/> cannot be
    /// used to express an AOT-safe serializer. Every one of its sixteen members carries
    /// <c>[RequiresDynamicCode]</c> and <c>[RequiresUnreferencedCode]</c>; only its protected
    /// constructor is clean. Trim analysis requires an override's annotations to match the member it
    /// overrides (IL2046/IL3051), and callers reach it through the base-typed reference anyway - so a
    /// reflection-free subclass would make serialization <em>work</em> under Native AOT without
    /// silencing a single warning. The annotation lives in the abstraction, not the implementation.
    /// </para>
    /// <para>
    /// The remedy is the one System.Text.Json used: rather than un-annotating the reflection API, add
    /// a second, attribute-free one that takes the compile-time contract as a parameter.
    /// <c>JsonSerializer.Serialize&lt;T&gt;(T)</c> is still annotated today; <c>Serialize(T,
    /// JsonTypeInfo&lt;T&gt;)</c> is not. This type is that second API, narrowed to the members
    /// CoreWCF's operation formatter actually calls.
    /// </para>
    /// <para>
    /// Deliberately carries no attributes of its own, which also means it needs no polyfills on
    /// netstandard2.0, where the trim and AOT attributes do not exist.
    /// </para>
    /// </remarks>
    public abstract class AotXmlObjectSerializer
    {
        /// <summary>Writes <paramref name="graph"/> as a complete element.</summary>
        public abstract void WriteObject(XmlDictionaryWriter writer, object graph);

        /// <summary>
        /// Whether this serializer can also read. Read support is opt-in because a generator may
        /// emit the write direction before the read direction; while this is <c>false</c>, CoreWCF
        /// keeps using the reflection-based serializer to deserialize. The two directions are
        /// independent, so mixing them is safe.
        /// </summary>
        public virtual bool CanReadObject => false;

        public virtual bool IsStartObject(XmlDictionaryReader reader)
        {
            throw new NotSupportedException(ReadNotSupportedMessage());
        }

        public virtual object ReadObject(XmlDictionaryReader reader, bool verifyObjectName)
        {
            throw new NotSupportedException(ReadNotSupportedMessage());
        }

        private string ReadNotSupportedMessage() =>
            GetType().FullName + " does not support reading. Callers must check " +
            nameof(CanReadObject) + " before calling " + nameof(ReadObject) + " or " + nameof(IsStartObject) + ".";
    }
}
