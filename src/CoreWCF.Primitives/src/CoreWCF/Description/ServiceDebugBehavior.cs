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
        private Uri _httpHelpPageUrl;
        private Uri _httpsHelpPageUrl;

        public bool HttpHelpPageEnabled { get; set; } = true;

        public Uri HttpHelpPageUrl
        {
            get { return _httpHelpPageUrl; }
            set
            {
                if (value != null && value.IsAbsoluteUri && value.Scheme != Uri.UriSchemeHttp)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxServiceMetadataBehaviorUrlMustBeHttpOrRelative,
                        nameof(HttpHelpPageUrl), Uri.UriSchemeHttp, value.ToString(), value.Scheme));
                }

                _httpHelpPageUrl = value;
            }
        }

        public bool HttpsHelpPageEnabled { get; set; } = true;

        public Uri HttpsHelpPageUrl
        {
            get { return _httpsHelpPageUrl; }
            set
            {
                if (value != null && value.IsAbsoluteUri && value.Scheme != Uri.UriSchemeHttps)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxServiceMetadataBehaviorUrlMustBeHttpOrRelative,
                        nameof(HttpsHelpPageUrl), Uri.UriSchemeHttps, value.ToString(), value.Scheme));
                }

                _httpsHelpPageUrl = value;
            }
        }

        [DefaultValue(false)]
        public bool IncludeExceptionDetailInFaults { get; set; } = false;

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase) { }

        void IServiceBehavior.AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            ServiceDebugBehavior param = parameters.Find<ServiceDebugBehavior>();
            if (param == null)
            {
                parameters.Add(this);
            }
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (IncludeExceptionDetailInFaults)
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

            if (!(HttpHelpPageEnabled || HttpsHelpPageEnabled))
            {
                return;
            }

            ServiceMetadataExtension mex = ServiceMetadataExtension.EnsureServiceMetadataExtension(serviceHostBase);
            SetExtensionProperties(mex, serviceHostBase);
        }

        private void SetExtensionProperties(ServiceMetadataExtension mex, ServiceHostBase host)
        {
            mex.HttpHelpPageEnabled = HttpHelpPageEnabled;
            mex.HttpHelpPageUrl = host.GetVia(Uri.UriSchemeHttp, _httpHelpPageUrl == null ? new Uri(string.Empty, UriKind.Relative) : _httpHelpPageUrl);

            mex.HttpsHelpPageEnabled = HttpsHelpPageEnabled;
            mex.HttpsHelpPageUrl = host.GetVia(Uri.UriSchemeHttps, _httpsHelpPageUrl == null ? new Uri(string.Empty, UriKind.Relative) : _httpsHelpPageUrl);
        }
    }
}
