// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Extensions.Configuration
{
    /// <summary>
    /// Thrown when a binding cannot be hydrated from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> source.
    /// </summary>
    public class BindingConfigurationException : Exception
    {
        public BindingConfigurationException(string message)
            : base(message)
        {
        }

        public BindingConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
