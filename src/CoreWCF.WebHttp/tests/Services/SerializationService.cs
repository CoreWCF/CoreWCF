// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Services
{
    public class SerializationService : ServiceContract.ISerializationService
    {
        public ServiceContract.SerializationData SerializeDeserializeJson(ServiceContract.SerializationData data) => data;

        public ServiceContract.SerializationData SerializeDeserializeXml(ServiceContract.SerializationData data) => data;
    }
}
