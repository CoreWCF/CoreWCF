using System.CodeDom.Compiler;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace BasicNetTcp
{
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
	[System.ServiceModel.ServiceContractAttribute]
	public interface IEcho
	{

		[System.ServiceModel.OperationContractAttribute(Action = "http://tempuri.org/IEcho/Echo", ReplyAction = "http://tempuri.org/IEcho/EchoResponse")]
		string Echo(string inputValue);
	}

	[System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
	public interface IEchoChannel : IEcho, System.ServiceModel.IClientChannel
	{
	}

	[GeneratedCode("System.ServiceModel", "4.0.0.0"), DebuggerStepThrough]
	public class EchoClient : ClientBase<IEcho>, IEcho
	{
		public EchoClient()
		{
		}
		public EchoClient(string endpointConfigurationName) : base(endpointConfigurationName)
		{
		}
		public EchoClient(string endpointConfigurationName, string remoteAddress) : base(endpointConfigurationName, remoteAddress)
		{
		}
		public EchoClient(string endpointConfigurationName, EndpointAddress remoteAddress) : base(endpointConfigurationName, remoteAddress)
		{
		}
		public EchoClient(Binding binding, EndpointAddress remoteAddress) : base(binding, remoteAddress)
		{
		}
		public EchoClient(ServiceEndpoint endpoint) : base(endpoint)
		{
		}
		public string Echo(string inputValue)
		{
			return base.Channel.Echo(inputValue);
		}
	}
}
