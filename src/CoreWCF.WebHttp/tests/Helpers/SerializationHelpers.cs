// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Helpers
{
    public static class SerializationHelpers
    {
        public static string SerializeJson<T>(T data)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            using MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, data);

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static T DeserializeJson<T>(string json)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            return (T)serializer.ReadObject(stream);
        }

        public static string SerializeXml<T>(T data)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(T));
            using MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, data);

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static T DeserializeXml<T>(string xml)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(T));
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            return (T)serializer.ReadObject(stream);
        }
    }
}
