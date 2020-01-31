using DispatcherClient;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace ErrorHandling
{
    public class ExceptionHandling
    {
        [Fact]
        public static void ServiceThrowsTimeoutException()
        {
            var factory = DispatcherHelper.CreateChannelFactory<ThrowingService, ISimpleService>(
                (services) => 
                {
                    services.AddSingleton(new ThrowingService(new TimeoutException()));
                });
            factory.Open();
            var channel = factory.CreateChannel();
            Exception exceptionThrown = null;
            try
            {
                var echo = channel.Echo("hello");
            }
            catch(Exception e)
            {
                exceptionThrown = e;
            }

            Assert.NotNull(exceptionThrown);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    public class ThrowingService : ISimpleService
    {
        private Exception _exception;

        public ThrowingService(Exception exception)
        {
            _exception = exception;
        }
        public string Echo(string echo)
        {
            throw _exception;
        }
    }
}
