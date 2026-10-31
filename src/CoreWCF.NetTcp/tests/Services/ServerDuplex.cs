using CoreWCF;

namespace Services
{
	[ServiceBehavior]
	public class ServerDuplex :ServiceContract. IDuplexService
	{		
		public string DuplexMethod(string msg)
		{
			return "duplex";
		}
	}
}
