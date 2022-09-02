// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Services
{
    public class SerializationService : ServiceContract.ISerializationService
    {
        public ServiceContract.SerializationData SerializeDeserializeJson(ServiceContract.SerializationData data) => data;

        public ServiceContract.SerializationData SerializeDeserializeXml(ServiceContract.SerializationData data) => data;

        public Stream SerializeDeserializeRaw(Stream data)
        {
            MemoryStream ms = new();
            data.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }
    }
}
