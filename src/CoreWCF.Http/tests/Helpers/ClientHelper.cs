using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Helpers
{
    public static class ClientHelper
    {
        private static TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        public static BasicHttpBinding GetBufferedModeBinding()
        {
            var binding = new BasicHttpBinding();
            ApplyDebugTimeouts(binding);
            return binding;
        }

        public static BasicHttpsBinding GetBufferedModeHttpsBinding()
        {
            var binding = new BasicHttpsBinding();
            ApplyDebugTimeouts(binding);
            return binding;
        }

        public static BasicHttpBinding GetStreamedModeBinding()
        {
            var binding = new BasicHttpBinding
            {
                TransferMode = TransferMode.Streamed
            };
            ApplyDebugTimeouts(binding);
            return binding;
        }

        public static NetHttpBinding GetBufferedModeWebSocketBinding()
        {
            var binding = new NetHttpBinding();
            binding.WebSocketSettings.TransportUsage = WebSocketTransportUsage.Always;
            ApplyDebugTimeouts(binding);
            return binding;
        }

        public static NetHttpBinding GetStreamedModeWebSocketBinding()
        {
            var binding = new NetHttpBinding
            {
                TransferMode = TransferMode.Streamed
            };
            binding.WebSocketSettings.TransportUsage = WebSocketTransportUsage.Always;
            ApplyDebugTimeouts(binding);
            return binding;
        }

        private static void ApplyDebugTimeouts(Binding binding)
        {
            if (Debugger.IsAttached)
            {
                binding.OpenTimeout =
                    binding.CloseTimeout =
                    binding.SendTimeout =
                    binding.ReceiveTimeout = s_debugTimeout;
            }
        }

        public static Binding GetBasicHttpBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
            basicHttpBinding.TransferMode = TransferMode.StreamedResponse;
            basicHttpBinding.MaxReceivedMessageSize = Int32.MaxValue;
            basicHttpBinding.SendTimeout = TimeSpan.FromMinutes(3);
            basicHttpBinding.ReceiveTimeout = TimeSpan.FromMinutes(3);
            return basicHttpBinding;
        }

        public static Binding GetCustomBinding()
        {
            HttpTransportBindingElement httpBE = new HttpTransportBindingElement();
            httpBE.MaxReceivedMessageSize = int.MaxValue;
            httpBE.TransferMode = TransferMode.StreamedResponse;
            CustomBinding binding = new CustomBinding(new TextMessageEncodingBindingElement(), httpBE)
            {
                SendTimeout = TimeSpan.FromMinutes(3),
                ReceiveTimeout = TimeSpan.FromMinutes(3)
            };
            return binding;
        }

        public static T GetProxy<T>()
        {
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            ChannelFactory<T> channelFactory = new ChannelFactory<T>(httpBinding, new EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
            T proxy = channelFactory.CreateChannel();
            return proxy;
        }
    }
}