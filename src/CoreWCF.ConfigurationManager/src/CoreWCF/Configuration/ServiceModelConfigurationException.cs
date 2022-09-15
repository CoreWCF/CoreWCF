// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF.Configuration
{
    [Serializable]
    internal class ServiceModelConfigurationException : Exception
    {
        public ServiceModelConfigurationException()
        {
        }

        public ServiceModelConfigurationException(string message)
            : base(message)
        {
        }

        public ServiceModelConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ServiceModelConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
