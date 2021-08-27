// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    internal class BindingFactory : IBindingFactory
    {
        public Binding Create(string bindingType)
        {
            if (string.IsNullOrEmpty(bindingType))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingType));
            }

            // with reflection?
            switch (bindingType)
            {
                case "basicHttpBinding":
                    return new BasicHttpBinding();
                case "netTcpBinding":
                    return new NetTcpBinding();
                case "wsHttpBinding":
                    return new WSHttpBinding();
                case "netHttpBinding":
                    return new NetHttpBinding();
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new BindingNotFoundException());
            }
        }
    }
}
