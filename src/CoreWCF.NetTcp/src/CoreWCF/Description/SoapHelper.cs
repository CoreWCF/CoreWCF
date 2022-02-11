// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Runtime;
using WsdlNS = System.Web.Services.Description;

namespace CoreWCF.Description
{
    internal static class SoapHelper
    {
        private static readonly object s_soapVersionStateKey = typeof(Dictionary<WsdlNS.Binding, EnvelopeVersion>);

        internal static WsdlNS.SoapBinding GetOrCreateSoapBinding(WsdlEndpointConversionContext endpointContext, WsdlExporter exporter)
        {
            if (GetSoapVersionState(endpointContext.WsdlBinding, exporter) == EnvelopeVersion.None)
            {
                return null;
            }

            WsdlNS.SoapBinding existingSoapBinding = GetSoapBinding(endpointContext);
            if (existingSoapBinding != null)
            {
                return existingSoapBinding;
            }

            EnvelopeVersion version = GetSoapVersion(endpointContext.WsdlBinding);
            WsdlNS.SoapBinding soapBinding = CreateSoapBinding(version, endpointContext.WsdlBinding);
            return soapBinding;
        }

        private static WsdlNS.SoapBinding GetSoapBinding(WsdlEndpointConversionContext endpointContext)
        {
            foreach (object o in endpointContext.WsdlBinding.Extensions)
            {
                if (o is WsdlNS.SoapBinding binding)
                {
                    return binding;
                }
            }

            return null;
        }

        private static WsdlNS.SoapBinding CreateSoapBinding(EnvelopeVersion version, WsdlNS.Binding wsdlBinding)
        {
            WsdlNS.SoapBinding soapBinding = null;

            if (version == EnvelopeVersion.Soap12)
            {
                soapBinding = new WsdlNS.Soap12Binding();
            }
            else if (version == EnvelopeVersion.Soap11)
            {
                soapBinding = new WsdlNS.SoapBinding();
            }

            Fx.Assert(soapBinding != null, "EnvelopeVersion is not recognized. Please update the SoapHelper class");

            wsdlBinding.Extensions.Add(soapBinding);
            return soapBinding;
        }

        private static EnvelopeVersion GetSoapVersionState(WsdlNS.Binding wsdlBinding, WsdlExporter exporter)
        {
            object versions = null;

            if (exporter.State.TryGetValue(s_soapVersionStateKey, out versions))
            {
                if (versions != null && ((Dictionary<WsdlNS.Binding, EnvelopeVersion>)versions).ContainsKey(wsdlBinding))
                {
                    return ((Dictionary<WsdlNS.Binding, EnvelopeVersion>)versions)[wsdlBinding];
                }
            }
            return null;
        }

        private static WsdlNS.SoapAddressBinding GetSoapAddressBinding(WsdlNS.Port wsdlPort)
        {
            foreach (object o in wsdlPort.Extensions)
            {
                if (o is WsdlNS.SoapAddressBinding binding)
                {
                    return binding;
                }
            }
            return null;
        }

        internal static EnvelopeVersion GetSoapVersion(WsdlNS.Binding wsdlBinding)
        {
            foreach (object o in wsdlBinding.Extensions)
            {
                if (o is WsdlNS.SoapBinding)
                {
                    return o is WsdlNS.Soap12Binding ? EnvelopeVersion.Soap12 : EnvelopeVersion.Soap11;
                }
            }

            return EnvelopeVersion.Soap12;
        }

    }
}
