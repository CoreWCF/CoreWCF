// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using ClientContract;

namespace Helpers
{
    public static class ClientHelper
    {
        private static readonly TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

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

        public static WSHttpBinding GetBufferedModeWSHttpBinding(SecurityMode securityMode)
        {
            var binding = new WSHttpBinding(securityMode);
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

        public class NoneSerializableStream : MemoryStream
        {
        }

        public static void PopulateStreamWithStringBytes(Stream stream, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            byte[] array = bytes;
            for (int i = 0; i < array.Length; i++)
            {
                byte value = array[i];
                stream.WriteByte(value);
            }

            stream.Position = 0L;
        }

        public static Stream GetStreamWithStringBytes(string s)
        {
            Stream stream = new NoneSerializableStream();
            PopulateStreamWithStringBytes(stream, s);
            return stream;
        }

        public static string GetStringFrom(Stream s)
        {
            StreamReader streamReader = new StreamReader(s, Encoding.UTF8);
            return streamReader.ReadToEnd();
        }

        public static MessageContractStreamNoHeader GetMessageContractStreamNoHeader(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException("input cannot bindingElement null to make GetMessageContractStreamNoHeader");
            }

            Stream streamWithStringBytes = GetStreamWithStringBytes(s);
            return new MessageContractStreamNoHeader
            {
                stream = streamWithStringBytes
            };
        }

        public static MessageContractStreamOneIntHeader GetMessageContractStreamOneIntHeader(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException("input cannot bindingElement null to make GetMessageContractStreamNoHeader");
            }

            Stream streamWithStringBytes = GetStreamWithStringBytes(s);
            return new MessageContractStreamOneIntHeader
            {
                input = streamWithStringBytes
            };
        }

        public static MessageContractStreamTwoHeaders GetMessageContractStreamTwoHeaders(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException("input cannot bindingElement null to make GetMessageContractStreamTwoHeaders");
            }
            Stream streamWithStringBytes = GetStreamWithStringBytes(s);
            return new MessageContractStreamTwoHeaders
            {
                Stream = streamWithStringBytes
            };
        }

        public static string GetStringFrom(MessageContractStreamTwoHeaders input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("MessageContractStreamTwoHeaders is null");
            }
            Stream stream = input.Stream;
            return GetStringFrom(stream);
        }

        public static string GetStringFrom(MessageContractStreamNoHeader input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("MessageContractStreamNoHeader is null");
            }
            Stream stream = input.stream;
            return GetStringFrom(stream);
        }

        public static T GetProxy<T>()
        {
            BasicHttpBinding httpBinding = GetBufferedModeBinding();
            ChannelFactory<T> channelFactory = new ChannelFactory<T>(httpBinding, new EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
            T proxy = channelFactory.CreateChannel();
            return proxy;
        }
    }
}