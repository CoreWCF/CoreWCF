using CoreWCF;

[ServiceContract]
public interface ISimpleService
{
    [OperationContract]
    string Echo(string echo);
}
