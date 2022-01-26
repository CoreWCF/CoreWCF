// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Description;
using CoreWCF.Runtime;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    public class WebHttpDispatchOperationSelector : IDispatchOperationSelector
    {
        public const string HttpOperationSelectorUriMatchedPropertyName = "UriMatched";
        internal const string HttpOperationSelectorDataPropertyName = "HttpOperationSelectorData";

        public const string HttpOperationNamePropertyName = "HttpOperationName";
        internal const string RedirectOperationName = ""; // always unhandled invoker
        internal const string RedirectPropertyName = "WebHttpRedirect";

        private readonly string _catchAllOperationName = ""; // user UT=* Method=* operation, else unhandled invoker

        private readonly Dictionary<string, UriTemplateTable> _methodSpecificTables; // indexed by the http method name
        private readonly UriTemplateTable _wildcardTable; // this is one of the methodSpecificTables, special-cased for faster access
        private readonly Dictionary<string, UriTemplate> _templates;
        private readonly UriTemplateTable _helpUriTable;

        public WebHttpDispatchOperationSelector(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpoint));
            }

            if (endpoint.Address == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.EndpointAddressCannotBeNull));
            }

            Uri baseUri = endpoint.Address.Uri;
            _methodSpecificTables = new Dictionary<string, UriTemplateTable>();
            _templates = new Dictionary<string, UriTemplate>();

            // TODO: Help.
            WebHttpBehavior webHttpBehavior = ((KeyedByTypeCollection<IEndpointBehavior>)endpoint.EndpointBehaviors).Find<WebHttpBehavior>();
            if (webHttpBehavior != null && webHttpBehavior.HelpEnabled)
            {
                _helpUriTable = new UriTemplateTable(endpoint.ListenUri, HelpPage.GetOperationTemplatePairs());
            }

            Dictionary<WCFKey, string> alreadyHaves = new Dictionary<WCFKey, string>();

            foreach (OperationDescription od in endpoint.Contract.Operations)
            {
                // ignore callback operations
                if (od.Messages[0].Direction == MessageDirection.Input)
                {
                    string method = WebHttpBehavior.GetWebMethod(od);
                    string path = UriTemplateClientFormatter.GetUTStringOrDefault(od);

                    // 

                    if (UriTemplateHelpers.IsWildcardPath(path) && (method == WebHttpBehavior.WildcardMethod))
                    {
                        if (_catchAllOperationName != "")
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                new InvalidOperationException(
                                SR.Format(SR.MultipleOperationsInContractWithPathMethod,
                                endpoint.Contract.Name, path, method)));
                        }
                        _catchAllOperationName = od.Name;
                    }

                    UriTemplate ut = new UriTemplate(path);
                    WCFKey wcfKey = new WCFKey(ut, method);
                    if (alreadyHaves.ContainsKey(wcfKey))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                            new InvalidOperationException(
                            SR.Format(SR.MultipleOperationsInContractWithPathMethod,
                            endpoint.Contract.Name, path, method)));
                    }

                    alreadyHaves.Add(wcfKey, od.Name);

                    if (!_methodSpecificTables.TryGetValue(method, out UriTemplateTable methodSpecificTable))
                    {
                        methodSpecificTable = new UriTemplateTable(baseUri);
                        _methodSpecificTables.Add(method, methodSpecificTable);
                    }

                    methodSpecificTable.KeyValuePairs.Add(new KeyValuePair<UriTemplate, object>(ut, od.Name));
                    _templates.Add(od.Name, ut);
                }
            }

            if (_methodSpecificTables.Count == 0)
            {
                _methodSpecificTables = null;
            }
            else
            {
                // freeze all the tables because they should not be modified after this point
                foreach (UriTemplateTable table in _methodSpecificTables.Values)
                {
                    table.MakeReadOnly(true /* allowDuplicateEquivalentUriTemplates */);
                }

                if (!_methodSpecificTables.TryGetValue(WebHttpBehavior.WildcardMethod, out _wildcardTable))
                {
                    _wildcardTable = null;
                }
            }
        }

        protected WebHttpDispatchOperationSelector()
        {
        }

        public virtual UriTemplate GetUriTemplate(string operationName)
        {
            if (operationName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operationName));
            }

            if (!_templates.TryGetValue(operationName, out UriTemplate result))
            {
                return null;
            }
            else
            {
                return result;
            }
        }

        public string SelectOperation(ref Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            string result = SelectOperation(ref message, out bool uriMatched);
            message.Properties.Add(HttpOperationSelectorUriMatchedPropertyName, uriMatched);
            if (result != null)
            {
                message.Properties.Add(HttpOperationNamePropertyName, result);
//                if (DiagnosticUtility.ShouldTraceInformation)
//                {
//#pragma warning disable 56506 // Microsoft, Message.Headers is never null
//                    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.WebRequestMatchesOperation, SR2.GetString(SR2.TraceCodeWebRequestMatchesOperation, message.Headers.To, result));
//#pragma warning restore 56506
//                }
            }

            return result;
        }

        protected virtual string SelectOperation(ref Message message, out bool uriMatched)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            uriMatched = false;
            if (_methodSpecificTables == null)
            {
                return _catchAllOperationName;
            }

            if (!message.Properties.ContainsKey(HttpRequestMessageProperty.Name))
            {
                return _catchAllOperationName;
            }

            if (!(message.Properties[HttpRequestMessageProperty.Name] is HttpRequestMessageProperty prop))
            {
                return _catchAllOperationName;
            }

            string method = prop.Method;

            Uri to = message.Headers.To;

            if (to == null)
            {
                return _catchAllOperationName;
            }

            // TODO: Help
            if (_helpUriTable != null)
            {
                UriTemplateMatch match = _helpUriTable.MatchSingle(to);
                if (match != null)
                {
                    uriMatched = true;
                    AddUriTemplateMatch(match, prop, message);
                    if (method == WebHttpBehavior.GET)
                    {
                        return HelpOperationInvoker.OperationName;
                    }
                    message.Properties.Add(HttpOperationSelectorDataPropertyName,
                        new WebHttpDispatchOperationSelectorData() { AllowedMethods = new List<string>() { WebHttpBehavior.GET } });
                    return _catchAllOperationName;
                }
            }

            bool methodMatchesExactly = _methodSpecificTables.TryGetValue(method, out UriTemplateTable methodSpecificTable);
            if (methodMatchesExactly)
            {
                uriMatched = CanUriMatch(methodSpecificTable, to, prop, message, out string operationName);
                if (uriMatched)
                {
                    return operationName;
                }
            }

            if (_wildcardTable != null)
            {
                uriMatched = CanUriMatch(_wildcardTable, to, prop, message, out string operationName);
                if (uriMatched)
                {
                    return operationName;
                }
            }

            if (ShouldRedirectToUriWithSlashAtTheEnd(methodSpecificTable, message, to))
            {
                return RedirectOperationName;
            }

            // the {method, uri} pair does not match anything the service supports.
            // we know at this point that we'll return some kind of error code, but we 
            // should go through all methods for the uri to see if any method is supported
            // so that that information could be returned to the user as well

            List<string> allowedMethods = null;
            foreach (KeyValuePair<string, UriTemplateTable> pair in _methodSpecificTables)
            {
                if (pair.Key == method || pair.Key == WebHttpBehavior.WildcardMethod)
                {
                    // the uri must not match the uri template
                    continue;
                }

                UriTemplateTable table = pair.Value;
                if (table.MatchSingle(to) != null)
                {
                    if (allowedMethods == null)
                    {
                        allowedMethods = new List<string>();
                    }

                    // 

                    if (!allowedMethods.Contains(pair.Key))
                    {
                        allowedMethods.Add(pair.Key);
                    }
                }
            }

            if (allowedMethods != null)
            {
                uriMatched = true;
                message.Properties.Add(HttpOperationSelectorDataPropertyName,
                    new WebHttpDispatchOperationSelectorData() { AllowedMethods = allowedMethods });
            }

            return _catchAllOperationName;
        }

        private bool CanUriMatch(UriTemplateTable methodSpecificTable, Uri to, HttpRequestMessageProperty prop, Message message, out string operationName)
        {
            operationName = null;
            UriTemplateMatch result = methodSpecificTable.MatchSingle(to);

            if (result != null)
            {
                operationName = result.Data as string;
                Fx.Assert(operationName != null, "bad result");
                AddUriTemplateMatch(result, prop, message);
                return true;
            }

            return false;
        }

        private void AddUriTemplateMatch(UriTemplateMatch match, HttpRequestMessageProperty requestProp, Message message)
        {
            match.SetBaseUri(match.BaseUri, requestProp);
            message.Properties.Add(IncomingWebRequestContext.UriTemplateMatchResultsPropertyName, match);
        }

        private bool ShouldRedirectToUriWithSlashAtTheEnd(UriTemplateTable methodSpecificTable, Message message, Uri to)
        {
            UriBuilder ub = new UriBuilder(to);
            if (ub.Path.EndsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            ub.Path = ub.Path + "/";
            Uri originalPlusSlash = ub.Uri;

            bool result = false;
            if (methodSpecificTable != null && methodSpecificTable.MatchSingle(originalPlusSlash) != null)
            {
                // as an optimization, we check the table that matched the request's method
                // first, as it is more probable that a hit happens there
                result = true;
            }
            else
            {
                // back-compat:
                // we will redirect as long as there is any method 
                // - not necessary the one the user is looking for -
                // that matches the uri with a slash at the end

                foreach (KeyValuePair<string, UriTemplateTable> pair in _methodSpecificTables)
                {
                    UriTemplateTable table = pair.Value;
                    if (table != methodSpecificTable && table.MatchSingle(originalPlusSlash) != null)
                    {
                        result = true;
                        break;
                    }
                }
            }

            if (result)
            {
                string hostAndPort = GetAuthority(message);
                originalPlusSlash = UriTemplate.RewriteUri(ub.Uri, hostAndPort);
                message.Properties.Add(RedirectPropertyName, originalPlusSlash);
            }

            return result;
        }

        private static string GetAuthority(Message message)
        {
            string hostName = null;
            if (message.Properties.TryGetValue(HttpRequestMessageProperty.Name, out HttpRequestMessageProperty requestProperty))
            {
                hostName = requestProperty.Headers[HttpRequestHeader.Host];
                if (!string.IsNullOrEmpty(hostName))
                {
                    return hostName;
                }
            }

            return hostName;
        }

        // to enforce that no two ops have same UriTemplate & Method
        internal class WCFKey
        {
            private readonly string _method;
            private readonly UriTemplate _uriTemplate;

            public WCFKey(UriTemplate uriTemplate, string method)
            {
                _uriTemplate = uriTemplate;
                _method = method;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is WCFKey other))
                {
                    return false;
                }

                return _uriTemplate.IsEquivalentTo(other._uriTemplate) && _method == other._method;
            }

            public override int GetHashCode() => UriTemplateEquivalenceComparer.Instance.GetHashCode(_uriTemplate);
        }
    }
}
