using CoreWCF;

[XmlSerializerFormat]
[ServiceContract]
interface ISimpleXmlSerializerService
{
    [OperationContract]
    string Echo(string echo);
}
