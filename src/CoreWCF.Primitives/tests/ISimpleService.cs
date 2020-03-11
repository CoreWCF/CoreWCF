using CoreWCF;
using System.Threading.Tasks;

[ServiceContract]
[System.ServiceModel.ServiceContract]
public interface ISimpleService
{
    [OperationContract]
    [System.ServiceModel.OperationContract]
    string Echo(string echo);
}

[ServiceContract]
[System.ServiceModel.ServiceContract]
public interface ISimpleAsyncService
{
    [OperationContract]
    [System.ServiceModel.OperationContract]
    Task<string> EchoAsync(string echo);
}

[ServiceContract(SessionMode = SessionMode.Required)]
[System.ServiceModel.ServiceContract(SessionMode = System.ServiceModel.SessionMode.Required)]
public interface ISimpleSessionService : ISimpleService
{
}
