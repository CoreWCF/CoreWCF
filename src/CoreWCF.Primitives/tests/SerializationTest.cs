// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using Helpers;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class SerializationTests
    {

        public class ClassToPass
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public List<string> Items { get; set; }
            public BasicClass Basic { get; set; }
        }

        public class BasicClass
        {
            public int Prop1 { get; set; }
        }

        public class ExtendedClass : BasicClass
        {

            public string Prop2 { get; set; }
            public bool? Prop3 { get; set; }

            public void Method() { }
        }

        private class BasicClassResolver : DataContractResolver
        {
            public override Type ResolveName(string typeName, string typeNamespace, Type declaredType, DataContractResolver knownTypeResolver)
            {
                try
                {
                    if (typeNamespace == "http://tempuri.org/BasicClass")
                    {
                        return Type.GetType(typeName);
                    }
                    return knownTypeResolver.ResolveName(typeName, typeNamespace, declaredType, null); 
                }
                catch(Exception ex)
                {
                    throw new Exception($"Failed to resolve name. {typeName}, {typeNamespace}", ex);
                }
            }
            public override bool TryResolveType(Type type, Type declaredType, DataContractResolver knownTypeResolver, out XmlDictionaryString typeName, out XmlDictionaryString typeNamespace)
            {
                if (!knownTypeResolver.TryResolveType(type, declaredType, null, out typeName, out typeNamespace))
                {
                    XmlDictionary dictionary = new XmlDictionary();
                    typeName = dictionary.Add(type.AssemblyQualifiedName);
                    typeNamespace = dictionary.Add("http://tempuri.org/BasicClass");
                }
                return true;
            }
        }

        [Fact]
        public void Deserialize_Basic_NoResolver_Success()
        {
            ClassToPass classToPass = new ClassToPass()
            {
                Name = "Test",
                Description = "Failure test",
                Items = new List<string> { "1", "2", "3" },
                Basic = new BasicClass() //Basic class!!!
                {
                    Prop1 = 1
                }
            };

            ClassToPass result = SerializeAndDeserialize(classToPass, bvr => { });
            Assert.True(result != null);
            Assert.True(result.Basic != null);
            Assert.True(result.Basic.GetType() == typeof(BasicClass));
        }

        [Fact]
        public void Deserialize_Basic_WithResolver_Success()
        {
            ClassToPass classToPass = new ClassToPass()
            {
                Name = "Test class",
                Description = "Serialization tests class",
                Items = new List<string> { "1", "2", "3" },
                Basic = new BasicClass() //Basic class!!!
                {
                    Prop1 = 1
                }
            };

            ClassToPass result = SerializeAndDeserialize(classToPass, bvr => { bvr.DataContractResolver = new BasicClassResolver(); });
            Assert.True(result != null);
            Assert.True(result.Basic != null);
            Assert.True(result.Basic.GetType() == typeof(BasicClass));
        }

        [Fact]
        public void Deserialize_Extended_NoResolver_FailToWrite()
        {
            ClassToPass classToPass = new ClassToPass()
            {
                Name = "Test",
                Description = "Failure test",
                Items = new List<string>{ "1", "2", "3" },
                Basic = new ExtendedClass() //Extended class!!!
                {
                    Prop1 = 1,
                    Prop2 = "2",
                    Prop3 = null
                }
            };

            Assert.Throws<SerializationException>(() => SerializeAndDeserialize(classToPass, bvr => { }));

        }


        [Fact]
        public void Deserialize_Extended_WithResolver_Success()
        {
            {
                ClassToPass classToPass = new ClassToPass()
                {
                    Name = "Test",
                    Description = "Failure test",
                    Items = new List<string> { "1", "2", "3" },
                    Basic = new ExtendedClass() //Extended class!!!
                    {
                        Prop1 = 1,
                        Prop2 = "2",
                        Prop3 = null
                    }
                };

                ClassToPass result = SerializeAndDeserialize(classToPass, bvr => { bvr.DataContractResolver = new BasicClassResolver(); });
                Assert.True(result != null);
                Assert.True(result.Basic != null);
                Assert.True(result.Basic.GetType() == typeof(ExtendedClass));
            }
        }

        private ClassToPass SerializeAndDeserialize(ClassToPass data, Action<Description.DataContractSerializerOperationBehavior> resolverSet)
        {
            Description.DataContractSerializerOperationBehavior behavior =
                new Description.DataContractSerializerOperationBehavior(
                    new Description.OperationDescription("Serialize", new Description.ContractDescription("Contract")));

            resolverSet(behavior);

            XmlDictionary dic = new XmlDictionary();
            XmlDictionaryString xmlName = dic.Add("ClassToPass");
            XmlDictionaryString xmlNamespace = dic.Add("http://temuri.org/GDRPONSWKSL");
            XmlObjectSerializer serializer = behavior.CreateSerializer(typeof(ClassToPass), xmlName, xmlNamespace, new Type[] { });

            MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, data);

            var buffer = stream.GetBuffer();

            return serializer.ReadObject(new MemoryStream(buffer)) as ClassToPass;
        }
    }
}
