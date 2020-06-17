using CoreWCF;

namespace ServiceContract
{
	[ServiceContract, XmlSerializerFormat]
	public interface SCInterfaceB_1144850
	{
		[OperationContract]
		string StringMethodB(string str);
	}
}
