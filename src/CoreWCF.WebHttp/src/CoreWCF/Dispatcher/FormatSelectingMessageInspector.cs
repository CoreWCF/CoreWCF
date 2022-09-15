// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Runtime;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class FormatSelectingMessageInspector : IDispatchMessageInspector
    {
        private static readonly IEnumerable<string> s_wildcardMediaTypes = new List<string>() { "application", "text" };
        private readonly List<MultiplexingFormatMapping> _mappings;
        private readonly Dictionary<string, MultiplexingDispatchMessageFormatter> _formatters;
        private readonly Dictionary<string, NameValueCache<FormatContentTypePair>> _caches;
        private readonly bool _automaticFormatSelectionEnabled;

        // There are technically an infinite number of valid accept headers for just xml and json,
        // but to prevent DOS attacks, we need to set an upper limit. It is assumed that there would 
        // never be more than two dozen valid accept headers actually used out in the wild.
        private static readonly int s_maxCachedAcceptHeaders = 25;

        public FormatSelectingMessageInspector(WebHttpBehavior webHttpBehavior, List<MultiplexingFormatMapping> mappings)
        {
            if (webHttpBehavior == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(webHttpBehavior));
            }

            _automaticFormatSelectionEnabled = webHttpBehavior.AutomaticFormatSelectionEnabled;

            _formatters = new Dictionary<string, MultiplexingDispatchMessageFormatter>();

            _caches = new Dictionary<string, NameValueCache<FormatContentTypePair>>();

            _mappings = mappings ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(mappings));
        }

        public void RegisterOperation(string operationName, MultiplexingDispatchMessageFormatter formatter)
        {
            if (formatter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(formatter));
            }

            Fx.Assert(!_formatters.ContainsKey(operationName), "An operation should only be registered once.");

            _formatters.Add(operationName, formatter);
            _caches.Add(operationName, new NameValueCache<FormatContentTypePair>(s_maxCachedAcceptHeaders));
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            if (_automaticFormatSelectionEnabled)
            {
                MessageProperties messageProperties = OperationContext.Current.IncomingMessageProperties;
                if (messageProperties.ContainsKey(WebHttpDispatchOperationSelector.HttpOperationNamePropertyName))
                {
                    string operationName = messageProperties[WebHttpDispatchOperationSelector.HttpOperationNamePropertyName] as string;
                    if (!string.IsNullOrEmpty(operationName) && _formatters.ContainsKey(operationName))
                    {

                        string acceptHeader = WebOperationContext.Current.IncomingRequest.Accept;
                        if (!string.IsNullOrEmpty(acceptHeader))
                        {
                            if (TrySetFormatFromCache(operationName, acceptHeader) ||
                                TrySetFormatFromAcceptHeader(operationName, acceptHeader, true /* matchCharSet */) ||
                                TrySetFormatFromAcceptHeader(operationName, acceptHeader, false /* matchCharSet */))
                            {
                                return null;
                            }
                        }

                        if (TrySetFormatFromContentType(operationName))
                        {
                            return null;
                        }

                        SetFormatFromDefault(operationName);
                    }
                }
            }

            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            // do nothing
        }

        private bool TrySetFormatFromCache(string operationName, string acceptHeader)
        {
            Fx.Assert(_caches.ContainsKey(operationName), "The calling method is responsible for ensuring that the 'operationName' key exists in the caches dictionary.");
            Fx.Assert(acceptHeader != null, "The calling method is responsible for ensuring that 'acceptHeader' is not null");

            FormatContentTypePair pair = _caches[operationName].Lookup(acceptHeader.ToUpperInvariant());
            if (pair != null)
            {
                SetFormatAndContentType(pair.Format, pair.ContentType);
                return true;
            }

            return false;
        }

        private bool TrySetFormatFromAcceptHeader(string operationName, string acceptHeader, bool matchCharSet)
        {
            Fx.Assert(_formatters.ContainsKey(operationName), "The calling method is responsible for ensuring that the 'operationName' key exists in the formatters dictionary.");

            IList<ContentType> acceptHeaderElements = WebOperationContext.Current.IncomingRequest.GetAcceptHeaderElements();

            for (int i = 0; i < acceptHeaderElements.Count; i++)
            {
                string[] typeAndSubType = acceptHeaderElements[i].MediaType.Split('/');
                string type = typeAndSubType[0].Trim().ToLowerInvariant();
                string subType = typeAndSubType[1].Trim();

                if ((subType[0] == '*' && subType.Length == 1) &&
                     ((type[0] == '*' && type.Length == 1) ||
                      s_wildcardMediaTypes.Contains(type)))
                {
                    SetFormatFromDefault(operationName, acceptHeader);
                    return true;
                }

                foreach (MultiplexingFormatMapping mapping in _mappings)
                {
                    WebMessageFormat format = mapping.MessageFormat;
                    if (_formatters[operationName].SupportsMessageFormat(format) &&
                        mapping.CanFormatResponse(acceptHeaderElements[i], matchCharSet, out ContentType contentType))
                    {
                        string contentTypeStr = contentType.ToString();
                        _caches[operationName].AddOrUpdate(acceptHeader.ToUpperInvariant(), new FormatContentTypePair(format, contentTypeStr));
                        SetFormatAndContentType(format, contentTypeStr);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TrySetFormatFromContentType(string operationName)
        {
            Fx.Assert(_formatters.ContainsKey(operationName), "The calling method is responsible for ensuring that the 'operationName' key exists in the formatters dictionary.");

            string contentTypeStr = WebOperationContext.Current.IncomingRequest.ContentType;
            if (contentTypeStr != null)
            {
                ContentType contentType = Utility.GetContentType(contentTypeStr);
                if (contentType != null)
                {
                    foreach (MultiplexingFormatMapping mapping in _mappings)
                    {
                        if (_formatters[operationName].SupportsMessageFormat(mapping.MessageFormat) &&
                            mapping.CanFormatResponse(contentType, false, out ContentType responseContentType))
                        {
                            SetFormatAndContentType(mapping.MessageFormat, responseContentType.ToString());
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void SetFormatFromDefault(string operationName)
        {
            SetFormatFromDefault(operationName, null);
        }

        private  void SetFormatFromDefault(string operationName, string acceptHeader)
        {
            Fx.Assert(_formatters.ContainsKey(operationName), "The calling method is responsible for ensuring that the 'operationName' key exists in the formatters dictionary.");
            WebMessageFormat format = _formatters[operationName].DefaultFormat;

            if (!string.IsNullOrEmpty(acceptHeader))
            {
                _caches[operationName].AddOrUpdate(acceptHeader.ToUpperInvariant(), new FormatContentTypePair(format, null));
            }

            WebOperationContext.Current.OutgoingResponse.Format = format;

            //if (DiagnosticUtility.ShouldTraceInformation)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.AutomaticFormatSelectedOperationDefault, SR2.GetString(SR2.TraceCodeAutomaticFormatSelectedOperationDefault, format.ToString()));
            //}
        }

        private void SetFormatAndContentType(WebMessageFormat format, string contentType)
        {
            OutgoingWebResponseContext outgoingResponse = WebOperationContext.Current.OutgoingResponse;
            outgoingResponse.Format = format;
            outgoingResponse.AutomatedFormatSelectionContentType = contentType;

            //if (DiagnosticUtility.ShouldTraceInformation)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.AutomaticFormatSelectedRequestBased, SR2.GetString(SR2.TraceCodeAutomaticFormatSelectedRequestBased, format.ToString(), contentType));
            //}
        }

        internal class FormatContentTypePair
        {
            public FormatContentTypePair(WebMessageFormat format, string contentType)
            {
                Format = format;
                ContentType = contentType;
            }

            public WebMessageFormat Format { get; }

            public string ContentType { get; }
        }
    }
}
