namespace ServiceContract
{
	[CoreWCF.ServiceContract]
	[System.ServiceModel.ServiceContract]
	public interface IDuplexService
	{
		[CoreWCF.OperationContract]
		[System.ServiceModel.OperationContract]
		string DuplexMethod(string msg);
	}

}