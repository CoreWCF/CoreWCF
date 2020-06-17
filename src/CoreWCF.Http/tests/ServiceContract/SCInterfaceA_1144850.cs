using CoreWCF;

namespace ServiceContract
{
	[ServiceContract, XmlSerializerFormat]
	public interface SCInterfaceA_1144850
	{
		[OperationContract]
		string StringMethodA(string str);
	}
}
