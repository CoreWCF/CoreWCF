using CoreWCF;

[ServiceContract]
[System.ServiceModel.ServiceContract]
public interface ISimpleService
{
    [OperationContract]
    [System.ServiceModel.OperationContract]
    string Echo(string echo);
}

[ServiceContract(SessionMode = SessionMode.Required)]
[System.ServiceModel.ServiceContract(SessionMode = System.ServiceModel.SessionMode.Required)]
public interface ISimpleSessionService : ISimpleService
{
}
