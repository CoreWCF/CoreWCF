// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Runtime;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class WebErrorHandler : IErrorHandler
    {
        private const string HtmlMediaType = "text/html";

        private readonly WebHttpBehavior _webHttpBehavior;
        private readonly ContractDescription _contractDescription;
        private readonly bool _includeExceptionDetailInFaults;

        public WebErrorHandler(WebHttpBehavior webHttpBehavior, ContractDescription contractDescription, bool includeExceptionDetailInFaults)
        {
            _webHttpBehavior = webHttpBehavior;
            _contractDescription = contractDescription;
            _includeExceptionDetailInFaults = includeExceptionDetailInFaults;
        }

        public bool HandleError(Exception error) => false;

        public void ProvideFault(Exception error, MessageVersion version, ref Message fault)
        {
            if (version != MessageVersion.None || error == null)
            {
                return;
            }

            // If the exception is not derived from FaultException and the fault message is already present
            //   then only another error handler could have provided the fault so we should not replace it
            FaultException errorAsFaultException = error as FaultException;
            if (errorAsFaultException == null && fault != null)
            {
                return;
            }

            try
            {
                if (error is IWebFaultException webFaultException)
                {
                    WebOperationContext context = WebOperationContext.Current;
                    context.OutgoingResponse.StatusCode = webFaultException.StatusCode;
                    if (OperationContext.Current.IncomingMessageProperties.TryGetValue(WebHttpDispatchOperationSelector.HttpOperationNamePropertyName, out string operationName))
                    {
                        OperationDescription description = _contractDescription.Operations.Find(operationName);
                        bool isXmlSerializerFaultFormat = WebHttpBehavior.IsXmlSerializerFaultFormat(description);
                        if (isXmlSerializerFaultFormat && WebOperationContext.Current.OutgoingResponse.Format == WebMessageFormat.Json)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.JsonFormatRequiresDataContract, description.Name, description.DeclaringContract.Name, description.DeclaringContract.Namespace)));
                        }

                        WebMessageFormat? nullableFormat = !isXmlSerializerFaultFormat ? context.OutgoingResponse.Format : WebMessageFormat.Xml;
                        WebMessageFormat format = nullableFormat.HasValue ? nullableFormat.Value : _webHttpBehavior.GetResponseFormat(description);
                        if (webFaultException.DetailObject != null)
                        {
                            switch (format)
                            {
                                case WebMessageFormat.Json:
                                    fault = context.CreateJsonResponse(webFaultException.DetailObject, new DataContractJsonSerializer(webFaultException.DetailType, webFaultException.KnownTypes));
                                    break;
                                case WebMessageFormat.Xml:
                                    if (isXmlSerializerFaultFormat)
                                    {
                                        fault = context.CreateXmlResponse(webFaultException.DetailObject, new XmlSerializer(webFaultException.DetailType, webFaultException.KnownTypes));
                                    }
                                    else
                                    {
                                        fault = context.CreateXmlResponse(webFaultException.DetailObject, new DataContractSerializer(webFaultException.DetailType, webFaultException.KnownTypes));
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            if (OperationContext.Current.OutgoingMessageProperties.TryGetValue(HttpResponseMessageProperty.Name, out HttpResponseMessageProperty property) &&
                                property != null)
                            {
                                property.SuppressEntityBody = true;
                            }

                            if (format == WebMessageFormat.Json)
                            {
                                fault.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.JsonProperty);
                            }
                        }
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.OperationNameNotFound));
                    }
                }
                else
                {
                    fault = CreateHtmlResponse(error);
                }

            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                //if (DiagnosticUtility.ShouldTraceWarning)
                //{
                //    DiagnosticUtility.TraceHandledException(new InvalidOperationException(SR.Format(SR.HelpPageFailedToCreateErrorMessage)), TraceEventType.Warning);
                //}

                WebOperationContext.Current.OutgoingResponse.StatusCode = HttpStatusCode.BadRequest;
                fault = CreateHtmlResponse(ex);
            }
        }

        private Message CreateHtmlResponse(Exception error)
        {
            // Note: WebOperationContext may not be present in case of an invalid HTTP request
            Uri helpUri = null;
            if (WebOperationContext.Current != null)
            {
                helpUri = _webHttpBehavior.HelpUri != null ? UriTemplate.RewriteUri(_webHttpBehavior.HelpUri, WebOperationContext.Current.IncomingRequest.Headers[HttpRequestHeader.Host]) : null;
            }

            StreamBodyWriter bodyWriter;
            if (_includeExceptionDetailInFaults)
            {
                bodyWriter = ActionOfStreamBodyWriter.CreateStreamBodyWriter(s => HelpHtmlBuilder.CreateServerErrorPage(helpUri, error).Save(s, SaveOptions.OmitDuplicateNamespaces));
            }
            else
            {
                bodyWriter = ActionOfStreamBodyWriter.CreateStreamBodyWriter(s => HelpHtmlBuilder.CreateServerErrorPage(helpUri, null).Save(s, SaveOptions.OmitDuplicateNamespaces));
            }
            Message response = new HttpStreamMessage(bodyWriter);
            response.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);

            HttpResponseMessageProperty responseProperty = GetResponseProperty(WebOperationContext.Current, response);
            if (responseProperty.StatusCode == HttpStatusCode.OK)
            {
                responseProperty.StatusCode = HttpStatusCode.BadRequest;
            }

            responseProperty.Headers[HttpResponseHeader.ContentType] = HtmlMediaType;
            return response;
        }

        private static HttpResponseMessageProperty GetResponseProperty(WebOperationContext currentContext, Message response)
        {
            HttpResponseMessageProperty responseProperty;
            if (currentContext != null)
            {
                responseProperty = currentContext.OutgoingResponse.MessageProperty;
            }
            else
            {
                responseProperty = new HttpResponseMessageProperty();
                response.Properties.Add(HttpResponseMessageProperty.Name, responseProperty);
            }

            return responseProperty;
        }
    }
}
