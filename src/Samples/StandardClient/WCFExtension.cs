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

        public static TResult WcfInvoke<TContract, TResult>(this Func<TContract, TResult> wcfAction,
            Binding binding, string url, Action<ChannelFactory<TContract>> factorySetup = null)
        {
            ChannelFactory<TContract> factory = new ChannelFactory<TContract>( binding, new EndpointAddress(url));
            factorySetup?.Invoke(factory);

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

        private static readonly TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        public static Binding ApplyDebugTimeouts(this Binding binding, TimeSpan debugTimeout = default(TimeSpan))
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                debugTimeout = default(TimeSpan) == debugTimeout ? s_debugTimeout : debugTimeout;
                binding.OpenTimeout =
                    binding.CloseTimeout =
                    binding.SendTimeout =
                    binding.ReceiveTimeout = debugTimeout;
            }
            return binding;
        }

    }
}
