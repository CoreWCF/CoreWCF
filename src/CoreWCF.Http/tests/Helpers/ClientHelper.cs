using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Helpers
{
    public static class ClientHelper
    {
        private static TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        public static CustomBinding GetBinding()
        {
            HttpTransportBindingElement httpTransportBindingElement = new BasicHttpBinding().CreateBindingElements().Find<HttpTransportBindingElement>();
            httpTransportBindingElement.TransferMode = TransferMode.Streamed;
            httpTransportBindingElement.MaxReceivedMessageSize = long.MaxValue;
            httpTransportBindingElement.MaxBufferSize = int.MaxValue;
            BinaryMessageEncodingBindingElement binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
           
            return new CustomBinding(new BindingElement[]
            {
                binaryMessageEncodingBindingElement,
                httpTransportBindingElement
            })
            {
                SendTimeout = TimeSpan.FromMinutes(12.0),
                ReceiveTimeout = TimeSpan.FromMinutes(12.0),
                OpenTimeout = TimeSpan.FromMinutes(12.0),
                CloseTimeout = TimeSpan.FromMinutes(12.0)
            };
        }

        public static Binding GetBufferedModHttpBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
            HttpTransportBindingElement transportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
            return ConfigureHttpBinding(transportBindingElement);

        }
        private static CustomBinding ConfigureHttpBinding(HttpTransportBindingElement transportBindingElement)
        {
            BinaryMessageEncodingBindingElement binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
            transportBindingElement.TransferMode = TransferMode.Streamed;
            transportBindingElement.MaxReceivedMessageSize = 2147483647L;
            transportBindingElement.MaxBufferSize = int.MaxValue;
            transportBindingElement.UseDefaultWebProxy = false;
            CustomBinding customBinding = new CustomBinding(new BindingElement[]
            {
                binaryMessageEncodingBindingElement,
                transportBindingElement
            });
            ConfigureTimeout(customBinding);
            return customBinding;
        }
        private static void ConfigureTimeout(Binding binding)
        {
            int num = 12;
            num *= 2;
            binding.SendTimeout = TimeSpan.FromMinutes((double)num);
            binding.ReceiveTimeout = TimeSpan.FromMinutes((double)num);
            binding.OpenTimeout = TimeSpan.FromMinutes(5.0);
            binding.CloseTimeout = TimeSpan.FromMinutes(5.0);
        }

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

        public static T GetProxy<T>()
        {
            var httpBinding = ClientHelper.GetBufferedModeBinding();
            ChannelFactory<T> channelFactory = new ChannelFactory<T>(httpBinding, new EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
            T proxy = channelFactory.CreateChannel();
            return proxy;
        }
    }
}
