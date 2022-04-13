// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Mime;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Web
{
    public class IncomingWebRequestContext
    {
        private static readonly string s_httpGetMethod = "GET";
        private static readonly string s_httpHeadMethod = "HEAD";
        private static readonly string s_httpPutMethod = "PUT";
        private static readonly string s_httpPostMethod = "POST";
        private static readonly string s_httpDeleteMethod = "DELETE";

        private Collection<ContentType> _cachedAcceptHeaderElements;
        private string _acceptHeaderWhenHeaderElementsCached;
        private readonly OperationContext _operationContext;

        internal const string UriTemplateMatchResultsPropertyName = "UriTemplateMatchResults";

        internal IncomingWebRequestContext(OperationContext operationContext)
        {
            Fx.Assert(operationContext != null, "operationContext is null");
            _operationContext = operationContext;
        }

        public string Accept => EnsureMessageProperty().Headers[HttpRequestHeader.Accept];

        public long ContentLength => long.Parse(EnsureMessageProperty().Headers[HttpRequestHeader.ContentLength], CultureInfo.InvariantCulture);

        public string ContentType => EnsureMessageProperty().Headers[HttpRequestHeader.ContentType];

        public IEnumerable<string> IfMatch
        {
            get
            {
                string ifMatchHeader = MessageProperty.Headers[HttpRequestHeader.IfMatch];
                return (string.IsNullOrEmpty(ifMatchHeader)) ? null : Utility.QuoteAwareStringSplit(ifMatchHeader);
            }
        }

        public IEnumerable<string> IfNoneMatch
        {
            get
            {
                string ifNoneMatchHeader = MessageProperty.Headers[HttpRequestHeader.IfNoneMatch];
                return (string.IsNullOrEmpty(ifNoneMatchHeader)) ? null : Utility.QuoteAwareStringSplit(ifNoneMatchHeader);
            }
        }

        public DateTime? IfModifiedSince
        {
            get
            {
                string dateTime = this.MessageProperty.Headers[HttpRequestHeader.IfModifiedSince];
                if (!string.IsNullOrEmpty(dateTime))
                {
                    if (HttpDateParse.ParseHttpDate(dateTime, out DateTime parsedDateTime))
                    {
                        return parsedDateTime;
                    }
                }

                return null;
            }
        }

        public DateTime? IfUnmodifiedSince
        {
            get
            {
                string dateTime = MessageProperty.Headers[HttpRequestHeader.IfUnmodifiedSince];
                if (!string.IsNullOrEmpty(dateTime))
                {
                    if (HttpDateParse.ParseHttpDate(dateTime, out DateTime parsedDateTime))
                    {
                        return parsedDateTime;
                    }
                }

                return null;
            }
        }

        public WebHeaderCollection Headers => EnsureMessageProperty().Headers;

        public string Method => EnsureMessageProperty().Method;

        public UriTemplateMatch UriTemplateMatch
        {
            get
            {
                if (_operationContext.IncomingMessageProperties.ContainsKey(UriTemplateMatchResultsPropertyName))
                {
                    return _operationContext.IncomingMessageProperties[UriTemplateMatchResultsPropertyName] as UriTemplateMatch;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                _operationContext.IncomingMessageProperties[UriTemplateMatchResultsPropertyName] = value;
            }
        }

        public string UserAgent => EnsureMessageProperty().Headers[HttpRequestHeader.UserAgent];

        private HttpRequestMessageProperty MessageProperty
        {
            get
            {
                if (_operationContext.IncomingMessageProperties == null)
                {
                    return null;
                }

                if (!_operationContext.IncomingMessageProperties.ContainsKey(HttpRequestMessageProperty.Name))
                {
                    return null;
                }

                return _operationContext.IncomingMessageProperties[HttpRequestMessageProperty.Name] as HttpRequestMessageProperty;
            }
        }

        public void CheckConditionalRetrieve(string entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtagFromString(entityTag);
            CheckConditionalRetrieveWithValidatedEtag(validEtag);
        }

        public void CheckConditionalRetrieve(int entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtag(entityTag);
            CheckConditionalRetrieveWithValidatedEtag(validEtag);
        }

        public void CheckConditionalRetrieve(long entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtag(entityTag);
            CheckConditionalRetrieveWithValidatedEtag(validEtag);
        }

        public void CheckConditionalRetrieve(Guid entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtag(entityTag);
            CheckConditionalRetrieveWithValidatedEtag(validEtag);
        }

        public void CheckConditionalRetrieve(DateTime lastModified)
        {
            if (!string.Equals(Method, s_httpGetMethod, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Method, s_httpHeadMethod, StringComparison.OrdinalIgnoreCase))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.ConditionalRetrieveGetAndHeadOnly, Method)));
            }

            DateTime? ifModifiedSince = IfModifiedSince;
            if (ifModifiedSince.HasValue)
            {
                long ticksDifference = lastModified.ToUniversalTime().Ticks - ifModifiedSince.Value.ToUniversalTime().Ticks;
                if (ticksDifference < TimeSpan.TicksPerSecond)
                {
                    WebOperationContext.Current.OutgoingResponse.LastModified = lastModified;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new WebFaultException(HttpStatusCode.NotModified));
                }
            }
        }

        public void CheckConditionalUpdate(string entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtagFromString(entityTag);
            CheckConditionalUpdateWithValidatedEtag(validEtag);
        }

        public void CheckConditionalUpdate(int entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtag(entityTag);
            CheckConditionalUpdateWithValidatedEtag(validEtag);
        }

        public void CheckConditionalUpdate(long entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtag(entityTag);
            CheckConditionalUpdateWithValidatedEtag(validEtag);
        }

        public void CheckConditionalUpdate(Guid entityTag)
        {
            string validEtag = OutgoingWebResponseContext.GenerateValidEtag(entityTag);
            CheckConditionalUpdateWithValidatedEtag(validEtag);
        }

        public Collection<ContentType> GetAcceptHeaderElements()
        {
            string acceptHeader = Accept;
            if (_cachedAcceptHeaderElements == null ||
                (!string.Equals(_acceptHeaderWhenHeaderElementsCached, acceptHeader, StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrEmpty(acceptHeader))
                {
                    _cachedAcceptHeaderElements = new Collection<ContentType>();
                    _acceptHeaderWhenHeaderElementsCached = acceptHeader;
                }
                else
                {
                    List<ContentType> contentTypeList = new List<ContentType>();
                    int offset = 0;
                    while (true)
                    {
                        string nextItem = Utility.QuoteAwareSubString(acceptHeader, ref offset);
                        if (nextItem == null)
                        {
                            break;
                        }

                        ContentType contentType = Utility.GetContentTypeOrNull(nextItem);
                        if (contentType != null)
                        {
                            contentTypeList.Add(contentType);
                        }
                    }

                    contentTypeList.Sort(new AcceptHeaderElementComparer());
                    _cachedAcceptHeaderElements = new Collection<ContentType>(contentTypeList);
                    _acceptHeaderWhenHeaderElementsCached = acceptHeader;
                }
            }

            return _cachedAcceptHeaderElements;
        }

        private HttpRequestMessageProperty EnsureMessageProperty()
        {
            if (MessageProperty == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.HttpContextNoIncomingMessageProperty, typeof(HttpRequestMessageProperty).Name)));
            }

            return MessageProperty;
        }


        private void CheckConditionalRetrieveWithValidatedEtag(string entityTag)
        {
            if (!string.Equals(Method, s_httpGetMethod, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Method, s_httpHeadMethod, StringComparison.OrdinalIgnoreCase))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.ConditionalRetrieveGetAndHeadOnly, Method)));
            }

            if (!string.IsNullOrEmpty(entityTag))
            {
                string entityTagHeader = Headers[HttpRequestHeader.IfNoneMatch];
                if (!string.IsNullOrEmpty(entityTagHeader))
                {
                    if (IsWildCardCharacter(entityTagHeader) ||
                        DoesHeaderContainEtag(entityTagHeader, entityTag))
                    {
                        // set response entityTag directly because it has already been validated
                        WebOperationContext.Current.OutgoingResponse.ETag = entityTag;
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new WebFaultException(HttpStatusCode.NotModified));
                    }
                }
            }
        }

        private void CheckConditionalUpdateWithValidatedEtag(string entityTag)
        {
            bool isPutMethod = string.Equals(Method, s_httpPutMethod, StringComparison.OrdinalIgnoreCase);
            if (!isPutMethod &&
                !string.Equals(Method, s_httpPostMethod, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Method, s_httpDeleteMethod, StringComparison.OrdinalIgnoreCase))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.ConditionalUpdatePutPostAndDeleteOnly, Method)));
            }

            string headerOfInterest;

            // if the current entityTag is null then the resource doesn't currently exist and the
            //   a PUT request should only succeed if If-None-Match equals '*'.  
            if (isPutMethod && string.IsNullOrEmpty(entityTag))
            {
                headerOfInterest = Headers[HttpRequestHeader.IfNoneMatch];
                if (string.IsNullOrEmpty(headerOfInterest) ||
                    !IsWildCardCharacter(headerOfInterest))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new WebFaultException(HttpStatusCode.PreconditionFailed));
                }
            }
            else
            {
                // all remaining cases are with an If-Match header
                headerOfInterest = Headers[HttpRequestHeader.IfMatch];
                if (string.IsNullOrEmpty(headerOfInterest) ||
                    (!IsWildCardCharacter(headerOfInterest) &&
                    !DoesHeaderContainEtag(headerOfInterest, entityTag)))
                {
                    // set response entityTag directly because it has already been validated
                    WebOperationContext.Current.OutgoingResponse.ETag = entityTag;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new WebFaultException(HttpStatusCode.PreconditionFailed));
                }
            }
        }

        private static bool DoesHeaderContainEtag(string header, string entityTag)
        {
            int offset = 0;
            while (true)
            {
                string nextEntityTag = Utility.QuoteAwareSubString(header, ref offset);
                if (nextEntityTag == null)
                {
                    break;
                }

                if (string.Equals(nextEntityTag, entityTag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWildCardCharacter(string header) => header.Trim() == "*";
    }

    internal class AcceptHeaderElementComparer : IComparer<ContentType>
    {
        private static readonly NumberStyles s_numberStyles = NumberStyles.AllowDecimalPoint;

        public int Compare(ContentType x, ContentType y)
        {
            string[] xTypeSubType = x.MediaType.Split('/');
            string[] yTypeSubType = y.MediaType.Split('/');

            Fx.Assert(xTypeSubType.Length == 2, "The creation of the ContentType would have failed if there wasn't a type and subtype.");
            Fx.Assert(yTypeSubType.Length == 2, "The creation of the ContentType would have failed if there wasn't a type and subtype.");

            if (string.Equals(xTypeSubType[0], yTypeSubType[0], StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(xTypeSubType[1], yTypeSubType[1], StringComparison.OrdinalIgnoreCase))
                {
                    // need to check the number of parameters to determine which is more specific
                    bool xHasParam = HasParameters(x);
                    bool yHasParam = HasParameters(y);
                    if (xHasParam && !yHasParam)
                    {
                        return 1;
                    }
                    else if (!xHasParam && yHasParam)
                    {
                        return -1;
                    }
                }
                else
                {
                    if (xTypeSubType[1][0] == '*' && xTypeSubType[1].Length == 1)
                    {
                        return 1;
                    }
                    if (yTypeSubType[1][0] == '*' && yTypeSubType[1].Length == 1)
                    {
                        return -1;
                    }
                }
            }
            else if (xTypeSubType[0][0] == '*' && xTypeSubType[0].Length == 1)
            {
                return 1;
            }
            else if (yTypeSubType[0][0] == '*' && yTypeSubType[0].Length == 1)
            {
                return -1;
            }

            decimal qualityDifference = GetQualityFactor(x) - GetQualityFactor(y);
            if (qualityDifference < 0)
            {
                return 1;
            }
            else if (qualityDifference > 0)
            {
                return -1;
            }

            return 0;
        }

        private decimal GetQualityFactor(ContentType contentType)
        {
            foreach (string key in contentType.Parameters.Keys)
            {
                if (string.Equals("q", key, StringComparison.OrdinalIgnoreCase))
                {
                    if (decimal.TryParse(contentType.Parameters[key], s_numberStyles, CultureInfo.InvariantCulture, out decimal result) &&
                        (result <= (decimal)1.0))
                    {
                        return result;
                    }
                }
            }

            return (decimal)1.0;
        }

        private bool HasParameters(ContentType contentType)
        {
            int number = 0;
            foreach (string param in contentType.Parameters.Keys)
            {
                if (!string.Equals("q", param, StringComparison.OrdinalIgnoreCase))
                {
                    number++;
                }
            }

            return number > 0;
        }
    }
}
