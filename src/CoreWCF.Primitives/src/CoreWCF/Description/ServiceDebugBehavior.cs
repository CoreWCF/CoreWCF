// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;

namespace CoreWCF.Description
{
    public class ServiceDebugBehavior : IServiceBehavior
    {
        bool includeExceptionDetailInFaults = false;

        bool httpHelpPageEnabled = true;
        Uri httpHelpPageUrl;

        bool httpsHelpPageEnabled = true;
        Uri httpsHelpPageUrl;

        Binding httpHelpPageBinding;
        Binding httpsHelpPageBinding;

        public bool HttpHelpPageEnabled
        {
            get { return this.httpHelpPageEnabled; }
            set { this.httpHelpPageEnabled = value; }
        }

        public Uri HttpHelpPageUrl
        {
            get { return this.httpHelpPageUrl; }
            set
            {
                if (value != null && value.IsAbsoluteUri && value.Scheme != Uri.UriSchemeHttp)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxServiceMetadataBehaviorUrlMustBeHttpOrRelative,
                        "HttpHelpPageUrl", Uri.UriSchemeHttp, value.ToString(), value.Scheme));
                }
                this.httpHelpPageUrl = value;
            }
        }

        public bool HttpsHelpPageEnabled
        {
            get { return this.httpsHelpPageEnabled; }
            set { this.httpsHelpPageEnabled = value; }
        }

        public Uri HttpsHelpPageUrl
        {
            get { return this.httpsHelpPageUrl; }
            set
            {
                if (value != null && value.IsAbsoluteUri && value.Scheme != Uri.UriSchemeHttps)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxServiceMetadataBehaviorUrlMustBeHttpOrRelative,
                        "HttpsHelpPageUrl", Uri.UriSchemeHttps, value.ToString(), value.Scheme));
                }
                this.httpsHelpPageUrl = value;
            }
        }

        //public Binding HttpHelpPageBinding
        //{
        //    get { return this.httpHelpPageBinding; }
        //    set
        //    {
        //        if (value != null)
        //        {
        //            if (!value.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxBindingSchemeDoesNotMatch,
        //                    value.Scheme, value.GetType().ToString(), Uri.UriSchemeHttp));
        //            }
        //            CustomBinding customBinding = new CustomBinding(value);
        //            TextMessageEncodingBindingElement textMessageEncodingBindingElement = customBinding.Elements.Find<TextMessageEncodingBindingElement>();
        //            if (textMessageEncodingBindingElement != null && !textMessageEncodingBindingElement.MessageVersion.IsMatch(MessageVersion.None))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxIncorrectMessageVersion,
        //                    textMessageEncodingBindingElement.MessageVersion.ToString(), MessageVersion.None.ToString()));
        //            }
        //            HttpTransportBindingElement httpTransportBindingElement = customBinding.Elements.Find<HttpTransportBindingElement>();
        //            if (httpTransportBindingElement != null)
        //            {
        //                httpTransportBindingElement.Method = "GET";
        //            }
        //            this.httpHelpPageBinding = customBinding;
        //        }
        //    }
        //}

        //public Binding HttpsHelpPageBinding
        //{
        //    get { return this.httpsHelpPageBinding; }
        //    set
        //    {
        //        if (value != null)
        //        {
        //            if (!value.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxBindingSchemeDoesNotMatch,
        //                    value.Scheme, value.GetType().ToString(), Uri.UriSchemeHttps));
        //            }
        //            CustomBinding customBinding = new CustomBinding(value);
        //            TextMessageEncodingBindingElement textMessageEncodingBindingElement = customBinding.Elements.Find<TextMessageEncodingBindingElement>();
        //            if (textMessageEncodingBindingElement != null && !textMessageEncodingBindingElement.MessageVersion.IsMatch(MessageVersion.None))
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxIncorrectMessageVersion,
        //                    textMessageEncodingBindingElement.MessageVersion.ToString(), MessageVersion.None.ToString()));
        //            }
        //            HttpsTransportBindingElement httpsTransportBindingElement = customBinding.Elements.Find<HttpsTransportBindingElement>();
        //            if (httpsTransportBindingElement != null)
        //            {
        //                httpsTransportBindingElement.Method = "GET";
        //            }
        //            this.httpsHelpPageBinding = customBinding;
        //        }
        //    }
        //}

        [DefaultValue(false)]
        public bool IncludeExceptionDetailInFaults
        {
            get { return this.includeExceptionDetailInFaults; }
            set { this.includeExceptionDetailInFaults = value; }
        }

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("parameters");
            }

            ServiceDebugBehavior param = parameters.Find<ServiceDebugBehavior>();
            if (param == null)
            {
                parameters.Add(this);
            }
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (this.includeExceptionDetailInFaults)
            {
                for (int i = 0; i < serviceHostBase.ChannelDispatchers.Count; i++)
                {
                    ChannelDispatcher channelDispatcher = serviceHostBase.ChannelDispatchers[i] as ChannelDispatcher;
                    if (channelDispatcher != null)
                    {
                        channelDispatcher.IncludeExceptionDetailInFaults = true;
                    }
                }
            }

            if (!(this.httpHelpPageEnabled || this.httpsHelpPageEnabled))
                return;

            ServiceMetadataExtension mex = ServiceMetadataExtension.EnsureServiceMetadataExtension(description, serviceHostBase);
            SetExtensionProperties(mex, serviceHostBase);
        }

        private void SetExtensionProperties(ServiceMetadataExtension mex, ServiceHostBase host)
        {
            mex.HttpHelpPageEnabled = this.httpHelpPageEnabled;
            mex.HttpHelpPageUrl = host.GetVia(Uri.UriSchemeHttp, this.httpHelpPageUrl == null ? new Uri(string.Empty, UriKind.Relative) : this.httpHelpPageUrl);
            //mex.HttpHelpPageBinding = this.HttpHelpPageBinding;

            mex.HttpsHelpPageEnabled = this.httpsHelpPageEnabled;
            mex.HttpsHelpPageUrl = host.GetVia(Uri.UriSchemeHttps, this.httpsHelpPageUrl == null ? new Uri(string.Empty, UriKind.Relative) : this.httpsHelpPageUrl);
            //mex.HttpsHelpPageBinding = this.HttpsHelpPageBinding;
        }

        static void TraceWarning(Uri address, string urlProperty, string enabledProperty)
        {
            //if (DiagnosticUtility.ShouldTraceInformation)
            //{
            //    Hashtable h = new Hashtable(2)
            //    {
            //        { enabledProperty, "true" },
            //        { urlProperty, (address == null) ? string.Empty : address.ToString() }
            //    };
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.WarnHelpPageEnabledNoBaseAddress,
            //        SR.GetString(SR.TraceCodeWarnHelpPageEnabledNoBaseAddress), new DictionaryTraceRecord(h), null, null);
            //}
        }
    }
}
