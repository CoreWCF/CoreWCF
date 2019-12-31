using DispatcherClient;
using Helpers;
using Xunit;

namespace Contracts
{
    public class ServiceModelContractTests
    {
        [Fact]
        public static void AttributeNoPropertiesContract()
        {
            var factory = DispatcherHelper.CreateChannelFactory<ServiceModelSimpleService, IServiceModelSimpleService>();
            factory.Open();
            var channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            var echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void AttributeWithNameNamespaceActionReplyActionContract()
        {
            var factory = DispatcherHelper.CreateChannelFactory<ServiceModelSimpleService, IServiceModelServiceWithPropertiesSet>();
            factory.Open();
            var channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            var echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
   }

    [System.ServiceModel.ServiceContract]
    public interface IServiceModelSimpleService
    {
        [System.ServiceModel.OperationContract]
        string Echo(string echo);
    }

    [System.ServiceModel.ServiceContract(Name = "NotTheDefaultServiceName", Namespace = "http://tempuri.org/NotTheDefaultServiceNamespace")]
    public interface IServiceModelServiceWithPropertiesSet
    {
        [System.ServiceModel.OperationContract(Name = "NotTheDefaultOperationName", Action = "corewcf://corewcf.corewcf/OddAction", ReplyAction = "corewcf://corewcf.corewcf/OddReplyAction")]
        string Echo(string echo);
    }

    public class ServiceModelSimpleService : ServiceModelBaseService, IServiceModelSimpleService, IServiceModelServiceWithPropertiesSet { }

    public class ServiceModelBaseService
    {
        public string Echo(string echo)
        {
            return echo;
        }
    }
}
