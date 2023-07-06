// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class IXMLSerializable_789868_Service : IIXMLSerializableService
    {
        public void Method_IReadWriteXmlLotsOfData_out(IReadWriteXmlLotsOfData obj1, out IReadWriteXmlLotsOfData obj2)
        {
            obj2 = obj1;
            return;
        }

        public IReadWriteXmlLotsOfData Method_IReadWriteXmlLotsOfData_ref(ref IReadWriteXmlLotsOfData obj1)
        {
            return obj1;
        }

        public IReadWriteXmlLotsOfData Method_IReadWriteXmlLotsOfData(IReadWriteXmlLotsOfData obj1)
        {
            return obj1;
        }

        public void Method_IReadWriteXmlNestedWriteString_out(IReadWriteXmlNestedWriteString obj1, out IReadWriteXmlNestedWriteString obj2)
        {
            obj2 = obj1;
            return;
        }

        public IReadWriteXmlNestedWriteString Method_IReadWriteXmlNestedWriteString_ref(ref IReadWriteXmlNestedWriteString obj1)
        {
            return obj1;
        }

        public IReadWriteXmlNestedWriteString Method_IReadWriteXmlNestedWriteString(IReadWriteXmlNestedWriteString obj1)
        {
            return obj1;
        }

        public void Method_IReadWriteXmlWriteAttributesFromReader_out(IReadWriteXmlWriteAttributesFromReader obj1, out IReadWriteXmlWriteAttributesFromReader obj2)
        {
            obj2 = obj1;
            return;
        }

        public IReadWriteXmlWriteAttributesFromReader Method_IReadWriteXmlWriteAttributesFromReader_ref(ref IReadWriteXmlWriteAttributesFromReader obj1)
        {
            return obj1;
        }

        public IReadWriteXmlWriteAttributesFromReader Method_IReadWriteXmlWriteAttributesFromReader(IReadWriteXmlWriteAttributesFromReader obj1)
        {
            return obj1;
        }

        public void Method_IReadWriteXmlWriteName_out(IReadWriteXmlWriteName obj1, out IReadWriteXmlWriteName obj2)
        {
            obj2 = obj1;
            return;
        }

        public IReadWriteXmlWriteName Method_IReadWriteXmlWriteName_ref(ref IReadWriteXmlWriteName obj1)
        {
            return obj1;
        }

        public IReadWriteXmlWriteName Method_IReadWriteXmlWriteName(IReadWriteXmlWriteName obj1)
        {
            return obj1;
        }

        public void Method_IReadWriteXmlWriteStartAttribute_out(IReadWriteXmlWriteStartAttribute obj1, out IReadWriteXmlWriteStartAttribute obj2)
        {
            obj2 = obj1;
            return;
        }

        public IReadWriteXmlWriteStartAttribute Method_IReadWriteXmlWriteStartAttribute_ref(ref IReadWriteXmlWriteStartAttribute obj1)
        {
            return obj1;
        }

        public IReadWriteXmlWriteStartAttribute Method_IReadWriteXmlWriteStartAttribute(IReadWriteXmlWriteStartAttribute obj1)
        {
            return obj1;
        }
    }
}