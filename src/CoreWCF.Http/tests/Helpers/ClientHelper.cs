using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;

namespace Helpers
{
    public static class ClientHelper
    {
        private static TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        public static Binding GetBufferedModHttp1Binding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
            HttpTransportBindingElement httpTransportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
            MessageVersion messageVersion = basicHttpBinding.MessageVersion;
            MessageEncodingBindingElement encodingBindingElement = new BinaryMessageEncodingBindingElement();
            httpTransportBindingElement.TransferMode = TransferMode.Streamed;
            return new CustomBinding(new BindingElement[]
            {
                encodingBindingElement,
                httpTransportBindingElement
            })
            {
                SendTimeout = TimeSpan.FromMinutes(20.0),
                ReceiveTimeout = TimeSpan.FromMinutes(20.0),
                OpenTimeout = TimeSpan.FromMinutes(20.0),
                CloseTimeout = TimeSpan.FromMinutes(20.0)
            };
        }

        //public static Binding GetBufferedModHttp2Binding()
        //{
        //    BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
        //    HttpTransportBindingElement httpTransportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
        //    MessageVersion messageVersion = basicHttpBinding.MessageVersion;
        //    MessageEncodingBindingElement encodingBindingElement = new TextMessageEncodingBindingElement(messageVersion, Encoding.Unicode);
        //    httpTransportBindingElement.TransferMode = TransferMode.Streamed;
        //    return new CustomBinding(new BindingElement[]
        //    {
        //        encodingBindingElement,
        //        httpTransportBindingElement
        //    })
        //    {
        //        SendTimeout = TimeSpan.FromMinutes(20.0),
        //        ReceiveTimeout = TimeSpan.FromMinutes(20.0),
        //        OpenTimeout = TimeSpan.FromMinutes(20.0),
        //        CloseTimeout = TimeSpan.FromMinutes(20.0)
        //    };
        //}
        //public static Binding GetBufferedModHttp3Binding()
        //{
        //    BasicHttpBinding basicHttpBinding = new BasicHttpBinding();
        //    HttpTransportBindingElement httpTransportBindingElement = basicHttpBinding.CreateBindingElements().Find<HttpTransportBindingElement>();
        //    MessageVersion messageVersion = basicHttpBinding.MessageVersion;
        //    MessageEncodingBindingElement encodingBindingElement = new TextMessageEncodingBindingElement(messageVersion, Encoding.UTF8);
        //    httpTransportBindingElement.TransferMode = TransferMode.Streamed;
        //    return new CustomBinding(new BindingElement[]
        //    {
        //        encodingBindingElement,
        //        httpTransportBindingElement
        //    })
        //    {
        //        SendTimeout = TimeSpan.FromMinutes(20.0),
        //        ReceiveTimeout = TimeSpan.FromMinutes(20.0),
        //        OpenTimeout = TimeSpan.FromMinutes(20.0),
        //        CloseTimeout = TimeSpan.FromMinutes(20.0)
        //    };
        //}

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