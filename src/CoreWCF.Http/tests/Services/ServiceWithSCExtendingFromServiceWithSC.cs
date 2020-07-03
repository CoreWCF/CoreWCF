using CoreWCF;
using System;

namespace Services
{
	[ServiceContract]
	public class ServiceWithSCExtendingFromServiceWithSC : ServiceWithSC
	{
		[OperationContract]
		private string DerivedStringMethod(string str)
		{
			throw new ApplicationException("Failed:Not expected to be invoked!!");
		}
	}
}
