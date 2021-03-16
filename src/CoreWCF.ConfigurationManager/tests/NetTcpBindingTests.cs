using System;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class NetTcpBindingTests : TestBase
    {
        [Fact]
        public void NetTcpBinding_WithDefaultSetting()
        {
            string expectedName = "netTcpBindingConfig";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferSize = 65536;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);

            var xml = $@"
<configuration> 
   <system.serviceModel>         
     <bindings>         
       <netTcpBinding>
         <binding name=""{expectedName}"">                 
         </binding >
       </netTcpBinding>                             
     </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (var provider = CreateProvider(fs.Name))
                {
                    var options = provider.GetRequiredService<IConfigureOptions<ServiceModelOptions>>();
                    options.Configure(new ServiceModelOptions());
                    var settingHolder = provider.GetService<IConfigurationHolder>();

                    var expectedBinding = settingHolder.ResolveBinding(expectedName) as NetTcpBinding;
                    Assert.Equal(expectedName, expectedBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, expectedBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, expectedBinding.MaxBufferSize);
                    Assert.Equal(expectedDefaultTimeout, expectedBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, expectedBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, expectedBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, expectedBinding.ReceiveTimeout);
                    Assert.Equal(TransferMode.Buffered, expectedBinding.TransferMode);
                }
            }
        }

        [Fact]
        public void NetTcpBinding_WithCustomSetting()
        {
            string expectedName = "netTcpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);

            var xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <netTcpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""00:10:00"" >
                <readerQuotas maxDepth=""{expectedMaxDepth}"" />   
                </binding >
            </netTcpBinding>                             
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (var provider = CreateProvider(fs.Name))
                {
                    var options = provider.GetRequiredService<IConfigureOptions<ServiceModelOptions>>();
                    options.Configure(new ServiceModelOptions());
                    var settingHolder = provider.GetService<IConfigurationHolder>();

                    var expectedBinding = settingHolder.ResolveBinding(expectedName) as NetTcpBinding;
                    Assert.Equal(expectedName, expectedBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, expectedBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, expectedBinding.MaxBufferSize);
                    Assert.Equal(expectedDefaultTimeout, expectedBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, expectedBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, expectedBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, expectedBinding.ReceiveTimeout);
                    Assert.Equal(TransferMode.Buffered, expectedBinding.TransferMode);
                    Assert.Equal(expectedMaxDepth, expectedBinding.ReaderQuotas.MaxDepth);
                }
            }
        }
    }
}
