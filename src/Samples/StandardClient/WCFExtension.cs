using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace StandardClient
{
    public static class WCFExtension
    {
        public static TResult WcfInvoke<TContract, TResult>(this ChannelFactory<TContract> factory, Func<TContract, TResult> action)
        {
            TContract client = factory.CreateChannel();
            IClientChannel clientInstance = ((IClientChannel)client);

            try
            {
                TResult result = action(client);
                clientInstance.Close();
                return result;
            }
            catch (CommunicationException)
            {
                clientInstance.Abort();
                throw;
            }
            catch (TimeoutException)
            {
                clientInstance.Abort();
                throw;
            }
        }

        public static TResult WcfInvoke<TContract, TResult>(this Func<TContract, TResult> wcfAction, Binding binding, string url)
        {
            var factory = new ChannelFactory<TContract>( binding, new EndpointAddress(url));
            factory.Open();
            try
            {
                return factory.WcfInvoke(wcfAction);
            }
            finally
            {
                factory.Close();
            }
        }
    }
}
