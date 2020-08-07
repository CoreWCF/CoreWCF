using CoreWCF;

namespace Services
{
	[ServiceBehavior]
	public class ServerSimplex :ServiceContract. ISimplexService
	{
		public void SimplexMethod(string msg)
		{			
		}
	}
}