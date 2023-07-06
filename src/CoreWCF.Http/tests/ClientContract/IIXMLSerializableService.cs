// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel;
using ServiceContract;

namespace ClientContract
{
    [ServiceContract]
    public interface IIXMLSerializableService
    {
        [OperationContract()]
        void Method_IReadWriteXmlLotsOfData_out(IReadWriteXmlLotsOfData obj1, out IReadWriteXmlLotsOfData obj2);

        [OperationContract()]
        IReadWriteXmlLotsOfData Method_IReadWriteXmlLotsOfData_ref(ref IReadWriteXmlLotsOfData obj1);

        [OperationContract()]
        IReadWriteXmlLotsOfData Method_IReadWriteXmlLotsOfData(IReadWriteXmlLotsOfData obj1);

        [OperationContract()]
        void Method_IReadWriteXmlNestedWriteString_out(IReadWriteXmlNestedWriteString obj1, out IReadWriteXmlNestedWriteString obj2);

        [OperationContract()]
        IReadWriteXmlNestedWriteString Method_IReadWriteXmlNestedWriteString_ref(ref IReadWriteXmlNestedWriteString obj1);

        [OperationContract()]
        IReadWriteXmlNestedWriteString Method_IReadWriteXmlNestedWriteString(IReadWriteXmlNestedWriteString obj1);

        [OperationContract()]
        void Method_IReadWriteXmlWriteAttributesFromReader_out(IReadWriteXmlWriteAttributesFromReader obj1, out IReadWriteXmlWriteAttributesFromReader obj2);

        [OperationContract()]
        IReadWriteXmlWriteAttributesFromReader Method_IReadWriteXmlWriteAttributesFromReader_ref(ref IReadWriteXmlWriteAttributesFromReader obj1);

        [OperationContract()]
        IReadWriteXmlWriteAttributesFromReader Method_IReadWriteXmlWriteAttributesFromReader(IReadWriteXmlWriteAttributesFromReader obj1);

        [OperationContract()]
        void Method_IReadWriteXmlWriteName_out(IReadWriteXmlWriteName obj1, out IReadWriteXmlWriteName obj2);

        [OperationContract()]
        IReadWriteXmlWriteName Method_IReadWriteXmlWriteName_ref(ref IReadWriteXmlWriteName obj1);

        [OperationContract()]
        IReadWriteXmlWriteName Method_IReadWriteXmlWriteName(IReadWriteXmlWriteName obj1);

        [OperationContract()]
        void Method_IReadWriteXmlWriteStartAttribute_out(IReadWriteXmlWriteStartAttribute obj1, out IReadWriteXmlWriteStartAttribute obj2);

        [OperationContract()]
        IReadWriteXmlWriteStartAttribute Method_IReadWriteXmlWriteStartAttribute_ref(ref IReadWriteXmlWriteStartAttribute obj1);

        [OperationContract()]
        IReadWriteXmlWriteStartAttribute Method_IReadWriteXmlWriteStartAttribute(IReadWriteXmlWriteStartAttribute obj1);
    }
}