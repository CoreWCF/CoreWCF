// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using WsdlNS = System.Web.Services.Description;

namespace CoreWCF.Description
{
    // the description/metadata "mix-in"
    public class ServiceMetadataExtension : IExtension<ServiceHostBase>
    {
        private const string BaseAddressPattern = "{%BaseAddress%}";
        private static readonly Uri s_emptyUri = new Uri(string.Empty, UriKind.Relative);
        private static readonly Type[] s_httpGetSupportedChannels = new Type[] { typeof(IReplyChannel), };
        private MetadataSet _metadata;
        private WsdlNS.ServiceDescription _singleWsdl;
        private bool _isInitialized = false;
        private bool _isSingleWsdlInitialized = false;
        private ServiceHostBase _owner;
        private readonly object _syncRoot = new object();
        private readonly object _singleWsdlSyncRoot = new object();

        public ServiceMetadataExtension()
            : this(null)
        {
        }

        internal ServiceMetadataExtension(ServiceMetadataBehavior.MetadataExtensionInitializer initializer)
        {
            Initializer = initializer;
        }

        internal ServiceMetadataBehavior.MetadataExtensionInitializer Initializer { get; set; }

        public MetadataSet Metadata
        {
            get
            {
                EnsureInitialized();
                return _metadata;
            }
        }

        public WsdlNS.ServiceDescription SingleWsdl
        {
            get
            {
                EnsureSingleWsdlInitialized();
                return _singleWsdl;
            }
        }

        internal Uri ExternalMetadataLocation { get; set; }

        internal bool MexEnabled { get; set; } = false;

        internal bool HttpGetEnabled { get; set; } = false;

        internal bool HttpsGetEnabled { get; set; } = false;

        internal bool HelpPageEnabled => HttpHelpPageEnabled || HttpsHelpPageEnabled;

        internal bool MetadataEnabled => MexEnabled || HttpGetEnabled || HttpsGetEnabled;

        internal bool HttpHelpPageEnabled { get; set; } = false;

        internal bool HttpsHelpPageEnabled { get; set; } = false;

        internal Uri MexUrl { get; set; }

        internal Uri HttpGetUrl { get; set; }

        internal Uri HttpsGetUrl { get; set; }

        internal Uri HttpHelpPageUrl { get; set; }

        internal Uri HttpsHelpPageUrl { get; set; }

        internal Binding HttpHelpPageBinding { get; set; }

        internal Binding HttpsHelpPageBinding { get; set; }

        internal Binding HttpGetBinding { get; set; }

        internal Binding HttpsGetBinding { get; set; }

        internal bool UpdateAddressDynamically { get; set; }

        // This dictionary should not be mutated after open
        internal IDictionary<string, int> UpdatePortsByScheme { get; set; }

        internal static bool TryGetHttpHostAndPort(Uri listenUri, HttpRequest httpRequest, out string host, out int port)
        {
            host = null;
            port = 0;

            // Get the host header
            HostString hostString = httpRequest.Host;
            if (!hostString.HasValue)
            {
                return false;
            }

            host = hostString.Host;
            if (hostString.Port.HasValue)
            {
                port = hostString.Port.Value;
            }
            else
            {
                string hostUriString = string.Concat(listenUri.Scheme, "://", host);
                Uri hostUri;
                if (!Uri.TryCreate(hostUriString, UriKind.Absolute, out hostUri))
                {
                    return false;
                }

                port = hostUri.Port;
            }

            return true;
        }

        internal Action<IApplicationBuilder> ConfigureWith(Uri baseAddress)
        {
            HttpGetImpl impl = new HttpGetImpl(this, baseAddress);
            return impl.Configure;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                lock (_syncRoot)
                {
                    if (!_isInitialized)
                    {
                        if (Initializer != null)
                        {
                            // the following call will initialize this
                            // it will use the Metadata property to do the initialization
                            // this will call back into this method, but exit because isInitialized is set.
                            // if other threads try to call these methods, they will block on the lock
                            _metadata = Initializer.GenerateMetadata();
                        }

                        if (_metadata == null)
                        {
                            _metadata = new MetadataSet();
                        }

                        Thread.MemoryBarrier();

                        _isInitialized = true;
                        Initializer = null;
                    }
                }
            }
        }

        private void EnsureSingleWsdlInitialized()
        {
            if (!_isSingleWsdlInitialized)
            {
                lock (_singleWsdlSyncRoot)
                {
                    if (!_isSingleWsdlInitialized)
                    {
                        // Could throw NotSupportedException if multiple contract namespaces. Let the exception propagate to the dispatcher and show up on the html error page
                        _singleWsdl = WsdlHelper.GetSingleWsdl(Metadata);
                        _isSingleWsdlInitialized = true;
                    }
                }
            }
        }

        void IExtension<ServiceHostBase>.Attach(ServiceHostBase owner)
        {
            if (owner == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(owner)));

