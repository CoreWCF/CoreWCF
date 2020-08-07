using System.IO;
using System.Threading.Tasks;
using CoreWCF;

namespace ServiceContract
{
	[CoreWCF.ServiceContract]
	[System.ServiceModel.ServiceContract]
	public interface IMultiplexService
	{
		[CoreWCF.OperationContract]
		[System.ServiceModel.OperationContract]
		void MultiplexMethod(string msg);
	}
}