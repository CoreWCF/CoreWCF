// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class UriTemplateDispatchFormatter : IDispatchMessageFormatter
    {
        internal Dictionary<int, string> _pathMapping;
        internal Dictionary<int, KeyValuePair<string, Type>> _queryMapping;
        private readonly Uri _baseAddress;
        private readonly IDispatchMessageFormatter _inner;
        private readonly string _operationName;
        private readonly QueryStringConverter _qsc;
        private readonly int _totalNumUTVars;
        private readonly UriTemplate _uriTemplate;

        public UriTemplateDispatchFormatter(OperationDescription operationDescription, IDispatchMessageFormatter inner, QueryStringConverter qsc, string contractName, Uri baseAddress)
        {
            _inner = inner;
            _qsc = qsc;
            _baseAddress = baseAddress;
            _operationName = operationDescription.Name;
            UriTemplateClientFormatter.Populate(out _pathMapping,
                out _queryMapping,
                out _totalNumUTVars,
                out _uriTemplate,
                operationDescription,
                qsc,
                contractName);
        }

        public void DeserializeRequest(Message message, object[] parameters)
        {
            object[] innerParameters = new object[parameters.Length - _totalNumUTVars];
            if (innerParameters.Length != 0)
            {
                _inner.DeserializeRequest(message, innerParameters);
            }

            int j = 0;
            UriTemplateMatch utmr = null;
            string UTMRName = IncomingWebRequestContext.UriTemplateMatchResultsPropertyName;
            if (message.Properties.ContainsKey(UTMRName))
            {
                utmr = message.Properties[UTMRName] as UriTemplateMatch;
            }
            else
            {
                if (message.Headers.To != null && message.Headers.To.IsAbsoluteUri)
                {
                    utmr = _uriTemplate.Match(_baseAddress, message.Headers.To);
                }
            }

            NameValueCollection nvc = (utmr == null) ? new NameValueCollection() : utmr.BoundVariables;
            for (int i = 0; i < parameters.Length; ++i)
            {
                if (_pathMapping.ContainsKey(i) && utmr != null)
                {
                    parameters[i] = nvc[_pathMapping[i]];
                }
                else if (_queryMapping.ContainsKey(i) && utmr != null)
                {
                    string queryVal = nvc[_queryMapping[i].Key];
                    parameters[i] = _qsc.ConvertStringToValue(queryVal, _queryMapping[i].Value);
                }
                else
                {
                    parameters[i] = innerParameters[j];
                    ++j;
                }
            }

            //if (DiagnosticUtility.ShouldTraceInformation)
            //{
            //    if (utmr != null)
            //    {
            //        foreach (string key in utmr.QueryParameters.Keys)
            //        {
            //            bool isParameterIgnored = true;
            //            foreach (KeyValuePair<string, Type> kvp in this._queryMapping.Values)
            //            {
            //                if (String.Compare(key, kvp.Key, StringComparison.OrdinalIgnoreCase) == 0)
            //                {
            //                    isParameterIgnored = false;
            //                    break;
            //                }
            //            }
            //            if (isParameterIgnored)
            //            {
            //                TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.WebUnknownQueryParameterIgnored, SR2.GetString(SR2.TraceCodeWebRequestUnknownQueryParameterIgnored, key, _operationName));
            //            }
            //        }
            //    }
            //}
        }

        public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.QueryStringFormatterOperationNotSupportedServerSide)));
    }
}
