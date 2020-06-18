using System.ServiceModel;

namespace ClientContract
{
	[ServiceContract, XmlSerializerFormat]
	public interface SCInterfaceB_1144850
	{
		[OperationContract]
		string StringMethodB(string str);
	}
}
