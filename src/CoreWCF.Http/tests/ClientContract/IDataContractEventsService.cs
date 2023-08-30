// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;
using ServiceContract;

namespace ClientContract
{
    [ServiceContract]
    public interface IDataContractEventsService
    {
        [OperationContract()]
        void Method_All_out(DataContractEvents_All obj1, out DataContractEvents_All obj2);

        [OperationContract()]
        DataContractEvents_All Method_All_ref(ref DataContractEvents_All obj1);

        [OperationContract()]
        DataContractEvents_All Method_All(DataContractEvents_All obj2);

        [OperationContract()]
        void Method_OnSerializing_out(DataContractEvents_OnSerializing obj1, out DataContractEvents_OnSerializing obj2);

        [OperationContract()]
        DataContractEvents_OnSerializing Method_OnSerializing_ref(ref DataContractEvents_OnSerializing obj1);

        [OperationContract()]
        DataContractEvents_OnSerializing Method_OnSerializing(DataContractEvents_OnSerializing obj2);

        [OperationContract()]
        void Method_OnSerialized_out(DataContractEvents_OnSerialized obj1, out DataContractEvents_OnSerialized obj2);

        [OperationContract()]
        DataContractEvents_OnSerialized Method_OnSerialized_ref(ref DataContractEvents_OnSerialized obj1);

        [OperationContract()]
        DataContractEvents_OnSerialized Method_OnSerialized(DataContractEvents_OnSerialized obj2);

        [OperationContract()]
        void Method_OnDeserializing_out(DataContractEvents_OnDeserializing obj1, out DataContractEvents_OnDeserializing obj2);

        [OperationContract()]
        DataContractEvents_OnDeserializing Method_OnDeserializing_ref(ref DataContractEvents_OnDeserializing obj1);

        [OperationContract()]
        DataContractEvents_OnDeserializing Method_OnDeserializing(DataContractEvents_OnDeserializing obj2);

        [OperationContract()]
        void Method_OnDeserialized_out(DataContractEvents_OnDeserialized obj1, out DataContractEvents_OnDeserialized obj2);

        [OperationContract()]
        DataContractEvents_OnDeserialized Method_OnDeserialized_ref(ref DataContractEvents_OnDeserialized obj1);

        [OperationContract()]
        DataContractEvents_OnDeserialized Method_OnDeserialized(DataContractEvents_OnDeserialized obj2);
    }
}