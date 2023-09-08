// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Helpers
{
    public static class ClientHelper
    {
        private static readonly TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        public static UnixDomainSocketBinding GetBufferedModeBinding(SecurityMode securityMode = SecurityMode.None)
        {
            var binding = new UnixDomainSocketBinding(securityMode);
            ApplyDebugTimeouts(binding);
            return binding;
        }

        public static UnixDomainSocketBinding GetStreamedModeBinding(SecurityMode securityMode = SecurityMode.None)
        {
            var binding = new UnixDomainSocketBinding(securityMode)
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
    }
}
