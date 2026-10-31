// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime.Serialization;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    /// <summary>
    /// Which serializer a message part reads through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reading is opt-in separately from writing, so a part can be written by generated code and
    /// read by reflection. What must not happen is the two disagreeing within one part: if
    /// IsStartObject is answered by one serializer and ReadObject performed by the other, a part
    /// could be recognised and then read by something that does not recognise it.
    /// </para>
    /// <para>
    /// The generated serializer is planted directly on the part rather than by turning the
    /// AppContext switch on. The switch resolves into a Lazy that caches for the life of the
    /// process, so setting it here would leak into the rest of a concurrently running suite - and
    /// its own resolution order is already covered by GeneratedSerializerSwitchTests. What is under
    /// test here is the branch that follows.
    /// </para>
    /// </remarks>
    public class PartInfoSerializerSelectionTests
    {
        private const string PartName = "Part";
        private const string PartNamespace = "http://tempuri.org/";

        /// <summary>A stand-in that records what it was asked to do.</summary>
        private sealed class RecordingSerializer : AotXmlObjectSerializer
        {
            public bool CanRead { get; set; }

            public int IsStartObjectCalls { get; private set; }

            public int ReadObjectCalls { get; private set; }

            public override bool CanReadObject => CanRead;

            public override void WriteObject(XmlDictionaryWriter writer, object graph) =>
                throw new NotSupportedException("Not under test.");

            public override bool IsStartObject(XmlDictionaryReader reader)
            {
                IsStartObjectCalls++;
                reader.MoveToContent();
                return reader.IsStartElement(PartName, PartNamespace);
            }

            public override object ReadObject(XmlDictionaryReader reader, bool verifyObjectName)
            {
                ReadObjectCalls++;
                reader.Skip();
                return "read by the generated serializer";
            }
        }

        [Fact]
        public void PartWhoseGeneratedSerializerCanRead_UsesItForBothStartAndRead()
        {
            RecordingSerializer generated = new RecordingSerializer { CanRead = true };
            object part = CreatePart(generated);

            using (XmlDictionaryReader reader = ReaderOver(Recorded("hello")))
            {
                Assert.True(IsStartObject(part, reader));
                Assert.Equal("read by the generated serializer", ReadObject(part, reader));
            }

            Assert.Equal(1, generated.IsStartObjectCalls);
            Assert.Equal(1, generated.ReadObjectCalls);
        }

        [Fact]
        public void PartWhoseGeneratedSerializerCannotRead_FallsBackToReflectionForBoth()
        {
            // Half a fallback, and the half that matters here: the part still has a generated
            // serializer, but it does not read, so neither question may be put to it.
            RecordingSerializer generated = new RecordingSerializer { CanRead = false };
            object part = CreatePart(generated);

            using (XmlDictionaryReader reader = ReaderOver(Recorded("hello")))
            {
                Assert.True(IsStartObject(part, reader));
                Assert.Equal("hello", ReadObject(part, reader));
            }

            Assert.Equal(0, generated.IsStartObjectCalls);
            Assert.Equal(0, generated.ReadObjectCalls);
        }

        /// <summary>The bytes the reflection-based serializer produces, so neither path is guessed at.</summary>
        private static byte[] Recorded(string value)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(string), PartName, PartNamespace);

            using (MemoryStream stream = new MemoryStream())
            {
                using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream, System.Text.Encoding.UTF8, ownsStream: false))
                {
                    serializer.WriteObject(writer, value);
                }

                return stream.ToArray();
            }
        }

        private static XmlDictionaryReader ReaderOver(byte[] document) =>
            XmlDictionaryReader.CreateTextReader(document, XmlDictionaryReaderQuotas.Max);

        /// <summary>
        /// Builds a PartInfo with its generated serializer already resolved.
        /// </summary>
        /// <remarks>
        /// PartInfo is a protected nested type, reached by reflection the way
        /// GeneratedSerializerSwitchTests reaches the switch, so no InternalsVisibleTo is needed.
        /// </remarks>
        private static object CreatePart(AotXmlObjectSerializer generated)
        {
            Type partInfoType = typeof(DataContractSerializerOperationFormatter)
                .GetNestedType("PartInfo", BindingFlags.NonPublic);
            Assert.NotNull(partInfoType);

            ContractDescription contract = new ContractDescription("Contract");
            OperationDescription operation = new OperationDescription("Operation", contract);
            DataContractSerializerOperationBehavior behavior = new DataContractSerializerOperationBehavior(operation);

            XmlDictionary dictionary = new XmlDictionary(2);
            MessagePartDescription description = new MessagePartDescription(PartName, PartNamespace)
            {
                Type = typeof(string)
            };

            object part = Activator.CreateInstance(
                partInfoType,
                description,
                dictionary.Add(PartName),
                dictionary.Add(PartNamespace),
                new List<Type>(),
                behavior);

            // Planted rather than resolved, so the AppContext switch is left alone - see the remark
            // on the class.
            Field(partInfoType, "_aotSerializer").SetValue(part, generated);
            Field(partInfoType, "_aotSerializerResolved").SetValue(part, true);

            return part;
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field;
        }

        private static bool IsStartObject(object part, XmlDictionaryReader reader) =>
            (bool)Invoke(part, "IsStartObject", reader);

        private static object ReadObject(object part, XmlDictionaryReader reader) =>
            Invoke(part, "ReadObject", reader);

        private static object Invoke(object part, string name, XmlDictionaryReader reader)
        {
            MethodInfo method = part.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(XmlDictionaryReader) },
                modifiers: null);

            Assert.NotNull(method);
            return method.Invoke(part, new object[] { reader });
        }
    }
}
