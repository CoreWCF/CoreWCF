using System;

namespace CoreWCF.Primitives.Tests
{
    [XmlSerializerFormat]
    [ServiceContract]
    interface ISimpleXmlSerializerService
    {
        [OperationContract]
        string Echo(string echo);
    }
}
