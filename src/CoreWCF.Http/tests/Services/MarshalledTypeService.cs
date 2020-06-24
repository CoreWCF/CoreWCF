using CoreWCF;
using ServiceContract;
using Xunit;

namespace Services
{
	[ServiceBehavior]
	//[ServiceContract(Name = "IMarshalledTypeService")]
	public class MarshalledTypeService : IMarshalledTypeService
	{
		//[OperationContract]
		public void TwoWayMethod1(MarshalledType t)
		{
			t.AddToData(100);
			Assert.Equal(200.ToString(), t.GetData().ToString());
			return;
		}

		//[OperationContract]
		public MarshalledType TwoWayMethod2()
		{
			MarshalledType t = new MarshalledType(500);
			Assert.Equal(500.ToString(), t.GetData().ToString());
			return t;
		}

		//[OperationContract]
		public MarshalledType TwoWayMethod3(MarshalledType t)
		{
			MarshalledType t1 = new MarshalledType(t.GetData() + 500);
			Assert.Equal(1000.ToString(), t1.GetData().ToString());
			return t1;
		}
	}
}
