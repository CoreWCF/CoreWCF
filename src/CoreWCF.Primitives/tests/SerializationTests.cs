// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using CoreWCF.Description;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class SerializationTests
    {
        #region DataContractResolver test
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
                catch (Exception ex)
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
                Items = new List<string> { "1", "2", "3" },
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
        #endregion

        #region SerializationSurrogateProvider test
        [Fact]
        public static void DataContractSerializationSurrogateTest()
        {
            OperationDescription od = null;
            DataContractSerializerOperationBehavior behavior = new DataContractSerializerOperationBehavior(od);

            behavior.SerializationSurrogateProvider = new MySerializationSurrogateProvider();

            DataContractSerializer dcs = (DataContractSerializer)behavior.CreateSerializer(typeof(SurrogateTestType), nameof(SurrogateTestType), "ns", new List<Type>());

            var members = new NonSerializableType[2];
            members[0] = new NonSerializableType("name1", 1);
            members[1] = new NonSerializableType("name2", 2);

            using (MemoryStream ms = new MemoryStream())
            {
                SurrogateTestType obj = new SurrogateTestType { Members = members };

                dcs.WriteObject(ms, obj);
                ms.Position = 0;
                var deserialized = (SurrogateTestType)dcs.ReadObject(ms);

                Assert.True(((MySerializationSurrogateProvider)behavior.SerializationSurrogateProvider).mySurrogateProviderIsUsed);

                for (int i = 0; i < 2; i++)
                {
                    Assert.Equal(obj.Members[i].Name, deserialized.Members[i].Name);
                    Assert.StrictEqual(obj.Members[i].Index, deserialized.Members[i].Index);
                }
            }
        }

        public class MySerializationSurrogateProvider : ISerializationSurrogateProvider
        {
            public bool mySurrogateProviderIsUsed = false;

            public object GetDeserializedObject(object obj, Type targetType)
            {
                mySurrogateProviderIsUsed = true;
                if (obj is NonSerializableTypeSurrogate)
                {
                    NonSerializableTypeSurrogate surrogate = (NonSerializableTypeSurrogate)obj;
                    return new NonSerializableType(surrogate.Name, surrogate.Index);
                }

                return obj;
            }

            public object GetObjectToSerialize(object obj, Type targetType)
            {
                mySurrogateProviderIsUsed = true;
                if (obj is NonSerializableType)
                {
                    NonSerializableType i = (NonSerializableType)obj;
                    NonSerializableTypeSurrogate surrogate = new NonSerializableTypeSurrogate
                    {
                        Name = i.Name,
                        Index = i.Index,
                    };

                    return surrogate;
                }

                return obj;
            }

            public Type GetSurrogateType(Type type)
            {
                mySurrogateProviderIsUsed = true;
                if (type == typeof(NonSerializableType))
                {
                    return typeof(NonSerializableTypeSurrogate);
                }

                return type;
            }
        }

        public class NonSerializableType
        {
            public string Name { get; private set; }
            public int Index { get; private set; }

            public NonSerializableType(string name, int index)
            {
                this.Name = name;
                this.Index = index;
            }
        }

        [DataContract]
        public class NonSerializableTypeSurrogate
        {
            [DataMember]
            public string Name { get; set; }
            [DataMember]
            public int Index { get; set; }
        }

        public class SurrogateTestType
        {
            public NonSerializableType[] Members { get; set; }
        }
        #endregion
    }
}
