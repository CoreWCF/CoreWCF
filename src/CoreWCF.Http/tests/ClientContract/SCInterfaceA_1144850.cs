using System.ServiceModel;

namespace ClientContract
{
	[ServiceContract, XmlSerializerFormat]
	public interface SCInterfaceA_1144850
	{
		[OperationContract]
		string StringMethodA(string str);
	}
}
