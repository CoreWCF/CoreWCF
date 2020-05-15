using CoreWCF;
using System;

namespace Services
{
	[ServiceContract]
	public class ServiceWithSCDerivingFromSC : ServiceContract.SCInterface_1138907
	{
		public string SCStringMethod(string str)
		{
			throw new ApplicationException("Failed:Not expected to be invoked!!");
		}

		[OperationContract]
		public string ServiceWithSCStringMethod(string str)
		{
			throw new ApplicationException("Failed:Not expected to be invoked!!");
		}
	}
}
