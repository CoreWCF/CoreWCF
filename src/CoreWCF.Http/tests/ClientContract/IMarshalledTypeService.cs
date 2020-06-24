using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace ClientContract
{
	[ServiceContract]
	public interface IMarshalledTypeService
	{
		[OperationContract]
		void TwoWayMethod1(MarshalledType t);

		[OperationContract]
		MarshalledType TwoWayMethod2();

		[OperationContract]
		MarshalledType TwoWayMethod3(MarshalledType t);
	}

	[DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
	public class MarshalledType : MarshalByRefObject
	{
		[DataMember]
		public int Data;
		public int GetData()
		{
			return Data;
		}

		public MarshalledType()
		{
			Data = 100;
		}

		public MarshalledType(int m)
		{
			Data = m;
		}

		public void AddToData([In]int incr)
		{
			Data += incr;
		}

		public void ProcessData([Out] int d)
		{
			d = Data;
		}
	}
}
