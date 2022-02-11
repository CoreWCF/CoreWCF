// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using CoreWCF.Channels;
using CoreWCF.Configuration;
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
        private MetadataSet _metadata;
        private WsdlNS.ServiceDescription _singleWsdl;
        private bool _isInitialized = false;
        private bool _isSingleWsdlInitialized = false;
        private ServiceHostBase _owner;
        private readonly object _syncRoot = new object();
        private readonly object _singleWsdlSyncRoot = new object();

        public ServiceMetadataExtension() : this(null) { }

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
                if (!Uri.TryCreate(hostUriString, UriKind.Absolute, out Uri hostUri))
                {
                    return false;
                }

                port = hostUri.Port;
            }

            return true;
        }

        internal Func<RequestDelegate, RequestDelegate> CreateMiddleware(Uri baseAddress, bool isHttps)
        {
            HttpGetImpl impl = new HttpGetImpl(this, baseAddress, isHttps);
            return impl.MetadataMiddleware;
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
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(owner)));
            }

            if (_owner != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.TheServiceMetadataExtensionInstanceCouldNot2_0));
            }

            if (owner.State != CommunicationState.Created && owner.State != CommunicationState.Opening)
            {
                throw new InvalidOperationException(owner.State.ToString());
            }

            _owner = owner;
        }

        void IExtension<ServiceHostBase>.Detach(ServiceHostBase owner)
        {
            if (owner == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(owner));
            }

            if (_owner == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.TheServiceMetadataExtensionInstanceCouldNot3_0));
            }

            if (_owner != owner)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(owner), SR.TheServiceMetadataExtensionInstanceCouldNot4_0);
            }

            if (owner.State != CommunicationState.Created && owner.State != CommunicationState.Opening)
            {
                throw new InvalidOperationException(owner.State.ToString());
            }

            _owner = null;
        }

        internal static ServiceMetadataExtension EnsureServiceMetadataExtension(ServiceHostBase host)
        {
            ServiceMetadataExtension mex = host.Extensions.Find<ServiceMetadataExtension>();
            if (mex == null)
            {
                mex = new ServiceMetadataExtension();
                host.Extensions.Add(mex);
            }

            return mex;
        }

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

        private WriteFilter GetWriteFilter(Message request, Uri listenUri, bool removeBaseAddress)
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
            if (!TryGetHttpHostAndPort(listenUri, httpRequest, out string requestHost, out int requestPort))
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

            return new DynamicAddressUpdateWriter(listenUri, requestHost, requestPort, UpdatePortsByScheme, removeBaseAddress);
        }

        private DynamicAddressUpdateWriter GetDynamicAddressWriter(Message request, Uri listenUri, bool removeBaseAddress)
        {
            HttpContext context = null;
            if (request.Properties.TryGetValue("Microsoft.AspNetCore.Http.HttpContext", out object contextObj))
            {
                context = contextObj as HttpContext;
            }

            if (context==null || !TryGetHttpHostAndPort(listenUri, context.Request, out string requestHost, out int requestPort))
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
            return new DynamicAddressUpdateWriter(listenUri, requestHost, requestPort, UpdatePortsByScheme, removeBaseAddress);
        }

        internal class WSMexImpl : IMetadataExchange
        {
            internal const string MetadataMexBinding = "ServiceMetadataBehaviorMexBinding";
            internal const string ContractName = MetadataStrings.WSTransfer.Name;
            internal const string ContractNamespace = MetadataStrings.WSTransfer.Namespace;
            internal const string GetMethodName = "Get";
            internal const string RequestAction = MetadataStrings.WSTransfer.GetAction;
            internal const string ReplyAction = MetadataStrings.WSTransfer.GetResponseAction;
            private readonly ServiceMetadataExtension _parent;
            private readonly MetadataSet _metadataLocationSet;
            private TypedMessageConverter _converter;
            private readonly Uri _listenUri;

            internal WSMexImpl(ServiceMetadataExtension parent, bool isListeningOnHttps, Uri listenUri)
            {
                _parent = parent;
                IsListeningOnHttps = isListeningOnHttps;
                _listenUri = listenUri;

                if (_parent.ExternalMetadataLocation != null && _parent.ExternalMetadataLocation != s_emptyUri)
                {
                    _metadataLocationSet = new MetadataSet();
                    string location = GetLocationToReturn();
                    MetadataSection metadataLocationSection = new MetadataSection(MetadataSection.ServiceDescriptionDialect, null, new MetadataLocation(location));
                    _metadataLocationSet.MetadataSections.Add(metadataLocationSection);
                }
            }

            internal bool IsListeningOnHttps { get; set; }

            private string GetLocationToReturn()
            {
                Fx.Assert(_parent.ExternalMetadataLocation != null, "");
                Uri location = _parent.ExternalMetadataLocation;

                if (!location.IsAbsoluteUri)
                {
                    Uri httpAddr = _parent._owner.GetVia(Uri.UriSchemeHttp, location);
                    Uri httpsAddr = _parent._owner.GetVia(Uri.UriSchemeHttps, location);

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
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(_parent.ExternalMetadataLocation), SR.Format(SR.SFxBadMetadataLocationNoAppropriateBaseAddress, _parent.ExternalMetadataLocation.OriginalString));
                    }
                }

                return location.ToString();
            }

            private MetadataSet GatherMetadata(string dialect, string identifier)
            {
                if (_metadataLocationSet != null)
                {
                    return _metadataLocationSet;
                }
                else
                {
                    MetadataSet metadataSet = new MetadataSet();
                    foreach (MetadataSection document in _parent.Metadata.MetadataSections)
                    {
                        if ((dialect == null || dialect == document.Dialect) &&
                            (identifier == null || identifier == document.Identifier))
                        {
                            metadataSet.MetadataSections.Add(document);
                        }
                    }

                    return metadataSet;
                }
            }

            public Message Get(Message request)
            {
                GetResponse response = new GetResponse
                {
                    Metadata = GatherMetadata(null, null)
                };

                response.Metadata.WriteFilter = _parent.GetWriteFilter(request, _listenUri, true);

                if (_converter == null)
                {
                    _converter = TypedMessageConverter.Create(typeof(GetResponse), ReplyAction);
                }

                return _converter.ToMessage(response, request.Version);
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
            private const int MaxQueryStringChars = 2048;

            internal const string MetadataHttpGetBinding = "ServiceMetadataBehaviorHttpGetBinding";
            internal const string ContractName = "IHttpGetHelpPageAndMetadataContract";
            internal const string ContractNamespace = "http://schemas.microsoft.com/2006/04/http/metadata";
            internal const string GetMethodName = "Get";
            internal const string RequestAction = "*"; // MessageHeaders.WildcardAction;
            internal const string ReplyAction = "*"; // MessageHeaders.WildcardAction;
            internal const string HtmlBreak = "<BR/>";

            private readonly ServiceMetadataExtension _parent;
            private readonly object _sync = new object();
            private InitializationData _initData;
            private readonly Uri _listenUri;
            private readonly bool _isHttps;

            internal HttpGetImpl(ServiceMetadataExtension parent, Uri listenUri, bool isHttps)
            {
                _parent = parent;
                _listenUri = listenUri;
                GetWsdlEnabled = parent.HttpsGetEnabled || parent.HttpGetEnabled;
                HelpPageEnabled = parent.HelpPageEnabled;
                _isHttps = isHttps;
            }

            public bool HelpPageEnabled { get; set; } = false;

            public bool GetWsdlEnabled { get; set; }

            private InitializationData GetInitData()
            {
                if (_initData == null)
                {
                    lock (_sync)
                    {
                        if (_initData == null)
                        {
                            _initData = InitializationData.InitializeFrom(_parent);
                        }
                    }
                }

                return _initData;
            }

            private string FindWsdlReference(DynamicAddressUpdateWriter addressUpdater)
            {
                if (_parent.ExternalMetadataLocation == null || _parent.ExternalMetadataLocation == s_emptyUri)
                {
                    return null;
                }
                else
                {
                    Uri location = _parent.ExternalMetadataLocation;

                    Uri result = GetUri(_listenUri, location);
                    if (addressUpdater != null)
                    {
                        addressUpdater.UpdateUri(ref result);
                    }

                    return result.ToString();
                }
            }

            private async Task<bool> TryHandleDocumentationRequestAsync(HttpContext requestContext)
            {
                if (!HelpPageEnabled)
                {
                    return false;
                }

                MetadataResult result;
                if (_parent.MetadataEnabled)
                {
                    string discoUrl = null;
                    string singleWsdlUrl = null;
                    bool linkMetadata = true;

                    DynamicAddressUpdateWriter addressUpdater = null;
                    if (_parent.UpdateAddressDynamically)
                    {
                        addressUpdater = _parent.GetDynamicAddressWriter(requestContext.Request, _listenUri, false);
                    }

                    string wsdlUrl = FindWsdlReference(addressUpdater);

                    string httpGetUrl = GetHttpGetUrl(addressUpdater);

                    if (wsdlUrl == null && httpGetUrl != null)
                    {
                        wsdlUrl = httpGetUrl + "?" + WsdlQueryString;
                        singleWsdlUrl = httpGetUrl + "?" + SingleWsdlQueryString;
                    }

                    if (httpGetUrl != null)
                    {
                        discoUrl = httpGetUrl + "?" + DiscoQueryString;
                    }

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
                if (_listenUri.Scheme == Uri.UriSchemeHttp)
                {
                    if (_parent.HttpGetEnabled)
                    {
                        result = _parent.HttpGetUrl;
                    }
                    else if (_parent.HttpsGetEnabled)
                    {
                        result = _parent.HttpsGetUrl;
                    }
                }
                else
                {
                    if (_parent.HttpsGetEnabled)
                    {
                        result = _parent.HttpsGetUrl;
                    }
                    else if (_parent.HttpGetEnabled)
                    {
                        result = _parent.HttpGetUrl;
                    }
                }

                if (result != null)
                {
                    if (addressUpdater != null)
                    {
                        addressUpdater.UpdateUri(ref result, _listenUri.Scheme != result.Scheme /*updateBaseAddressOnly*/);
                    }

                    return result.ToString();
                }

                return null;
            }

            private string GetMexUrl(DynamicAddressUpdateWriter addressUpdater)
            {
                if (_parent.MexEnabled)
                {
                    Uri result = _parent.MexUrl;
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
                {
                    return false;
                }

                WriteFilter writeFilter = _parent.GetWriteFilter(requestContext.Request, _listenUri, false);

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
                if (GetInitData().TryQueryLookup(query, out object doc))
                {
                    if (doc is WsdlNS.ServiceDescription description)
                    {
                        result = new ServiceDescriptionResult(description, writeFilter);
                    }
                    else if (doc is XmlSchema schema)
                    {
                        result = new XmlSchemaResult(schema, writeFilter);
                    }
                    else if (doc is string @string)
                    {
                        if (@string == DiscoToken)
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
                    WsdlNS.ServiceDescription singleWSDL = _parent.SingleWsdl;
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
                Uri wsdlUrlBase = _listenUri;
                if (addressUpdater != null)
                {
                    addressUpdater.UpdateUri(ref wsdlUrlBase);
                }

                string wsdlUrl = wsdlUrlBase.ToString() + "?" + WsdlQueryString;

                Uri docUrl = null;
                if (_listenUri.Scheme == Uri.UriSchemeHttp)
                {
                    if (_parent.HttpHelpPageEnabled)
                    {
                        docUrl = _parent.HttpHelpPageUrl;
                    }
                    else if (_parent.HttpsHelpPageEnabled)
                    {
                        docUrl = _parent.HttpsGetUrl;
                    }
                }
                else
                {
                    if (_parent.HttpsHelpPageEnabled)
                    {
                        docUrl = _parent.HttpsHelpPageUrl;
                    }
                    else if (_parent.HttpHelpPageEnabled)
                    {
                        docUrl = _parent.HttpGetUrl;
                    }
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
                    {
                        query = q.Key;
                    }
                    else if (string.Compare(q.Key, XsdQueryString, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        query = q.Key;
                    }
                    else if (string.Compare(q.Key, SingleWsdlQueryString, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        query = q.Key;
                    }
                    else if (_parent.HelpPageEnabled && (string.Compare(q.Key, DiscoQueryString, StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        query = q.Key;
                    }
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
                {
                    return true;
                }

                if (await TryHandleDocumentationRequestAsync(requestContext))
                {
                    return true;
                }

                return false;
            }

            private class InitializationData
            {
                private readonly Dictionary<string, object> _docFromQuery;
                private readonly Dictionary<object, string> _queryFromDoc;
                private readonly WsdlNS.ServiceDescriptionCollection _wsdls;
                private readonly XmlSchemaSet _xsds;

                public string ServiceName;
                public string ClientName;
                public WsdlNS.ServiceDescription DefaultWsdl;

                private InitializationData(
                    Dictionary<string, object> docFromQuery,
                    Dictionary<object, string> queryFromDoc,
                    WsdlNS.ServiceDescriptionCollection wsdls,
                    XmlSchemaSet xsds)
                {
                    _docFromQuery = docFromQuery;
                    _queryFromDoc = queryFromDoc;
                    _wsdls = wsdls;
                    _xsds = xsds;
                }

                public bool TryQueryLookup(string query, out object doc)
                {
                    return _docFromQuery.TryGetValue(query, out doc);
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
                    {
                        defaultWsdl = someService.ServiceDescription;
                    }

                    // WSDLs
                    {
                        int i = 0;
                        foreach (WsdlNS.ServiceDescription wsdlDoc in wsdls)
                        {
                            string query = WsdlQueryString;
                            if (wsdlDoc != defaultWsdl) // don't count the WSDL at ?WSDL
                            {
                                query += "=wsdl" + (i++).ToString(CultureInfo.InvariantCulture);
                            }

                            docFromQueryInit.Add(query, wsdlDoc);
                            queryFromDocInit.Add(wsdlDoc, query);
                        }
                    }

                    // XSDs
                    {
                        int i = 0;
                        foreach (XmlSchema xsdDoc in xsds.Schemas())
                        {
                            string query = XsdQueryString + "=xsd" + (i++).ToString(CultureInfo.InvariantCulture);
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

                    InitializationData data = new InitializationData(docFromQueryInit, queryFromDocInit, wsdls, xsds)
                    {
                        DefaultWsdl = defaultWsdl,
                        ServiceName = GetAnyWsdlName(wsdls),
                        ClientName = ClientClassGenerator.GetClientClassName(GetAnyContractName(wsdls) ?? "IHello")
                    };

                    return data;
                }

                private static WsdlNS.ServiceDescriptionCollection CollectWsdls(MetadataSet metadata)
                {
                    WsdlNS.ServiceDescriptionCollection wsdls = new WsdlNS.ServiceDescriptionCollection();
                    foreach (MetadataSection section in metadata.MetadataSections)
                    {
                        if (section.Metadata is WsdlNS.ServiceDescription description)
                        {
                            wsdls.Add(description);
                        }
                    }

                    return wsdls;
                }

                private static XmlSchemaSet CollectXsds(MetadataSet metadata)
                {
                    XmlSchemaSet xsds = new XmlSchemaSet
                    {
                        XmlResolver = null
                    };
                    foreach (MetadataSection section in metadata.MetadataSections)
                    {
                        if (section.Metadata is XmlSchema schema)
                        {
                            xsds.Add(schema);
                        }
                    }

                    return xsds;
                }

                internal void FixImportAddresses()
                {
                    // fixup imports and includes with addresses
                    // WSDLs
                    foreach (WsdlNS.ServiceDescription wsdlDoc in _wsdls)
                    {
                        FixImportAddresses(wsdlDoc);
                    }
                    // XSDs
                    foreach (XmlSchema xsdDoc in _xsds.Schemas())
                    {
                        FixImportAddresses(xsdDoc);
                    }
                }

                private void FixImportAddresses(WsdlNS.ServiceDescription wsdlDoc)
                {
                    foreach (WsdlNS.Import import in wsdlDoc.Imports)
                    {
                        if (!string.IsNullOrEmpty(import.Location))
                        {
                            continue;
                        }

                        WsdlNS.ServiceDescription targetDoc = _wsdls[import.Namespace ?? string.Empty];
                        if (targetDoc != null)
                        {
                            string query = _queryFromDoc[targetDoc];
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
                        if (!(o is XmlSchemaExternal external) || !string.IsNullOrEmpty(external.SchemaLocation))
                        {
                            continue;
                        }

                        string targetNs = external is XmlSchemaImport import ? import.Namespace : xsdDoc.TargetNamespace;

                        foreach (XmlSchema targetXsd in _xsds.Schemas(targetNs ?? string.Empty))
                        {
                            if (targetXsd != xsdDoc)
                            {
                                string query = _queryFromDoc[targetXsd];
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

            internal RequestDelegate MetadataMiddleware(RequestDelegate _next)
            {
                string path = _listenUri.AbsolutePath;
                return async context =>
                {
                    if (context.Request.Path != path ||
                        !await HandleRequest(context))
                    {
                        await _next(context);
                    }
                };
            }

            internal async Task<bool> HandleRequest(HttpContext httpContext)
            {
                try
                {
                    if (httpContext.Request.IsHttps != _isHttps) // Prevent handling requests on HTTP url's for HTTPS requests and vice versa
                    {
                        return false;
                    }

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
                {
                    return baseUri;
                }

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
                private readonly string _wsdlAddress;
                private readonly string _docAddress;

                public DiscoResult(string wsdlAddress, string docAddress)
                {
                    _wsdlAddress = wsdlAddress;
                    _docAddress = docAddress;
                }

                protected override void Write(XmlWriter writer)
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("discovery", "http://schemas.xmlsoap.org/disco/");
                    writer.WriteStartElement("contractRef", "http://schemas.xmlsoap.org/disco/scl/");
                    writer.WriteAttributeString("ref", _wsdlAddress);
                    writer.WriteAttributeString("docRef", _docAddress);
                    writer.WriteEndElement(); // </contractRef>
                    writer.WriteEndElement(); // </discovery>
                    writer.WriteEndDocument();
                }
            }

            private class MetadataOnHelpPageResult : MetadataResult
            {
                private readonly string _discoUrl;
                private readonly string _metadataUrl;
                private readonly string _singleWsdlUrl;
                private readonly string _serviceName;
                private readonly string _clientName;
                private readonly bool _linkMetadata;
                private readonly string _errorMessage;
                private readonly ExceptionDetail _exceptionDetail;

                public MetadataOnHelpPageResult(string discoUrl, string metadataUrl, string singleWsdlUrl, string serviceName, string clientName, bool linkMetadata)
                    : base()
                {
                    _discoUrl = discoUrl;
                    _metadataUrl = metadataUrl;
                    _singleWsdlUrl = singleWsdlUrl;
                    _serviceName = serviceName;
                    _clientName = clientName;
                    _linkMetadata = linkMetadata;
                }

                public MetadataOnHelpPageResult(string errorMessage, ExceptionDetail exceptionDetail)
                    : base()
                {
                    _errorMessage = errorMessage;
                    _exceptionDetail = exceptionDetail;
                }

                protected override void Write(XmlWriter writer)
                {
                    HelpPageWriter page = new HelpPageWriter(writer);

                    writer.WriteStartElement("HTML");
                    writer.WriteAttributeString("lang", "en"); // We don't have localized strings so no need to look up "en"
                    writer.WriteStartElement("HEAD");

                    if (!string.IsNullOrEmpty(_discoUrl))
                    {
                        page.WriteDiscoLink(_discoUrl);
                    }

                    page.WriteStyleSheet();

                    page.WriteTitle(!string.IsNullOrEmpty(_serviceName) ? SR.Format(SR.SFxDocExt_MainPageTitle, _serviceName) : SR.SFxDocExt_MainPageTitleNoServiceName);

                    if (!string.IsNullOrEmpty(_errorMessage))
                    {
                        page.WriteError(_errorMessage);

                        if (_exceptionDetail != null)
                        {
                            page.WriteExceptionDetail(_exceptionDetail);
                        }
                    }
                    else
                    {
                        page.WriteToolUsage(_metadataUrl, _singleWsdlUrl, _linkMetadata);
                        page.WriteSampleCode(_clientName);
                    }

                    writer.WriteEndElement(); // BODY
                    writer.WriteEndElement(); // HTML
                }

                private struct HelpPageWriter
                {
                    private readonly XmlWriter _writer;
                    public HelpPageWriter(XmlWriter writer)
                    {
                        _writer = writer;
                    }

                    internal void WriteClass(string className)
                    {
                        _writer.WriteStartElement("font");
                        _writer.WriteAttributeString("color", "black");
                        _writer.WriteString(className);
                        _writer.WriteEndElement(); // font
                    }

                    internal void WriteComment(string comment)
                    {
                        _writer.WriteStartElement("font");
                        _writer.WriteAttributeString("color", "darkgreen");
                        _writer.WriteString(comment);
                        _writer.WriteEndElement(); // font
                    }

                    internal void WriteDiscoLink(string discoUrl)
                    {
                        _writer.WriteStartElement("link");
                        _writer.WriteAttributeString("rel", "alternate");
                        _writer.WriteAttributeString("type", "text/xml");
                        _writer.WriteAttributeString("href", discoUrl);
                        _writer.WriteEndElement(); // link
                    }

                    internal void WriteError(string message)
                    {
                        _writer.WriteStartElement("P");
                        _writer.WriteAttributeString("class", "intro");
                        _writer.WriteString(message);
                        _writer.WriteEndElement(); // P

                    }

                    internal void WriteKeyword(string keyword)
                    {
                        _writer.WriteStartElement("font");
                        _writer.WriteAttributeString("color", "blue");
                        _writer.WriteString(keyword);
                        _writer.WriteEndElement(); // font
                    }

                    internal void WriteSampleCode(string clientName)
                    {
                        _writer.WriteStartElement("P");
                        _writer.WriteAttributeString("class", "intro");
                        _writer.WriteRaw(SR.SFxDocExt_MainPageIntro2);
                        _writer.WriteEndElement(); // P

                        // C#
                        _writer.WriteRaw("<h2 class='intro'>C#</h2><br />");
                        _writer.WriteStartElement("PRE");
                        WriteKeyword("class ");
                        WriteClass("Test\n");
                        _writer.WriteString("{\n");
                        WriteKeyword("    static void ");
                        _writer.WriteString("Main()\n");
                        _writer.WriteString("    {\n");
                        _writer.WriteString("        ");
                        WriteClass(clientName);
                        _writer.WriteString(" client = ");
                        WriteKeyword("new ");
                        WriteClass(clientName);
                        _writer.WriteString("();\n\n");
                        WriteComment("        // " + SR.SFxDocExt_MainPageComment+ "\n\n");
                        WriteComment("        // " + SR.SFxDocExt_MainPageComment2+ "\n");
                        _writer.WriteString("        client.Close();\n");
                        _writer.WriteString("    }\n");
                        _writer.WriteString("}\n");
                        _writer.WriteEndElement(); // PRE
                        _writer.WriteRaw(HtmlBreak);


                        // VB
                        _writer.WriteRaw("<h2 class='intro'>Visual Basic</h2><br />");
                        _writer.WriteStartElement("PRE");
                        WriteKeyword("Class ");
                        WriteClass("Test\n");
                        WriteKeyword("    Shared Sub ");
                        _writer.WriteString("Main()\n");
                        WriteKeyword("        Dim ");
                        _writer.WriteString("client As ");
                        WriteClass(clientName);
                        _writer.WriteString(" = ");
                        WriteKeyword("New ");
                        WriteClass(clientName);
                        _writer.WriteString("()\n");
                        WriteComment("        ' " + SR.SFxDocExt_MainPageComment+ "\n\n");
                        WriteComment("        ' " + SR.SFxDocExt_MainPageComment2+ "\n");
                        _writer.WriteString("        client.Close()\n");
                        WriteKeyword("    End Sub\n");
                        WriteKeyword("End Class");
                        _writer.WriteEndElement(); // PRE
                    }

                    internal void WriteExceptionDetail(ExceptionDetail exceptionDetail)
                    {
                        _writer.WriteStartElement("PRE");
                        _writer.WriteString(exceptionDetail.ToString().Replace("\r", ""));
                        _writer.WriteEndElement(); // PRE
                    }

                    internal void WriteStyleSheet()
                    {
                        _writer.WriteStartElement("STYLE");
                        _writer.WriteAttributeString("type", "text/css");
                        _writer.WriteString("#content{ FONT-SIZE: 0.7em; PADDING-BOTTOM: 2em; MARGIN-LEFT: 30px}");
                        _writer.WriteString("BODY{MARGIN-TOP: 0px; MARGIN-LEFT: 0px; COLOR: #000000; FONT-FAMILY: Verdana; BACKGROUND-COLOR: white}");
                        _writer.WriteString("P{MARGIN-TOP: 0px; MARGIN-BOTTOM: 12px; COLOR: #000000; FONT-FAMILY: Verdana}");
                        _writer.WriteString("PRE{BORDER-RIGHT: #f0f0e0 1px solid; PADDING-RIGHT: 5px; BORDER-TOP: #f0f0e0 1px solid; MARGIN-TOP: -5px; PADDING-LEFT: 5px; FONT-SIZE: 1.2em; PADDING-BOTTOM: 5px; BORDER-LEFT: #f0f0e0 1px solid; PADDING-TOP: 5px; BORDER-BOTTOM: #f0f0e0 1px solid; FONT-FAMILY: Courier New; BACKGROUND-COLOR: #e5e5cc}");
                        _writer.WriteString(".heading1{MARGIN-TOP: 0px; PADDING-LEFT: 15px; FONT-WEIGHT: normal; FONT-SIZE: 26px; MARGIN-BOTTOM: 0px; PADDING-BOTTOM: 3px; MARGIN-LEFT: -30px; WIDTH: 100%; COLOR: #ffffff; PADDING-TOP: 10px; FONT-FAMILY: Tahoma; BACKGROUND-COLOR: #003366}");
                        _writer.WriteString(".intro{display: block; font-size: 1em;}");
                        _writer.WriteEndElement();
                    }

                    internal void WriteTitle(string title)
                    {
                        _writer.WriteElementString("TITLE", title);
                        _writer.WriteEndElement();
                        _writer.WriteStartElement("BODY");
                        _writer.WriteStartElement("DIV");
                        _writer.WriteAttributeString("id", "content");
                        _writer.WriteAttributeString("role", "main");
                        _writer.WriteStartElement("h1");
                        _writer.WriteAttributeString("class", "heading1");
                        _writer.WriteString(title);
                        _writer.WriteEndElement();
                        _writer.WriteRaw(HtmlBreak);

                    }

                    internal void WriteToolUsage(string wsdlUrl, string singleWsdlUrl, bool linkMetadata)
                    {
                        _writer.WriteStartElement("P");
                        _writer.WriteAttributeString("class", "intro");

                        if (wsdlUrl != null)
                        {
                            WriteMetadataAddress(SR.SFxDocExt_MainPageIntro1a, "svcutil.exe ", wsdlUrl, linkMetadata);
                            if (singleWsdlUrl != null)
                            {
                                // ?singleWsdl message
                                _writer.WriteStartElement("P");
                                WriteMetadataAddress(SR.SFxDocExt_MainPageIntroSingleWsdl, null, singleWsdlUrl, linkMetadata);
                                _writer.WriteEndElement();
                            }
                        }
                        else
                        {
                            // no metadata message
                            _writer.WriteRaw(SR.SFxDocExt_MainPageIntro1b);
                        }
                        _writer.WriteEndElement(); // P
                    }

                    private void WriteMetadataAddress(string introductionText, string clientToolName, string wsdlUrl, bool linkMetadata)
                    {
                        _writer.WriteRaw(introductionText);
                        _writer.WriteRaw(HtmlBreak);
                        _writer.WriteStartElement("PRE");
                        if (!string.IsNullOrEmpty(clientToolName))
                        {
                            _writer.WriteString(clientToolName);
                        }

                        if (linkMetadata)
                        {
                            _writer.WriteStartElement("A");
                            _writer.WriteAttributeString("HREF", wsdlUrl);
                        }

                        _writer.WriteString(wsdlUrl);

                        if (linkMetadata)
                        {
                            _writer.WriteEndElement(); // A
                        }

                        _writer.WriteEndElement(); // PRE
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
                            using (var xmlTextWriter = XmlWriter.Create(textWriter, new XmlWriterSettings { Indent = false, OmitXmlDeclaration = true }))
                            {
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
                private readonly WsdlNS.ServiceDescription _description;

                public ServiceDescriptionResult(WsdlNS.ServiceDescription description, WriteFilter responseWriter) : base(responseWriter)
                {
                    _description = description;
                }

                protected override void WriteCore(XmlWriter writer) => _description.Write(writer);
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
            internal XmlWriter Writer { get; set; }
            public abstract WriteFilter CloneWriteFilter();
            public override void Close() => Writer.Close();
            public override void Flush() => Writer.Flush();
            public override string LookupPrefix(string ns) => Writer.LookupPrefix(ns);
            public override void WriteBase64(byte[] buffer, int index, int count) => Writer.WriteBase64(buffer, index, count);
            public override void WriteCData(string text) => Writer.WriteCData(text);
            public override void WriteCharEntity(char ch) => Writer.WriteCharEntity(ch);
            public override void WriteChars(char[] buffer, int index, int count) => Writer.WriteChars(buffer, index, count);
            public override void WriteComment(string text) => Writer.WriteComment(text);
            public override void WriteDocType(string name, string pubid, string sysid, string subset) => Writer.WriteDocType(name, pubid, sysid, subset);
            public override void WriteEndAttribute() => Writer.WriteEndAttribute();
            public override void WriteEndDocument() => Writer.WriteEndDocument();
            public override void WriteEndElement() => Writer.WriteEndElement();
            public override void WriteEntityRef(string name) => Writer.WriteEntityRef(name);
            public override void WriteFullEndElement() => Writer.WriteFullEndElement();
            public override void WriteProcessingInstruction(string name, string text) => Writer.WriteProcessingInstruction(name, text);
            public override void WriteRaw(string data) => Writer.WriteRaw(data);
            public override void WriteRaw(char[] buffer, int index, int count) => Writer.WriteRaw(buffer, index, count);
            public override void WriteStartAttribute(string prefix, string localName, string ns) => Writer.WriteStartAttribute(prefix, localName, ns);
            public override void WriteStartDocument(bool standalone) => Writer.WriteStartDocument(standalone);
            public override void WriteStartDocument() => Writer.WriteStartDocument();
            public override void WriteStartElement(string prefix, string localName, string ns) => Writer.WriteStartElement(prefix, localName, ns);
            public override WriteState WriteState => Writer.WriteState;
            public override void WriteString(string text) => Writer.WriteString(text);
            public override void WriteSurrogateCharEntity(char lowChar, char highChar) => Writer.WriteSurrogateCharEntity(lowChar, highChar);
            public override void WriteWhitespace(string ws) => Writer.WriteWhitespace(ws);
        }

        private class LocationUpdatingWriter : WriteFilter
        {
            private readonly string _oldValue;
            private readonly string _newValue;

            // passing null for newValue filters any string with oldValue as a prefix rather than replacing
            internal LocationUpdatingWriter(string oldValue, string newValue)
            {
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override WriteFilter CloneWriteFilter()
            {
                return new LocationUpdatingWriter(_oldValue, _newValue);
            }

            public override void WriteString(string text)
            {
                if (_newValue != null)
                {
                    text = text.Replace(_oldValue, _newValue);
                }
                else if (text.StartsWith(_oldValue, StringComparison.Ordinal))
                {
                    text = string.Empty;
                }

                base.WriteString(text);
            }
        }

        private class DynamicAddressUpdateWriter : WriteFilter
        {
            private readonly string _oldHostName;
            private readonly string _newHostName;
            private readonly string _newBaseAddress;
            private readonly bool _removeBaseAddress;
            private readonly string _requestScheme;
            private readonly int _requestPort;
            private readonly IDictionary<string, int> _updatePortsByScheme;

            internal DynamicAddressUpdateWriter(Uri listenUri, string requestHost, int requestPort,
                IDictionary<string, int> updatePortsByScheme, bool removeBaseAddress)
                : this(listenUri.Host, requestHost, removeBaseAddress, listenUri.Scheme, requestPort, updatePortsByScheme)
            {
                _newBaseAddress = UpdateUri(listenUri).ToString();
            }

            private DynamicAddressUpdateWriter(string oldHostName, string newHostName, string newBaseAddress, bool removeBaseAddress, string requestScheme,
                int requestPort, IDictionary<string, int> updatePortsByScheme)
                : this(oldHostName, newHostName, removeBaseAddress, requestScheme, requestPort, updatePortsByScheme)
            {
                _newBaseAddress = newBaseAddress;
            }

            private DynamicAddressUpdateWriter(string oldHostName, string newHostName, bool removeBaseAddress, string requestScheme,
                int requestPort, IDictionary<string, int> updatePortsByScheme)
            {
                _oldHostName = oldHostName;
                _newHostName = newHostName;
                _removeBaseAddress = removeBaseAddress;
                _requestScheme = requestScheme;
                _requestPort = requestPort;
                _updatePortsByScheme = updatePortsByScheme;
            }

            public override WriteFilter CloneWriteFilter()
            {
                return new DynamicAddressUpdateWriter(_oldHostName, _newHostName, _newBaseAddress, _removeBaseAddress,
                    _requestScheme, _requestPort, _updatePortsByScheme);
            }

            public override void WriteString(string text)
            {
                if (_removeBaseAddress &&
                    text.StartsWith(BaseAddressPattern, StringComparison.Ordinal))
                {
                    text = string.Empty;
                }
                else if (!_removeBaseAddress &&
                    text.Contains(BaseAddressPattern))
                {
                    text = text.Replace(BaseAddressPattern, _newBaseAddress);
                }
                else if (Uri.TryCreate(text, UriKind.Absolute, out Uri uri))
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
                if (uri.Host != _oldHostName)
                {
                    return null;
                }

                UriBuilder result = new UriBuilder(uri)
                {
                    Host = _newHostName
                };

                if (!updateBaseAddressOnly)
                {
                    int port;
                    if (uri.Scheme == _requestScheme)
                    {
                        port = _requestPort;
                    }
                    else if (!_updatePortsByScheme.TryGetValue(uri.Scheme, out port))
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
