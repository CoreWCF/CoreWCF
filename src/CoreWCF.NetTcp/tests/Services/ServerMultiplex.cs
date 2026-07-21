using CoreWCF;

namespace Services
{
	[ServiceBehavior]
	public class ServerMultiplex : ServiceContract.IMultiplexService
	{		
		public void MultiplexMethod(string msg)
		{			
		}
	}
}
