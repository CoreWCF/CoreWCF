using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Helpers
{
    public static class ClientHelper
    {
        private static TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        public static NetTcpBinding GetBufferedModeBinding(SecurityMode securityMode = SecurityMode.None)
        {
            var binding = new NetTcpBinding(securityMode);
            ApplyDebugTimeouts(binding);
            return binding;
        }

        public static NetTcpBinding GetStreamedModeBinding(SecurityMode securityMode = SecurityMode.None)
        {
            var binding = new NetTcpBinding(securityMode)
            {
                TransferMode = TransferMode.Streamed
            };
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

        public static CustomBinding GetCustomClientBinding(CompressionFormat clientCompressionFormat, TransferMode transferMode)
        {
            BinaryMessageEncodingBindingElement binaryMessageEncodingElement = new BinaryMessageEncodingBindingElement();
            binaryMessageEncodingElement.CompressionFormat = clientCompressionFormat;
            TcpTransportBindingElement tranportBE = new TcpTransportBindingElement
            {
                TransferMode = transferMode,
                MaxReceivedMessageSize = int.MaxValue
            };

            CustomBinding customBinding = new CustomBinding();
            customBinding.Elements.Add(binaryMessageEncodingElement);
            customBinding.Elements.Add(tranportBE);
            return new CustomBinding(customBinding);
        }
    }
}
