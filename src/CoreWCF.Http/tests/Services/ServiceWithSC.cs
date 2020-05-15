using CoreWCF;

namespace Services
{
	[ServiceContract]
	public class ServiceWithSC
	{
		[OperationContract]
		public string BaseStringMethod(string str)
		{
			return str;
		}
	}
}
