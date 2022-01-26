// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using CoreWCF.Channels;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class HttpUnhandledOperationInvoker : IOperationInvoker
    {
        private const string HtmlContentType = "text/html; charset=UTF-8";
        private const string HtmlMediaType = "text/html";

        public bool IsSynchronous => true;

        public object[] AllocateInputs() => new object[1];

        public Uri HelpUri { get; set; }

        public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
        {
            if (!(inputs[0] is Message message))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.HttpUnhandledOperationInvokerCalledWithoutMessage)));
            }

            // We might be here because we desire a redirect...
            Uri newLocation = null;
            Uri to = message.Headers.To;
            if (message.Properties.ContainsKey(WebHttpDispatchOperationSelector.RedirectPropertyName))
            {
                newLocation = message.Properties[WebHttpDispatchOperationSelector.RedirectPropertyName] as Uri;
            }

            if (newLocation != null && to != null)
            {
                // ...redirect
                // TODO: Help
                Message redirectResult = WebOperationContext.Current.CreateStreamResponse(s => HelpHtmlBuilder.CreateTransferRedirectPage(to.AbsoluteUri, newLocation.AbsoluteUri).Save(s, SaveOptions.OmitDuplicateNamespaces), HtmlMediaType);
                WebOperationContext.Current.OutgoingResponse.Location = newLocation.AbsoluteUri;
                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.TemporaryRedirect;
                WebOperationContext.Current.OutgoingResponse.ContentType = HtmlContentType;

                // Note that no exception is thrown along this path, even if the debugger is attached
                //if (DiagnosticUtility.ShouldTraceInformation)
                //{
                //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.WebRequestRedirect,
                //        SR2.GetString(SR2.TraceCodeWebRequestRedirect, to, newLocation));
                //}
                return new ValueTask<(object, object[])>((redirectResult, (object[])null));
            }

            // otherwise we are here to issue either a 404 or a 405
            bool uriMatched = false;
            if (message.Properties.ContainsKey(WebHttpDispatchOperationSelector.HttpOperationSelectorUriMatchedPropertyName))
            {
                uriMatched = (bool)message.Properties[WebHttpDispatchOperationSelector.HttpOperationSelectorUriMatchedPropertyName];
            }

            Message result = null;
            Uri helpUri = HelpUri != null ? UriTemplate.RewriteUri(HelpUri, WebOperationContext.Current.IncomingRequest.Headers[HttpRequestHeader.Host]) : null;
            if (uriMatched)
            {
                WebHttpDispatchOperationSelectorData allowedMethodsData = null;
                if (message.Properties.TryGetValue(WebHttpDispatchOperationSelector.HttpOperationSelectorDataPropertyName, out allowedMethodsData))
                {
                    WebOperationContext.Current.OutgoingResponse.Headers[HttpResponseHeader.Allow] = allowedMethodsData.AllowHeader;
                }
                // TODO: Help
                result = WebOperationContext.Current.CreateStreamResponse(s => HelpHtmlBuilder.CreateMethodNotAllowedPage(helpUri).Save(s, SaveOptions.OmitDuplicateNamespaces), HtmlMediaType);
            }
            else
            {
                // TODO: Help
                result = WebOperationContext.Current.CreateStreamResponse(s => HelpHtmlBuilder.CreateEndpointNotFound(helpUri).Save(s, SaveOptions.OmitDuplicateNamespaces), HtmlMediaType);
            }

            WebOperationContext.Current.OutgoingResponse.StatusCode = uriMatched ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotFound;
            WebOperationContext.Current.OutgoingResponse.ContentType = HtmlContentType;

            try
            {
                if (!uriMatched)
                {
                    if (Debugger.IsAttached)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.WebRequestDidNotMatchOperation,
                            OperationContext.Current.IncomingMessageHeaders.To)));
                    }
                    else
                    {
                        DiagnosticUtility.TraceHandledException(new InvalidOperationException(SR.Format(SR.WebRequestDidNotMatchOperation,
                                OperationContext.Current.IncomingMessageHeaders.To)), TraceEventType.Warning);
                    }
                }
                else
                {
                    if (Debugger.IsAttached)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.WebRequestDidNotMatchMethod,
                            WebOperationContext.Current.IncomingRequest.Method, OperationContext.Current.IncomingMessageHeaders.To)));
                    }
                    else
                    {
                        DiagnosticUtility.TraceHandledException(new InvalidOperationException(SR.Format(SR.WebRequestDidNotMatchMethod,
                                WebOperationContext.Current.IncomingRequest.Method, OperationContext.Current.IncomingMessageHeaders.To)), TraceEventType.Warning);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // catch the exception - its only used for tracing
            }

            return new ValueTask<(object, object[])>((result, (object[])null));
        }
    }
}