            if (_owner != null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.TheServiceMetadataExtensionInstanceCouldNot2_0));

            if (owner.State != CommunicationState.Created && owner.State != CommunicationState.Opening)
            {
                throw new InvalidOperationException(owner.State.ToString());
            }

            _owner = owner;
        }

        void IExtension<ServiceHostBase>.Detach(ServiceHostBase owner)
        {
            if (owner == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(owner));

            if (_owner == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.TheServiceMetadataExtensionInstanceCouldNot3_0));

            if (_owner != owner)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(owner), SR.TheServiceMetadataExtensionInstanceCouldNot4_0);

            if (owner.State != CommunicationState.Created && owner.State != CommunicationState.Opening)
            {
                throw new InvalidOperationException(owner.State.ToString());
            }

            _owner = null;
        }

        internal static ServiceMetadataExtension EnsureServiceMetadataExtension(ServiceDescription description, ServiceHostBase host)
        {
            ServiceMetadataExtension mex = host.Extensions.Find<ServiceMetadataExtension>();
            if (mex == null)
            {
                mex = new ServiceMetadataExtension();
                host.Extensions.Add(mex);
            }

            return mex;
        }

        //internal ChannelDispatcher EnsureGetDispatcher(Uri listenUri)
        //{
        //    ChannelDispatcher channelDispatcher = FindGetDispatcher(listenUri);

        //    if (channelDispatcher == null)
        //    {
        //        channelDispatcher = CreateGetDispatcher(listenUri);
        //        owner.ChannelDispatchers.Add(channelDispatcher);
        //    }

        //    return channelDispatcher;
        //}

        //internal ChannelDispatcher EnsureGetDispatcher(Uri listenUri, bool isServiceDebugBehavior)
        //{
        //    ChannelDispatcher channelDispatcher = FindGetDispatcher(listenUri);

        //    Binding binding;
        //    if (channelDispatcher == null)
        //    {
        //        if (listenUri.Scheme == Uri.UriSchemeHttp)
        //        {
        //            if (isServiceDebugBehavior)
        //            {
        //                binding = httpHelpPageBinding ?? MetadataExchangeBindings.HttpGet;
        //            }
        //            else
        //            {
        //                binding = httpGetBinding ?? MetadataExchangeBindings.HttpGet;
        //            }
        //        }
        //        else if (listenUri.Scheme == Uri.UriSchemeHttps)
        //        {
        //            if (isServiceDebugBehavior)
        //            {
        //                binding = httpsHelpPageBinding ?? MetadataExchangeBindings.HttpsGet;
        //            }
        //            else
        //            {
        //                binding = httpsGetBinding ?? MetadataExchangeBindings.HttpsGet;
        //            }
        //        }
        //        else
        //        {
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SFxGetChannelDispatcherDoesNotSupportScheme, typeof(ChannelDispatcher).Name, Uri.UriSchemeHttp, Uri.UriSchemeHttps)));
        //        }
        //        channelDispatcher = CreateGetDispatcher(listenUri, binding);
        //        owner.ChannelDispatchers.Add(channelDispatcher);
        //    }

        //    return channelDispatcher;
        //}

        //private ChannelDispatcher FindGetDispatcher(Uri listenUri)
        //{
        //    foreach (ChannelDispatcherBase channelDispatcherBase in owner.ChannelDispatchers)
        //    {
        //        ChannelDispatcher channelDispatcher = channelDispatcherBase as ChannelDispatcher;
        //        if (channelDispatcher != null && channelDispatcher.Listener.Uri == listenUri)
        //        {
        //            if (channelDispatcher.Endpoints.Count == 1 &&
        //                channelDispatcher.Endpoints[0].DispatchRuntime.SingletonInstanceContext != null &&
        //                channelDispatcher.Endpoints[0].DispatchRuntime.SingletonInstanceContext.UserObject is HttpGetImpl)
        //            {
        //                return channelDispatcher;
        //            }
        //        }
        //    }
        //    return null;
        //}

        //private ChannelDispatcher CreateGetDispatcher(Uri listenUri)
        //{
        //    if (listenUri.Scheme == Uri.UriSchemeHttp)
        //    {
        //        return CreateGetDispatcher(listenUri, MetadataExchangeBindings.HttpGet);
        //    }
        //    else if (listenUri.Scheme == Uri.UriSchemeHttps)
        //    {
        //        return CreateGetDispatcher(listenUri, MetadataExchangeBindings.HttpsGet);
        //    }
        //    else
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SFxGetChannelDispatcherDoesNotSupportScheme, typeof(ChannelDispatcher).Name, Uri.UriSchemeHttp, Uri.UriSchemeHttps)));
        //    }
        //}

        //private ChannelDispatcher CreateGetDispatcher(Uri listenUri, Binding binding)
        //{
        //    EndpointAddress address = new EndpointAddress(listenUri);
        //    Uri listenUriBaseAddress = listenUri;
        //    string listenUriRelativeAddress = string.Empty;

        //    //Set up binding parameter collection 
        //    BindingParameterCollection parameters = owner.GetBindingParameters();
        //    AspNetEnvironment.Current.AddMetadataBindingParameters(listenUriBaseAddress, owner.Description.Behaviors, parameters);

        //    // find listener for HTTP GET
        //    IChannelListener listener = null;
        //    if (binding.CanBuildChannelListener<IReplyChannel>(parameters))
        //    {
        //        listener = binding.BuildChannelListener<IReplyChannel>(listenUriBaseAddress, listenUriRelativeAddress, parameters);
        //    }
        //    else
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.SFxBindingNotSupportedForMetadataHttpGet));
        //    }

        //    //create dispatchers
        //    ChannelDispatcher channelDispatcher = new ChannelDispatcher(listener, HttpGetImpl.MetadataHttpGetBinding, binding);
        //    channelDispatcher.MessageVersion = binding.MessageVersion;
        //    EndpointDispatcher dispatcher = new EndpointDispatcher(address, HttpGetImpl.ContractName, HttpGetImpl.ContractNamespace, true);

        //    //Add operation
        //    DispatchOperation operationDispatcher = new DispatchOperation(dispatcher.DispatchRuntime, HttpGetImpl.GetMethodName, HttpGetImpl.RequestAction, HttpGetImpl.ReplyAction);
        //    operationDispatcher.Formatter = MessageOperationFormatter.Instance;
        //    MethodInfo methodInfo = typeof(IHttpGetMetadata).GetMethod(HttpGetImpl.GetMethodName);
        //    operationDispatcher.Invoker = new SyncMethodInvoker(methodInfo);
        //    dispatcher.DispatchRuntime.Operations.Add(operationDispatcher);

        //    //wire up dispatchers
        //    HttpGetImpl impl = new HttpGetImpl(this, listener.Uri);
        //    dispatcher.DispatchRuntime.SingletonInstanceContext = new InstanceContext(owner, impl, false);
        //    dispatcher.DispatchRuntime.MessageInspectors.Add(impl);
        //    channelDispatcher.Endpoints.Add(dispatcher);
        //    dispatcher.ContractFilter = new MatchAllMessageFilter();
        //    dispatcher.FilterPriority = 0;
        //    dispatcher.DispatchRuntime.InstanceContextProvider = InstanceContextProviderBase.GetProviderForMode(InstanceContextMode.Single, dispatcher.DispatchRuntime);
        //    channelDispatcher.ServiceThrottle = owner.ServiceThrottle;

        //    ServiceDebugBehavior sdb = owner.Description.Behaviors.Find<ServiceDebugBehavior>();
        //    if (sdb != null)
        //        channelDispatcher.IncludeExceptionDetailInFaults |= sdb.IncludeExceptionDetailInFaults;

        //    ServiceBehaviorAttribute sba = owner.Description.Behaviors.Find<ServiceBehaviorAttribute>();
        //    if (sba != null)
        //        channelDispatcher.IncludeExceptionDetailInFaults |= sba.IncludeExceptionDetailInFaults;


        //    return channelDispatcher;
        //}

        private WriteFilter GetWriteFilter(HttpRequest httpRequest, Uri listenUri, bool removeBaseAddress)
        {
            WriteFilter result = null;
            if (UpdateAddressDynamically)
            {
                // Update address dynamically based on the request URI
                result = GetDynamicAddressWriter(httpRequest, listenUri, removeBaseAddress);
            }

            if (result == null)
            {
                // Just use the statically known listen URI
                if (removeBaseAddress)
                {
                    result = new LocationUpdatingWriter(BaseAddressPattern, null);
                }
                else
                {
                    result = new LocationUpdatingWriter(BaseAddressPattern, listenUri.ToString());
                }
            }

            return result;
        }

        WriteFilter GetWriteFilter(Message request, Uri listenUri, bool removeBaseAddress)
        {
            WriteFilter result = null;
            if (UpdateAddressDynamically)
            {
                // Update address dynamically based on the request URI
                result = GetDynamicAddressWriter(request, listenUri, removeBaseAddress);
            }
            if (result == null)
            {
                // Just use the statically known listen URI
                if (removeBaseAddress)
                {
                    result = new LocationUpdatingWriter(BaseAddressPattern, null);
                }
                else
                {
                    result = new LocationUpdatingWriter(BaseAddressPattern, listenUri.ToString());
                }
            }
            return result;
        }

        private DynamicAddressUpdateWriter GetDynamicAddressWriter(HttpRequest httpRequest, Uri listenUri, bool removeBaseAddress)
        {
            string requestHost;
            int requestPort;
            if (!TryGetHttpHostAndPort(listenUri, httpRequest, out requestHost, out requestPort))
            {
                return null;
            }

            // Perf optimization: don't do dynamic update if it would be a no-op.
            // Ordinal string comparison is okay; it just means we don't get the perf optimization
            // if the listen host and request host are case-insensitively equal.
            if (requestHost == listenUri.Host &&
                requestPort == listenUri.Port &&
                (UpdatePortsByScheme == null || UpdatePortsByScheme.Count == 0))
            {
                return null;
            }
            return new DynamicAddressUpdateWriter(
                listenUri, requestHost, requestPort, UpdatePortsByScheme, removeBaseAddress);
        }

        DynamicAddressUpdateWriter GetDynamicAddressWriter(Message request, Uri listenUri, bool removeBaseAddress)
        {
            string requestHost;
            int requestPort;
            HttpContext context = null;
            if (request.Properties.TryGetValue("Microsoft.AspNetCore.Http.HttpContext", out object contextObj))
            {
                context = contextObj as HttpContext;
            }

            if (context==null || !TryGetHttpHostAndPort(listenUri, context.Request, out requestHost, out requestPort))
            {
                if (request.Headers.To == null)
                {
                    return null;
                }
                requestHost = request.Headers.To.Host;
                requestPort = request.Headers.To.Port;
            }

            // Perf optimization: don't do dynamic update if it would be a no-op.
            // Ordinal string comparison is okay; it just means we don't get the perf optimization
            // if the listen host and request host are case-insensitively equal.
            if (requestHost == listenUri.Host &&
                requestPort == listenUri.Port &&
                (UpdatePortsByScheme == null || UpdatePortsByScheme.Count == 0))
            {
                return null;
            }
            return new DynamicAddressUpdateWriter(
                listenUri, requestHost, requestPort, UpdatePortsByScheme, removeBaseAddress);
        }

        internal class MetadataBindingParameter { }

        internal class WSMexImpl : IMetadataExchange
        {
            internal const string MetadataMexBinding = "ServiceMetadataBehaviorMexBinding";
            internal const string ContractName = MetadataStrings.WSTransfer.Name;
            internal const string ContractNamespace = MetadataStrings.WSTransfer.Namespace;
            internal const string GetMethodName = "Get";
            internal const string RequestAction = MetadataStrings.WSTransfer.GetAction;
            internal const string ReplyAction = MetadataStrings.WSTransfer.GetResponseAction;
            private ServiceMetadataExtension parent;
            private MetadataSet metadataLocationSet;
            private TypedMessageConverter converter;
            private Uri listenUri;

            internal WSMexImpl(ServiceMetadataExtension parent, bool isListeningOnHttps, Uri listenUri)
            {
                this.parent = parent;
                this.IsListeningOnHttps = isListeningOnHttps;
                this.listenUri = listenUri;

                if (this.parent.ExternalMetadataLocation != null && this.parent.ExternalMetadataLocation != s_emptyUri)
                {
                    metadataLocationSet = new MetadataSet();
                    string location = GetLocationToReturn();
                    MetadataSection metadataLocationSection = new MetadataSection(MetadataSection.ServiceDescriptionDialect, null, new MetadataLocation(location));
                    metadataLocationSet.MetadataSections.Add(metadataLocationSection);
                }
            }

            internal bool IsListeningOnHttps { get; set; }

            private string GetLocationToReturn()
            {
                Fx.Assert(parent.ExternalMetadataLocation != null, "");
                Uri location = parent.ExternalMetadataLocation;

                if (!location.IsAbsoluteUri)
                {
                    Uri httpAddr = parent._owner.GetVia(Uri.UriSchemeHttp, location);
                    Uri httpsAddr = parent._owner.GetVia(Uri.UriSchemeHttps, location);

                    if (IsListeningOnHttps && httpsAddr != null)
                    {
                        location = httpsAddr;
                    }
                    else if (httpAddr != null)
                    {
                        location = httpAddr;
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(parent.ExternalMetadataLocation), SR.Format(SR.SFxBadMetadataLocationNoAppropriateBaseAddress, parent.ExternalMetadataLocation.OriginalString));
                    }
                }

                return location.ToString();
            }

            private MetadataSet GatherMetadata(string dialect, string identifier)
            {
                if (metadataLocationSet != null)
                {
                    return metadataLocationSet;
                }
                else
                {
                    MetadataSet metadataSet = new MetadataSet();
                    foreach (MetadataSection document in parent.Metadata.MetadataSections)
                    {
                        if ((dialect == null || dialect == document.Dialect) &&
                            (identifier == null || identifier == document.Identifier))
                            metadataSet.MetadataSections.Add(document);
                    }

                    return metadataSet;
                }
            }

            public Message Get(Message request)
            {
                GetResponse response = new GetResponse();
                response.Metadata = GatherMetadata(null, null);

                response.Metadata.WriteFilter = parent.GetWriteFilter(request, listenUri, true);

                if (converter == null)
                    converter = TypedMessageConverter.Create(typeof(GetResponse), ReplyAction);

                return converter.ToMessage(response, request.Version);
            }

            public Task<Message> GetAsync(Message request)
            {
                return Task.FromException<Message>(DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException()));
            }
        }

        internal class HttpGetImpl
        {
            private const string DiscoToken = "disco token";
            private const string DiscoQueryString = "disco";
            private const string WsdlQueryString = "wsdl";
            private const string XsdQueryString = "xsd";
            private const string SingleWsdlQueryString = "singleWsdl";
            private const string HtmlContentType = "text/html; charset=UTF-8";
            private const string XmlContentType = "text/xml; charset=UTF-8";
            private const int closeTimeoutInSeconds = 90;
            private const int MaxQueryStringChars = 2048;

            internal const string MetadataHttpGetBinding = "ServiceMetadataBehaviorHttpGetBinding";
            internal const string ContractName = "IHttpGetHelpPageAndMetadataContract";
            internal const string ContractNamespace = "http://schemas.microsoft.com/2006/04/http/metadata";
            internal const string GetMethodName = "Get";
            internal const string RequestAction = "*"; // MessageHeaders.WildcardAction;
            internal const string ReplyAction = "*"; // MessageHeaders.WildcardAction;
            internal const string HtmlBreak = "<BR/>";
            private static string[] NoQueries = new string[0];
            private ServiceMetadataExtension parent;
            private object sync = new object();
            private InitializationData initData;
            private Uri listenUri;

            internal HttpGetImpl(ServiceMetadataExtension parent, Uri listenUri)
            {
                this.parent = parent;
                this.listenUri = listenUri;
                GetWsdlEnabled = parent.HttpsGetEnabled || parent.HttpGetEnabled;
            }

            public bool HelpPageEnabled { get; set; } = false;
            public bool GetWsdlEnabled { get; set; }

            private InitializationData GetInitData()
            {
                if (initData == null)
                {
                    lock (sync)
                    {
                        if (initData == null)
                        {
                            initData = InitializationData.InitializeFrom(parent);
                        }
                    }
                }
                return initData;
            }

            private string FindWsdlReference(DynamicAddressUpdateWriter addressUpdater)
            {
                if (parent.ExternalMetadataLocation == null || parent.ExternalMetadataLocation == s_emptyUri)
                {
                    return null;
                }
                else
                {
                    Uri location = parent.ExternalMetadataLocation;

                    Uri result = GetUri(listenUri, location);
                    if (addressUpdater != null)
                    {
                        addressUpdater.UpdateUri(ref result);
                    }
                    return result.ToString();
                }
            }

            private async Task<bool> TryHandleDocumentationRequestAsync(HttpContext requestContext, IQueryCollection queries)
            {
                if (!HelpPageEnabled)
                    return false;

                MetadataResult result;
                if (parent.MetadataEnabled)
                {
                    string discoUrl = null;
                    string singleWsdlUrl = null;
                    bool linkMetadata = true;

                    DynamicAddressUpdateWriter addressUpdater = null;
                    if (parent.UpdateAddressDynamically)
                    {
                        addressUpdater = parent.GetDynamicAddressWriter(requestContext.Request, listenUri, false);
                    }

                    string wsdlUrl = FindWsdlReference(addressUpdater);

                    string httpGetUrl = GetHttpGetUrl(addressUpdater);

                    if (wsdlUrl == null && httpGetUrl != null)
                    {
                        wsdlUrl = httpGetUrl + "?" + WsdlQueryString;
                        singleWsdlUrl = httpGetUrl + "?" + SingleWsdlQueryString;
                    }

                    if (httpGetUrl != null)
                        discoUrl = httpGetUrl + "?" + DiscoQueryString;

                    if (wsdlUrl == null)
                    {
                        wsdlUrl = GetMexUrl(addressUpdater);
                        linkMetadata = false;
                    }

                    result = new MetadataOnHelpPageResult(discoUrl, wsdlUrl, singleWsdlUrl, GetInitData().ServiceName, GetInitData().ClientName, linkMetadata);
                }
                else
                {
                    result = new MetadataOffHelpPageMessage();
                }

                SetResponseStatusAndContentType(requestContext.Response, HttpStatusCode.OK, HtmlContentType);
                await result.WriteResponseAsync(requestContext.Response);
                return true;
            }

            private string GetHttpGetUrl(DynamicAddressUpdateWriter addressUpdater)
            {
                Uri result = null;
                if (listenUri.Scheme == Uri.UriSchemeHttp)
                {
                    if (parent.HttpGetEnabled)
                        result = parent.HttpGetUrl;
                    else if (parent.HttpsGetEnabled)
                        result = parent.HttpsGetUrl;
                }
                else
                {
                    if (parent.HttpsGetEnabled)
                        result = parent.HttpsGetUrl;
                    else if (parent.HttpGetEnabled)
                        result = parent.HttpGetUrl;
                }

                if (result != null)
                {
                    if (addressUpdater != null)
                    {
                        addressUpdater.UpdateUri(ref result, listenUri.Scheme != result.Scheme /*updateBaseAddressOnly*/);
                    }
                    return result.ToString();
                }

                return null;
            }

            private string GetMexUrl(DynamicAddressUpdateWriter addressUpdater)
            {
                if (parent.MexEnabled)
                {
                    Uri result = parent.MexUrl;
                    if (addressUpdater != null)
                    {
                        addressUpdater.UpdateUri(ref result);
                    }
                    return result.ToString();
                }

                return null;
            }

            private async Task<bool> TryHandleMetadataRequestAsync(HttpContext requestContext, IQueryCollection queries)
            {
                if (!GetWsdlEnabled)
                    return false;

                WriteFilter writeFilter = parent.GetWriteFilter(requestContext.Request, listenUri, false);

                string query = FindQuery(queries);

                MetadataResult result;
                if (string.IsNullOrEmpty(query))
                {
                    //if the documentation page is not available return the default wsdl if it exists
                    if (!HelpPageEnabled && GetInitData().DefaultWsdl != null)
                    {
                        // use the default WSDL
                        result = new ServiceDescriptionResult(GetInitData().DefaultWsdl, writeFilter);
                        SetResponseStatusAndContentType(requestContext.Response, HttpStatusCode.OK, XmlContentType);
                        GetInitData().FixImportAddresses();
                        await result.WriteResponseAsync(requestContext.Response);
                        return true;
                    }

                    return false;
                }

                // try to look the document up in the query table
                object doc;
                if (GetInitData().TryQueryLookup(query, out doc))
                {
                    if (doc is WsdlNS.ServiceDescription)
                    {
                        result = new ServiceDescriptionResult((WsdlNS.ServiceDescription)doc, writeFilter);
                    }
                    else if (doc is XmlSchema)
                    {
                        result = new XmlSchemaResult((XmlSchema)doc, writeFilter);
                    }
                    else if (doc is string)
                    {
                        if (((string)doc) == DiscoToken)
                        {
                            result = CreateDiscoMessage(writeFilter as DynamicAddressUpdateWriter);
                        }
                        else
                        {
                            Fx.Assert("Bad object in HttpGetImpl docFromQuery table");
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Bad object in HttpGetImpl docFromQuery table")));
                        }
                    }
                    else
                    {
                        Fx.Assert("Bad object in HttpGetImpl docFromQuery table");
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Bad object in HttpGetImpl docFromQuery table")));
                    }

                    SetResponseStatusAndContentType(requestContext.Response, HttpStatusCode.OK, XmlContentType);
                    GetInitData().FixImportAddresses();
                    await result.WriteResponseAsync(requestContext.Response);
                    return true;
                }

                // otherwise see if they just wanted ?WSDL
                if (string.Compare(query, WsdlQueryString, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (GetInitData().DefaultWsdl != null)
                    {
                        // use the default WSDL
                        result = new ServiceDescriptionResult(GetInitData().DefaultWsdl, writeFilter);
                        SetResponseStatusAndContentType(requestContext.Response, HttpStatusCode.OK, XmlContentType);
                        GetInitData().FixImportAddresses();
                        await result.WriteResponseAsync(requestContext.Response);
                        return true;
                    }

                    // or redirect to an external WSDL
                    string wsdlReference = FindWsdlReference(writeFilter as DynamicAddressUpdateWriter);
                    if (wsdlReference != null)
                    {
                        RespondWithRedirect(requestContext.Response, wsdlReference);
                        return true;
                    }
                }

                // ?singleWSDL
                if (string.Compare(query, SingleWsdlQueryString, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    WsdlNS.ServiceDescription singleWSDL = parent.SingleWsdl;
                    if (singleWSDL != null)
                    {
                        result = new ServiceDescriptionResult(singleWSDL, writeFilter);
                        SetResponseStatusAndContentType(requestContext.Response, HttpStatusCode.OK, XmlContentType);
                        await result.WriteResponseAsync(requestContext.Response);
                        return true;
                    }
                }

                // we weren't able to handle the request -- return the documentation page if available
                return false;
            }

            private MetadataResult CreateDiscoMessage(DynamicAddressUpdateWriter addressUpdater)
            {
                Uri wsdlUrlBase = listenUri;
                if (addressUpdater != null)
                {
                    addressUpdater.UpdateUri(ref wsdlUrlBase);
                }
                string wsdlUrl = wsdlUrlBase.ToString() + "?" + WsdlQueryString;

                Uri docUrl = null;
                if (listenUri.Scheme == Uri.UriSchemeHttp)
                {
                    if (parent.HttpHelpPageEnabled)
                        docUrl = parent.HttpHelpPageUrl;
                    else if (parent.HttpsHelpPageEnabled)
                        docUrl = parent.HttpsGetUrl;
                }
                else
                {
                    if (parent.HttpsHelpPageEnabled)
                        docUrl = parent.HttpsHelpPageUrl;
                    else if (parent.HttpHelpPageEnabled)
                        docUrl = parent.HttpGetUrl;
                }
                if (addressUpdater != null)
                {
                    addressUpdater.UpdateUri(ref docUrl);
                }

                return new DiscoResult(wsdlUrl, docUrl.ToString());
            }

            private string FindQuery(IQueryCollection queries)
            {
                string query = null;
                foreach (var q in queries)
                {
                    if (string.Compare(q.Key, WsdlQueryString, StringComparison.OrdinalIgnoreCase) == 0)
                        query = q.Key;
                    else if (string.Compare(q.Key, XsdQueryString, StringComparison.OrdinalIgnoreCase) == 0)
                        query = q.Key;
                    else if (string.Compare(q.Key, SingleWsdlQueryString, StringComparison.OrdinalIgnoreCase) == 0)
                        query = q.Key;
                    else if (parent.HelpPageEnabled && (string.Compare(q.Key, DiscoQueryString, StringComparison.OrdinalIgnoreCase) == 0))
                        query = q.Key;
                }

                return query;
            }

            private async Task<bool> ProcessHttpRequest(HttpContext requestContext)
            {
                if (!"GET".Equals(requestContext.Request.Method, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string queryString = requestContext.Request.QueryString.Value;

                if (queryString.Length > MaxQueryStringChars)
                {
                    SetHttpResponseStatus(requestContext.Response, HttpStatusCode.RequestUriTooLong);
                    return true;
                }

                var queries = requestContext.Request.Query;
                if (await TryHandleMetadataRequestAsync(requestContext, queries))
                    return true;

                if (await TryHandleDocumentationRequestAsync(requestContext, queries))
                    return true;

                return false;
            }

            private class InitializationData
            {
                private readonly Dictionary<string, object> docFromQuery;
                private readonly Dictionary<object, string> queryFromDoc;
                private WsdlNS.ServiceDescriptionCollection wsdls;
                private XmlSchemaSet xsds;

                public string ServiceName;
                public string ClientName;
                public WsdlNS.ServiceDescription DefaultWsdl;

                private InitializationData(
                    Dictionary<string, object> docFromQuery,
                    Dictionary<object, string> queryFromDoc,
                    WsdlNS.ServiceDescriptionCollection wsdls,
                    XmlSchemaSet xsds)
                {
                    this.docFromQuery = docFromQuery;
                    this.queryFromDoc = queryFromDoc;
                    this.wsdls = wsdls;
                    this.xsds = xsds;
                }

                public bool TryQueryLookup(string query, out object doc)
                {
                    return docFromQuery.TryGetValue(query, out doc);
                }

                public static InitializationData InitializeFrom(ServiceMetadataExtension extension)
                {
                    Dictionary<string, object> docFromQueryInit = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<object, string> queryFromDocInit = new Dictionary<object, string>();

                    // this collection type provides useful lookup features
                    WsdlNS.ServiceDescriptionCollection wsdls = CollectWsdls(extension.Metadata);
                    XmlSchemaSet xsds = CollectXsds(extension.Metadata);

                    WsdlNS.ServiceDescription defaultWsdl = null;
                    WsdlNS.Service someService = GetAnyService(wsdls);
                    if (someService != null)
                        defaultWsdl = someService.ServiceDescription;

                    // WSDLs
                    {
                        int i = 0;
                        foreach (WsdlNS.ServiceDescription wsdlDoc in wsdls)
                        {
                            string query = WsdlQueryString;
                            if (wsdlDoc != defaultWsdl) // don't count the WSDL at ?WSDL
                                query += "=wsdl" + (i++).ToString(System.Globalization.CultureInfo.InvariantCulture);

                            docFromQueryInit.Add(query, wsdlDoc);
                            queryFromDocInit.Add(wsdlDoc, query);
                        }
                    }

                    // XSDs
                    {
                        int i = 0;
                        foreach (XmlSchema xsdDoc in xsds.Schemas())
                        {
                            string query = XsdQueryString + "=xsd" + (i++).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            docFromQueryInit.Add(query, xsdDoc);
                            queryFromDocInit.Add(xsdDoc, query);
                        }
                    }

                    // Disco
                    if (extension.HelpPageEnabled)
                    {
                        string query = DiscoQueryString;
                        docFromQueryInit.Add(query, DiscoToken);
                        queryFromDocInit.Add(DiscoToken, query);
                    }

                    InitializationData data = new InitializationData(docFromQueryInit, queryFromDocInit, wsdls, xsds);

                    data.DefaultWsdl = defaultWsdl;
                    data.ServiceName = GetAnyWsdlName(wsdls);
                    data.ClientName = ClientClassGenerator.GetClientClassName(GetAnyContractName(wsdls) ?? "IHello");

                    return data;
                }

                private static WsdlNS.ServiceDescriptionCollection CollectWsdls(MetadataSet metadata)
                {
                    WsdlNS.ServiceDescriptionCollection wsdls = new WsdlNS.ServiceDescriptionCollection();
                    foreach (MetadataSection section in metadata.MetadataSections)
                    {
                        if (section.Metadata is WsdlNS.ServiceDescription)
                        {
                            wsdls.Add((WsdlNS.ServiceDescription)section.Metadata);
                        }
                    }
                    return wsdls;
                }

                private static XmlSchemaSet CollectXsds(MetadataSet metadata)
                {
                    XmlSchemaSet xsds = new XmlSchemaSet();
                    xsds.XmlResolver = null;
                    foreach (MetadataSection section in metadata.MetadataSections)
                    {
                        if (section.Metadata is XmlSchema)
                        {
                            xsds.Add((XmlSchema)section.Metadata);
                        }
                    }
                    return xsds;
                }

                internal void FixImportAddresses()
                {
                    // fixup imports and includes with addresses
                    // WSDLs
                    foreach (WsdlNS.ServiceDescription wsdlDoc in wsdls)
                    {
                        FixImportAddresses(wsdlDoc);
                    }
                    // XSDs
                    foreach (XmlSchema xsdDoc in xsds.Schemas())
                    {
                        FixImportAddresses(xsdDoc);
                    }

                }

                private void FixImportAddresses(WsdlNS.ServiceDescription wsdlDoc)
                {
                    foreach (WsdlNS.Import import in wsdlDoc.Imports)
                    {
                        if (!string.IsNullOrEmpty(import.Location)) continue;

                        WsdlNS.ServiceDescription targetDoc = wsdls[import.Namespace ?? string.Empty];
                        if (targetDoc != null)
                        {
                            string query = queryFromDoc[targetDoc];
                            import.Location = BaseAddressPattern + "?" + query;
                        }
                    }

                    if (wsdlDoc.Types != null)
                    {
                        foreach (XmlSchema xsdDoc in wsdlDoc.Types.Schemas)
                        {
                            FixImportAddresses(xsdDoc);
                        }
                    }
                }

                private void FixImportAddresses(XmlSchema xsdDoc)
                {
                    foreach (XmlSchemaObject o in xsdDoc.Includes)
                    {
                        XmlSchemaExternal external = o as XmlSchemaExternal;
                        if (external == null || !string.IsNullOrEmpty(external.SchemaLocation)) continue;

                        string targetNs = external is XmlSchemaImport ? ((XmlSchemaImport)external).Namespace : xsdDoc.TargetNamespace;

                        foreach (XmlSchema targetXsd in xsds.Schemas(targetNs ?? string.Empty))
                        {
                            if (targetXsd != xsdDoc)
                            {
                                string query = queryFromDoc[targetXsd];
                                external.SchemaLocation = BaseAddressPattern + "?" + query;
                                break;
                            }
                        }
                    }
                }

                private static string GetAnyContractName(WsdlNS.ServiceDescriptionCollection wsdls)
                {
                    // try to track down a WSDL portType name using a wsdl:service as a starting point
                    foreach (WsdlNS.ServiceDescription wsdl in wsdls)
                    {
                        foreach (WsdlNS.Service service in wsdl.Services)
                        {
                            foreach (WsdlNS.Port port in service.Ports)
                            {
                                if (!port.Binding.IsEmpty)
                                {
                                    WsdlNS.Binding binding = wsdls.GetBinding(port.Binding);
                                    if (!binding.Type.IsEmpty)
                                    {
                                        return binding.Type.Name;
                                    }
                                }
                            }
                        }
                    }
                    return null;
                }

                private static WsdlNS.Service GetAnyService(WsdlNS.ServiceDescriptionCollection wsdls)
                {
                    // try to track down a WSDL service
                    foreach (WsdlNS.ServiceDescription wsdl in wsdls)
                    {
                        if (wsdl.Services.Count > 0)
                        {
                            return wsdl.Services[0];
                        }
                    }
                    return null;
                }

                private static string GetAnyWsdlName(WsdlNS.ServiceDescriptionCollection wsdls)
                {
                    // try to track down a WSDL name
                    foreach (WsdlNS.ServiceDescription wsdl in wsdls)
                    {
                        if (!string.IsNullOrEmpty(wsdl.Name))
                        {
                            return wsdl.Name;
                        }
                    }
                    return null;
                }
            }

            internal void Configure(IApplicationBuilder app)
            {
                RequestDelegate MetadataMiddlewareImpl(RequestDelegate _next)
                {
                    return async httpContext =>
                    {
                        if (!await HandleRequest(httpContext))
                        {
                            await _next(httpContext);
                        }
                    };
                }
                app.Use(MetadataMiddlewareImpl);
            }

            private async Task<bool> HandleRequest(HttpContext httpContext)
            {
                try
                {
                    return await ProcessHttpRequest(httpContext);
                }
                catch (Exception exception)
                {
                    var result = new MetadataOnHelpPageResult(SR.SFxDocExt_Error, new ExceptionDetail(exception));
                    SetResponseStatusAndContentType(httpContext.Response, HttpStatusCode.InternalServerError, HtmlContentType);
                    await result.WriteResponseAsync(httpContext.Response);
                    return true;
                }
            }

            #region static helpers
            private static void SetResponseStatusAndContentType(HttpResponse httpResponse, HttpStatusCode status, string contentType)
            {
                httpResponse.StatusCode = (int)status;
                httpResponse.ContentType = contentType;
            }

            private static void RespondWithRedirect(HttpResponse httpResponse, string redirectedDestination)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.RedirectKeepVerb;
                httpResponse.Headers["Location"] = redirectedDestination;
            }

            private static void SetHttpResponseStatus(HttpResponse response, HttpStatusCode code)
            {
                response.StatusCode = (int)code;
            }

            internal static Uri GetUri(Uri baseUri, Uri relativeUri)
            {
                return GetUri(baseUri, relativeUri.OriginalString);
            }

            internal static Uri GetUri(Uri baseUri, string path)
            {
                if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
                {
                    int i = 1;
                    for (; i < path.Length; ++i)
                    {
                        if (path[i] != '/' && path[i] != '\\')
                        {
                            break;
                        }
                    }
                    path = path.Substring(i);
                }

                if (path.Length == 0)
                    return baseUri;

                if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
                {
                    baseUri = new Uri(baseUri.AbsoluteUri + "/");
                }
                return new Uri(baseUri, path);
            }

            #endregion static helpers

            #region Helper Message implementations
            private class DiscoResult : MetadataResult
            {
                private string wsdlAddress;
                private string docAddress;

                public DiscoResult(string wsdlAddress, string docAddress)
                {
                    this.wsdlAddress = wsdlAddress;
                    this.docAddress = docAddress;
                }

                protected override void Write(XmlWriter writer)
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("discovery", "http://schemas.xmlsoap.org/disco/");
                    writer.WriteStartElement("contractRef", "http://schemas.xmlsoap.org/disco/scl/");
                    writer.WriteAttributeString("ref", wsdlAddress);
                    writer.WriteAttributeString("docRef", docAddress);
                    writer.WriteEndElement(); // </contractRef>
                    writer.WriteEndElement(); // </discovery>
                    writer.WriteEndDocument();
                }
            }

            private class MetadataOnHelpPageResult : MetadataResult
            {
                private string discoUrl;
                private string metadataUrl;
                private string singleWsdlUrl;
                private string serviceName;
                private string clientName;
                private bool linkMetadata;
                private string errorMessage;
                private ExceptionDetail exceptionDetail;

                public MetadataOnHelpPageResult(string discoUrl, string metadataUrl, string singleWsdlUrl, string serviceName, string clientName, bool linkMetadata)
                    : base()
                {
                    this.discoUrl = discoUrl;
                    this.metadataUrl = metadataUrl;
                    this.singleWsdlUrl = singleWsdlUrl;
                    this.serviceName = serviceName;
                    this.clientName = clientName;
                    this.linkMetadata = linkMetadata;
                }

                public MetadataOnHelpPageResult(string errorMessage, ExceptionDetail exceptionDetail)
                    : base()
                {
                    this.errorMessage = errorMessage;
                    this.exceptionDetail = exceptionDetail;
                }

                protected override void Write(XmlWriter writer)
                {
                    HelpPageWriter page = new HelpPageWriter(writer);

                    writer.WriteStartElement("HTML");
                    writer.WriteStartElement("HEAD");

                    if (!string.IsNullOrEmpty(discoUrl))
                    {
                        page.WriteDiscoLink(discoUrl);
                    }

                    page.WriteStyleSheet();

                    page.WriteTitle(!string.IsNullOrEmpty(serviceName) ? SR.Format(SR.SFxDocExt_MainPageTitle, serviceName) : SR.SFxDocExt_MainPageTitleNoServiceName);

                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        page.WriteError(errorMessage);

                        if (exceptionDetail != null)
                        {
                            page.WriteExceptionDetail(exceptionDetail);
                        }
                    }
                    else
                    {
                        page.WriteToolUsage(metadataUrl, singleWsdlUrl, linkMetadata);
                        page.WriteSampleCode(clientName);
                    }

                    writer.WriteEndElement(); // BODY
                    writer.WriteEndElement(); // HTML
                }

                private struct HelpPageWriter
                {
                    private XmlWriter writer;
                    public HelpPageWriter(XmlWriter writer)
                    {
                        this.writer = writer;
                    }

                    internal void WriteClass(string className)
                    {
                        writer.WriteStartElement("font");
                        writer.WriteAttributeString("color", "teal");
                        writer.WriteString(className);
                        writer.WriteEndElement(); // font
                    }

                    internal void WriteComment(string comment)
                    {
                        writer.WriteStartElement("font");
                        writer.WriteAttributeString("color", "green");
                        writer.WriteString(comment);
                        writer.WriteEndElement(); // font
                    }

                    internal void WriteDiscoLink(string discoUrl)
                    {
                        writer.WriteStartElement("link");
                        writer.WriteAttributeString("rel", "alternate");
                        writer.WriteAttributeString("type", "text/xml");
                        writer.WriteAttributeString("href", discoUrl);
                        writer.WriteEndElement(); // link
                    }

                    internal void WriteError(string message)
                    {
                        writer.WriteStartElement("P");
                        writer.WriteAttributeString("class", "intro");
                        writer.WriteString(message);
                        writer.WriteEndElement(); // P

                    }

                    internal void WriteKeyword(string keyword)
                    {
                        writer.WriteStartElement("font");
                        writer.WriteAttributeString("color", "blue");
                        writer.WriteString(keyword);
                        writer.WriteEndElement(); // font
                    }

                    internal void WriteSampleCode(string clientName)
                    {
                        writer.WriteStartElement("P");
                        writer.WriteAttributeString("class", "intro");
                        writer.WriteEndElement(); // P

                        writer.WriteRaw(SR.SFxDocExt_MainPageIntro2);


                        // C#
                        writer.WriteRaw(SR.SFxDocExt_CS);
                        writer.WriteStartElement("PRE");
                        WriteKeyword("class ");
                        WriteClass("Test\n");
                        writer.WriteString("{\n");
                        WriteKeyword("    static void ");
                        writer.WriteString("Main()\n");
                        writer.WriteString("    {\n");
                        writer.WriteString("        ");
                        WriteClass(clientName);
                        writer.WriteString(" client = ");
                        WriteKeyword("new ");
                        WriteClass(clientName);
                        writer.WriteString("();\n\n");
                        WriteComment("        // " + SR.SFxDocExt_MainPageComment+ "\n\n");
                        WriteComment("        // " + SR.SFxDocExt_MainPageComment2+ "\n");
                        writer.WriteString("        client.Close();\n");
                        writer.WriteString("    }\n");
                        writer.WriteString("}\n");
                        writer.WriteEndElement(); // PRE
                        writer.WriteRaw(HttpGetImpl.HtmlBreak);


                        // VB
                        writer.WriteRaw(SR.SFxDocExt_VB);
                        writer.WriteStartElement("PRE");
                        WriteKeyword("Class ");
                        WriteClass("Test\n");
                        WriteKeyword("    Shared Sub ");
                        writer.WriteString("Main()\n");
                        WriteKeyword("        Dim ");
                        writer.WriteString("client As ");
                        WriteClass(clientName);
                        writer.WriteString(" = ");
                        WriteKeyword("New ");
                        WriteClass(clientName);
                        writer.WriteString("()\n");
                        WriteComment("        ' " + SR.SFxDocExt_MainPageComment+ "\n\n");
                        WriteComment("        ' " + SR.SFxDocExt_MainPageComment2+ "\n");
                        writer.WriteString("        client.Close()\n");
                        WriteKeyword("    End Sub\n");
                        WriteKeyword("End Class");
                        writer.WriteEndElement(); // PRE
                    }

                    internal void WriteExceptionDetail(ExceptionDetail exceptionDetail)
                    {
                        writer.WriteStartElement("PRE");
                        writer.WriteString(exceptionDetail.ToString().Replace("\r", ""));
                        writer.WriteEndElement(); // PRE
                    }

                    internal void WriteStyleSheet()
                    {
                        writer.WriteStartElement("STYLE");
                        writer.WriteAttributeString("type", "text/css");
                        writer.WriteString("#content{ FONT-SIZE: 0.7em; PADDING-BOTTOM: 2em; MARGIN-LEFT: 30px}");
                        writer.WriteString("BODY{MARGIN-TOP: 0px; MARGIN-LEFT: 0px; COLOR: #000000; FONT-FAMILY: Verdana; BACKGROUND-COLOR: white}");
                        writer.WriteString("P{MARGIN-TOP: 0px; MARGIN-BOTTOM: 12px; COLOR: #000000; FONT-FAMILY: Verdana}");
                        writer.WriteString("PRE{BORDER-RIGHT: #f0f0e0 1px solid; PADDING-RIGHT: 5px; BORDER-TOP: #f0f0e0 1px solid; MARGIN-TOP: -5px; PADDING-LEFT: 5px; FONT-SIZE: 1.2em; PADDING-BOTTOM: 5px; BORDER-LEFT: #f0f0e0 1px solid; PADDING-TOP: 5px; BORDER-BOTTOM: #f0f0e0 1px solid; FONT-FAMILY: Courier New; BACKGROUND-COLOR: #e5e5cc}");
                        writer.WriteString(".heading1{MARGIN-TOP: 0px; PADDING-LEFT: 15px; FONT-WEIGHT: normal; FONT-SIZE: 26px; MARGIN-BOTTOM: 0px; PADDING-BOTTOM: 3px; MARGIN-LEFT: -30px; WIDTH: 100%; COLOR: #ffffff; PADDING-TOP: 10px; FONT-FAMILY: Tahoma; BACKGROUND-COLOR: #003366}");
                        writer.WriteString(".intro{MARGIN-LEFT: -15px}");
                        writer.WriteEndElement(); // STYLE
                    }

                    internal void WriteTitle(string title)
                    {
                        writer.WriteElementString("TITLE", title);
                        writer.WriteEndElement(); // HEAD
                        writer.WriteStartElement("BODY");
                        writer.WriteStartElement("DIV");
                        writer.WriteAttributeString("id", "content");
                        writer.WriteStartElement("P");
                        writer.WriteAttributeString("class", "heading1");
                        writer.WriteString(title);
                        writer.WriteEndElement(); // P
                        writer.WriteRaw(HttpGetImpl.HtmlBreak);

                    }

                    internal void WriteToolUsage(string wsdlUrl, string singleWsdlUrl, bool linkMetadata)
                    {
                        writer.WriteStartElement("P");
                        writer.WriteAttributeString("class", "intro");

                        if (wsdlUrl != null)
                        {
                            WriteMetadataAddress(SR.SFxDocExt_MainPageIntro1a, "svcutil.exe ", wsdlUrl, linkMetadata);
                            if (singleWsdlUrl != null)
                            {
                                // ?singleWsdl message
                                writer.WriteStartElement("P");
                                WriteMetadataAddress(SR.SFxDocExt_MainPageIntroSingleWsdl, null, singleWsdlUrl, linkMetadata);
                                writer.WriteEndElement();
                            }
                        }
                        else
                        {
                            // no metadata message
                            writer.WriteRaw(SR.SFxDocExt_MainPageIntro1b);
                        }
                        writer.WriteEndElement(); // P
                    }

                    private void WriteMetadataAddress(string introductionText, string clientToolName, string wsdlUrl, bool linkMetadata)
                    {
                        writer.WriteRaw(introductionText);
                        writer.WriteRaw(HttpGetImpl.HtmlBreak);
                        writer.WriteStartElement("PRE");
                        if (!string.IsNullOrEmpty(clientToolName))
                        {
                            writer.WriteString(clientToolName);
                        }

                        if (linkMetadata)
                        {
                            writer.WriteStartElement("A");
                            writer.WriteAttributeString("HREF", wsdlUrl);
                        }

                        writer.WriteString(wsdlUrl);

                        if (linkMetadata)
                        {
                            writer.WriteEndElement(); // A
                        }

                        writer.WriteEndElement(); // PRE
                    }
                }
            }

            private class MetadataOffHelpPageMessage : MetadataResult
            {
                protected override void Write(XmlWriter writer)
                {
                    writer.WriteStartElement("HTML");
                    writer.WriteStartElement("HEAD");
                    writer.WriteRaw(string.Format(CultureInfo.InvariantCulture,
                        @"<STYLE type=""text/css"">#content{{ FONT-SIZE: 0.7em; PADDING-BOTTOM: 2em; MARGIN-LEFT: 30px}}BODY{{MARGIN-TOP: 0px; MARGIN-LEFT: 0px; COLOR: #000000; FONT-FAMILY: Verdana; BACKGROUND-COLOR: white}}P{{MARGIN-TOP: 0px; MARGIN-BOTTOM: 12px; COLOR: #000000; FONT-FAMILY: Verdana}}PRE{{BORDER-RIGHT: #f0f0e0 1px solid; PADDING-RIGHT: 5px; BORDER-TOP: #f0f0e0 1px solid; MARGIN-TOP: -5px; PADDING-LEFT: 5px; FONT-SIZE: 1.2em; PADDING-BOTTOM: 5px; BORDER-LEFT: #f0f0e0 1px solid; PADDING-TOP: 5px; BORDER-BOTTOM: #f0f0e0 1px solid; FONT-FAMILY: Courier New; BACKGROUND-COLOR: #e5e5cc}}.heading1{{MARGIN-TOP: 0px; PADDING-LEFT: 15px; FONT-WEIGHT: normal; FONT-SIZE: 26px; MARGIN-BOTTOM: 0px; PADDING-BOTTOM: 3px; MARGIN-LEFT: -30px; WIDTH: 100%; COLOR: #ffffff; PADDING-TOP: 10px; FONT-FAMILY: Tahoma; BACKGROUND-COLOR: #003366}}.intro{{MARGIN-LEFT: -15px}}</STYLE>
<TITLE>Service</TITLE>"));
                    writer.WriteEndElement(); //HEAD

                    writer.WriteRaw(string.Format(CultureInfo.InvariantCulture,
                                            @"<BODY>
<DIV id=""content"">
<P class=""heading1"">Service</P>
<BR/>
<P class=""intro"">{0}</P>
<PRE>
<font color=""blue"">&lt;<font color=""darkred"">" + ConfigurationStrings.BehaviorsSectionName + @"</font>&gt;</font>
<font color=""blue"">    &lt;<font color=""darkred"">" + ConfigurationStrings.ServiceBehaviors + @"</font>&gt;</font>
<font color=""blue"">        &lt;<font color=""darkred"">" + ConfigurationStrings.Behavior + @" </font><font color=""red"">" + ConfigurationStrings.Name + @"</font>=<font color=""black"">""</font>MyServiceTypeBehaviors<font color=""black"">"" </font>&gt;</font>
<font color=""blue"">            &lt;<font color=""darkred"">" + ConfigurationStrings.ServiceMetadataPublishingSectionName + @" </font><font color=""red"">" + ConfigurationStrings.HttpGetEnabled + @"</font>=<font color=""black"">""</font>true<font color=""black"">"" </font>/&gt;</font>
<font color=""blue"">        &lt;<font color=""darkred"">/" + ConfigurationStrings.Behavior + @"</font>&gt;</font>
<font color=""blue"">    &lt;<font color=""darkred"">/" + ConfigurationStrings.ServiceBehaviors + @"</font>&gt;</font>
<font color=""blue"">&lt;<font color=""darkred"">/" + ConfigurationStrings.BehaviorsSectionName + @"</font>&gt;</font>
</PRE>
<P class=""intro"">{1}</P>
<PRE>
<font color=""blue"">&lt;<font color=""darkred"">" + ConfigurationStrings.Service + @" </font><font color=""red"">" + ConfigurationStrings.Name + @"</font>=<font color=""black"">""</font><i>MyNamespace.MyServiceType</i><font color=""black"">"" </font><font color=""red"">" + ConfigurationStrings.BehaviorConfiguration + @"</font>=<font color=""black"">""</font><i>MyServiceTypeBehaviors</i><font color=""black"">"" </font>&gt;</font>
</PRE>
<P class=""intro"">{2}</P>
<PRE>
<font color=""blue"">&lt;<font color=""darkred"">" + ConfigurationStrings.Endpoint + @" </font><font color=""red"">" + ConfigurationStrings.Contract + @"</font>=<font color=""black"">""</font>" + ServiceMetadataBehavior.MexContractName + @"<font color=""black"">"" </font><font color=""red"">" + ConfigurationStrings.Binding + @"</font>=<font color=""black"">""</font>mexHttpBinding<font color=""black"">"" </font><font color=""red"">" + ConfigurationStrings.Address + @"</font>=<font color=""black"">""</font>mex<font color=""black"">"" </font>/&gt;</font>
</PRE>

<P class=""intro"">{3}</P>
<PRE>
<font color=""blue"">&lt;<font color=""darkred"">configuration</font>&gt;</font>
<font color=""blue"">    &lt;<font color=""darkred"">" + ConfigurationStrings.SectionGroupName + @"</font>&gt;</font>
 
<font color=""blue"">        &lt;<font color=""darkred"">" + ConfigurationStrings.ServicesSectionName + @"</font>&gt;</font>
<font color=""blue"">            &lt;!-- <font color=""green"">{4}</font> --&gt;</font>
<font color=""blue"">            &lt;<font color=""darkred"">" + ConfigurationStrings.Service + @" </font><font color=""red"">" + ConfigurationStrings.Name + @"</font>=<font color=""black"">""</font><i>MyNamespace.MyServiceType</i><font color=""black"">"" </font><font color=""red"">" + ConfigurationStrings.BehaviorConfiguration + @"</font>=<font color=""black"">""</font><i>MyServiceTypeBehaviors</i><font color=""black"">"" </font>&gt;</font>
<font color=""blue"">                &lt;!-- <font color=""green"">{5}</font> --&gt;</font>
<font color=""blue"">                &lt;!-- <font color=""green"">{6}</font> --&gt;</font>
<font color=""blue"">                &lt;<font color=""darkred"">" + ConfigurationStrings.Endpoint + @" </font><font color=""red"">" + ConfigurationStrings.Contract + @"</font>=<font color=""black"">""</font>" + ServiceMetadataBehavior.MexContractName + @"<font color=""black"">"" </font><font color=""red"">" + ConfigurationStrings.Binding + @"</font>=<font color=""black"">""</font>mexHttpBinding<font color=""black"">"" </font><font color=""red"">" + ConfigurationStrings.Address + @"</font>=<font color=""black"">""</font>mex<font color=""black"">"" </font>/&gt;</font>
<font color=""blue"">            &lt;<font color=""darkred"">/" + ConfigurationStrings.Service + @"</font>&gt;</font>
<font color=""blue"">        &lt;<font color=""darkred"">/" + ConfigurationStrings.ServicesSectionName + @"</font>&gt;</font>
 
<font color=""blue"">        &lt;<font color=""darkred"">" + ConfigurationStrings.BehaviorsSectionName + @"</font>&gt;</font>
<font color=""blue"">            &lt;<font color=""darkred"">" + ConfigurationStrings.ServiceBehaviors + @"</font>&gt;</font>
<font color=""blue"">                &lt;<font color=""darkred"">" + ConfigurationStrings.Behavior + @" </font><font color=""red"">name</font>=<font color=""black"">""</font><i>MyServiceTypeBehaviors</i><font color=""black"">"" </font>&gt;</font>
<font color=""blue"">                    &lt;!-- <font color=""green"">{7}</font> --&gt;</font>
<font color=""blue"">                    &lt;<font color=""darkred"">" + ConfigurationStrings.ServiceMetadataPublishingSectionName + @" </font><font color=""red"">" + ConfigurationStrings.HttpGetEnabled + @"</font>=<font color=""black"">""</font>true<font color=""black"">"" </font>/&gt;</font>
<font color=""blue"">                &lt;<font color=""darkred"">/" + ConfigurationStrings.Behavior + @"</font>&gt;</font>
<font color=""blue"">            &lt;<font color=""darkred"">/" + ConfigurationStrings.ServiceBehaviors + @"</font>&gt;</font>
<font color=""blue"">        &lt;<font color=""darkred"">/" + ConfigurationStrings.BehaviorsSectionName + @"</font>&gt;</font>
 
<font color=""blue"">    &lt;<font color=""darkred"">/" + ConfigurationStrings.SectionGroupName + @"</font>&gt;</font>
<font color=""blue"">&lt;<font color=""darkred"">/configuration</font>&gt;</font>
</PRE>
<P class=""intro"">{8}</P>
</DIV>
</BODY>",
SR.SFxDocExt_NoMetadataSection1, SR.SFxDocExt_NoMetadataSection2,
SR.SFxDocExt_NoMetadataSection3, SR.SFxDocExt_NoMetadataSection4,
SR.SFxDocExt_NoMetadataConfigComment1, SR.SFxDocExt_NoMetadataConfigComment2,
SR.SFxDocExt_NoMetadataConfigComment3, SR.SFxDocExt_NoMetadataConfigComment4,
SR.SFxDocExt_NoMetadataSection5        ));

                    writer.WriteEndElement(); //HTML
                }
            }

            internal abstract class MetadataResult
            {
                public async Task WriteResponseAsync(HttpResponse response)
                {
                    // TODO : Remove use of MemoryStream and write more directly asynchronously
                    using (var memoryStream = new MemoryStream())
                    {
                        var utf8NoBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                        using (var textWriter = new StreamWriter(memoryStream, encoding: utf8NoBomEncoding, bufferSize: 1024, leaveOpen: true))
                        {
                            using (var xmlTextWriter = new XmlTextWriter(textWriter))
                            {
                                xmlTextWriter.Formatting = Formatting.Indented;
                                xmlTextWriter.Indentation = 2;
                                Write(xmlTextWriter);
                            }
                        }
                        memoryStream.Position = 0;
                        await memoryStream.CopyToAsync(response.Body);
                    }
                }

                protected abstract void Write(XmlWriter writer);
            }

            // TODO: Cache the written response in memory and write async
            // Need to accomodate different WriteFilter possibilities. If the first client sends request to http://internal-hostname/service.svc?wsdl
            // and second client sends request to hhtp://public-hostname/service.svc?wsdl, we need to make sure the internal hostname isn't leaked to
            // to an external client.
            private abstract class MetadataFilteredResult : MetadataResult
            {
                private readonly WriteFilter _responseWriter;

                protected MetadataFilteredResult(WriteFilter responseWriter)
                {
                    _responseWriter = responseWriter;
                }

                protected override void Write(XmlWriter xmlWriter)
                {
                    _responseWriter.Writer = xmlWriter;
                    WriteCore(_responseWriter);
                }

                protected abstract void WriteCore(XmlWriter xmlWriter);
            }

            private class ServiceDescriptionResult : MetadataFilteredResult
            {
                private WsdlNS.ServiceDescription description;

                public ServiceDescriptionResult(WsdlNS.ServiceDescription description, WriteFilter responseWriter) : base(responseWriter)
                {
                    this.description = description;
                }

                protected override void WriteCore(XmlWriter writer) => description.Write(writer);
            }

            private class XmlSchemaResult : MetadataFilteredResult
            {
                private readonly XmlSchema _schema;

                public XmlSchemaResult(XmlSchema schema, WriteFilter responseWriter) : base(responseWriter)
                {
                    _schema = schema;
                }

                protected override void WriteCore(XmlWriter writer) => _schema.Write(writer);
            }
            #endregion //Helper Message implementations
        }

        internal abstract class WriteFilter : XmlDictionaryWriter
        {
            internal XmlWriter Writer;
            public abstract WriteFilter CloneWriteFilter();
            public override void Close()
            {
                Writer.Close();
            }

            public override void Flush()
            {
                Writer.Flush();
            }

            public override string LookupPrefix(string ns)
            {
                return Writer.LookupPrefix(ns);
            }

            public override void WriteBase64(byte[] buffer, int index, int count)
            {
                Writer.WriteBase64(buffer, index, count);
            }

            public override void WriteCData(string text)
            {
                Writer.WriteCData(text);
            }

            public override void WriteCharEntity(char ch)
            {
                Writer.WriteCharEntity(ch);
            }

            public override void WriteChars(char[] buffer, int index, int count)
            {
                Writer.WriteChars(buffer, index, count);
            }

            public override void WriteComment(string text)
            {
                Writer.WriteComment(text);
            }

            public override void WriteDocType(string name, string pubid, string sysid, string subset)
            {
                Writer.WriteDocType(name, pubid, sysid, subset);
            }

            public override void WriteEndAttribute()
            {
                Writer.WriteEndAttribute();
            }

            public override void WriteEndDocument()
            {
                Writer.WriteEndDocument();
            }

            public override void WriteEndElement()
            {
                Writer.WriteEndElement();
            }

            public override void WriteEntityRef(string name)
            {
                Writer.WriteEntityRef(name);
            }

            public override void WriteFullEndElement()
            {
                Writer.WriteFullEndElement();
            }

            public override void WriteProcessingInstruction(string name, string text)
            {
                Writer.WriteProcessingInstruction(name, text);
            }

            public override void WriteRaw(string data)
            {
                Writer.WriteRaw(data);
            }

            public override void WriteRaw(char[] buffer, int index, int count)
            {
                Writer.WriteRaw(buffer, index, count);
            }

            public override void WriteStartAttribute(string prefix, string localName, string ns)
            {
                Writer.WriteStartAttribute(prefix, localName, ns);
            }

            public override void WriteStartDocument(bool standalone)
            {
                Writer.WriteStartDocument(standalone);
            }

            public override void WriteStartDocument()
            {
                Writer.WriteStartDocument();
            }

            public override void WriteStartElement(string prefix, string localName, string ns)
            {
                Writer.WriteStartElement(prefix, localName, ns);
            }

            public override WriteState WriteState => Writer.WriteState;

            public override void WriteString(string text)
            {
                Writer.WriteString(text);
            }

            public override void WriteSurrogateCharEntity(char lowChar, char highChar)
            {
                Writer.WriteSurrogateCharEntity(lowChar, highChar);
            }

            public override void WriteWhitespace(string ws)
            {
                Writer.WriteWhitespace(ws);
            }
        }

        private class LocationUpdatingWriter : WriteFilter
        {
            private readonly string oldValue;
            private readonly string newValue;

            // passing null for newValue filters any string with oldValue as a prefix rather than replacing
            internal LocationUpdatingWriter(string oldValue, string newValue)
            {
                this.oldValue = oldValue;

                this.newValue = newValue;
            }

            public override WriteFilter CloneWriteFilter()
            {
                return new LocationUpdatingWriter(oldValue, newValue);
            }

            public override void WriteString(string text)
            {
                if (newValue != null)
                    text = text.Replace(oldValue, newValue);
                else if (text.StartsWith(oldValue, StringComparison.Ordinal))
                    text = string.Empty;

                base.WriteString(text);
            }
        }

        private class DynamicAddressUpdateWriter : WriteFilter
        {
            private readonly string oldHostName;
            private readonly string newHostName;
            private readonly string newBaseAddress;
            private readonly bool removeBaseAddress;
            private readonly string requestScheme;
            private readonly int requestPort;
            private readonly IDictionary<string, int> updatePortsByScheme;

            internal DynamicAddressUpdateWriter(Uri listenUri, string requestHost, int requestPort,
                IDictionary<string, int> updatePortsByScheme, bool removeBaseAddress)
                : this(listenUri.Host, requestHost, removeBaseAddress, listenUri.Scheme, requestPort, updatePortsByScheme)
            {
                newBaseAddress = UpdateUri(listenUri).ToString();
            }

            private DynamicAddressUpdateWriter(string oldHostName, string newHostName, string newBaseAddress, bool removeBaseAddress, string requestScheme,
                int requestPort, IDictionary<string, int> updatePortsByScheme)
                : this(oldHostName, newHostName, removeBaseAddress, requestScheme, requestPort, updatePortsByScheme)
            {
                this.newBaseAddress = newBaseAddress;
            }

            private DynamicAddressUpdateWriter(string oldHostName, string newHostName, bool removeBaseAddress, string requestScheme,
                int requestPort, IDictionary<string, int> updatePortsByScheme)
            {
                this.oldHostName = oldHostName;
                this.newHostName = newHostName;
                this.removeBaseAddress = removeBaseAddress;
                this.requestScheme = requestScheme;
                this.requestPort = requestPort;
                this.updatePortsByScheme = updatePortsByScheme;
            }

            public override WriteFilter CloneWriteFilter()
            {
                return new DynamicAddressUpdateWriter(oldHostName, newHostName, newBaseAddress, removeBaseAddress,
                    requestScheme, requestPort, updatePortsByScheme);
            }

            public override void WriteString(string text)
            {
                Uri uri;
                if (removeBaseAddress &&
                    text.StartsWith(ServiceMetadataExtension.BaseAddressPattern, StringComparison.Ordinal))
                {
                    text = string.Empty;
                }
                else if (!removeBaseAddress &&
                    text.Contains(ServiceMetadataExtension.BaseAddressPattern))
                {
                    text = text.Replace(ServiceMetadataExtension.BaseAddressPattern, newBaseAddress);
                }
                else if (Uri.TryCreate(text, UriKind.Absolute, out uri))
                {
                    Uri newUri = UpdateUri(uri);
                    if (newUri != null)
                    {
                        text = newUri.ToString();
                    }
                }
                base.WriteString(text);
            }

            public void UpdateUri(ref Uri uri, bool updateBaseAddressOnly = false)
            {
                Uri newUri = UpdateUri(uri, updateBaseAddressOnly);
                if (newUri != null)
                {
                    uri = newUri;
                }
            }

            private Uri UpdateUri(Uri uri, bool updateBaseAddressOnly = false)
            {
                // Ordinal comparison okay: we're filtering for auto-generated URIs which will
                // always be based off the listenURI, so always match in case
                if (uri.Host != oldHostName)
                {
                    return null;
                }

                UriBuilder result = new UriBuilder(uri);
                result.Host = newHostName;

                if (!updateBaseAddressOnly)
                {
                    int port;
                    if (uri.Scheme == requestScheme)
                    {
                        port = requestPort;
                    }
                    else if (!updatePortsByScheme.TryGetValue(uri.Scheme, out port))
                    {
                        return null;
                    }
                    result.Port = port;
                }

                return result.Uri;
            }
        }
    }
}
