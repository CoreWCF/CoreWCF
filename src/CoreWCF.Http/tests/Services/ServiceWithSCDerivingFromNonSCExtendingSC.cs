using CoreWCF;
using ServiceContract;
using System;

namespace Services
{
	[ServiceContract]
	public class ServiceWithSCDerivingFromNonSCExtendingSC : NonSCExtendingSC_1138907, SCInterface_1138907
	{
		public string SCStringMethod(string str)
		{
			throw new ApplicationException("Failed:Not expected to be invoked!!");
		}

		public string NonSCStringMethod(string str)
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
