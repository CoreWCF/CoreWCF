// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Runtime;

namespace CoreWCF.Web
{
    public class OutgoingWebResponseContext
    {
        internal static readonly string s_webResponseFormatPropertyName = "WebResponseFormatProperty";
        internal static readonly string s_automatedFormatSelectionContentTypePropertyName = "AutomatedFormatSelectionContentTypePropertyName";

        private Encoding _bindingWriteEncoding = null;
        private readonly OperationContext _operationContext;

        internal OutgoingWebResponseContext(OperationContext operationContext)
        {
            Fx.Assert(operationContext != null, "operationContext is null");

            _operationContext = operationContext;
        }

        public long ContentLength
        {
            get { return long.Parse(MessageProperty.Headers[HttpResponseHeader.ContentLength], CultureInfo.InvariantCulture); }
            set { MessageProperty.Headers[HttpResponseHeader.ContentLength] = value.ToString(CultureInfo.InvariantCulture); }
        }

        public string ContentType
        {
            get { return MessageProperty.Headers[HttpResponseHeader.ContentType]; }
            set { MessageProperty.Headers[HttpResponseHeader.ContentType] = value; }
        }

        public string ETag
        {
            get { return MessageProperty.Headers[HttpResponseHeader.ETag]; }
            set { MessageProperty.Headers[HttpResponseHeader.ETag] = value; }
        }

        public WebHeaderCollection Headers => MessageProperty.Headers;

        public DateTime LastModified
        {
            get
            {
                string dateTime = MessageProperty.Headers[HttpRequestHeader.LastModified];
                if (!string.IsNullOrEmpty(dateTime))
                {
                    if (DateTime.TryParse(dateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDateTime))
                    {
                        return parsedDateTime;
                    }
                }

                return DateTime.MinValue;
            }
            set
            {
                MessageProperty.Headers[HttpResponseHeader.LastModified] =
                    (value.Kind == DateTimeKind.Utc ?
                    value.ToString("R", CultureInfo.InvariantCulture) :
                    value.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture));
            }
        }

        public string Location
        {
            get { return MessageProperty.Headers[HttpResponseHeader.Location]; }
            set { MessageProperty.Headers[HttpResponseHeader.Location] = value; }
        }

        public HttpStatusCode StatusCode
        {
            get { return MessageProperty.StatusCode; }
            set { MessageProperty.StatusCode = value; }
        }

        public string StatusDescription
        {
            get { return MessageProperty.StatusDescription; }
            set { MessageProperty.StatusDescription = value; }
        }

        public bool SuppressEntityBody
        {
            get { return MessageProperty.SuppressEntityBody; }
            set { MessageProperty.SuppressEntityBody = value; }
        }

        public WebMessageFormat? Format
        {
            get
            {
                if (!_operationContext.OutgoingMessageProperties.ContainsKey(s_webResponseFormatPropertyName))
                {
                    return null;
                }

                return _operationContext.OutgoingMessageProperties[s_webResponseFormatPropertyName] as WebMessageFormat?;
            }
            set
            {
                if (value.HasValue)
                {
                    if (!WebMessageFormatHelper.IsDefined(value.Value))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                    }
                    else
                    {
                        _operationContext.OutgoingMessageProperties[s_webResponseFormatPropertyName] = value.Value;
                    }
                }
                else
                {
                    _operationContext.OutgoingMessageProperties[s_webResponseFormatPropertyName] = null;
                }

                AutomatedFormatSelectionContentType = null;
            }
        }

        // This is an internal property because we need to carry the content-type that was selected by the FormatSelectingMessageInspector
        // forward so that the formatter has access to it. However, we dond't want to use the ContentType property on this, because then
        // developers would have to clear the ContentType property manually when overriding the format set by the 
        // FormatSelectingMessageInspector
        internal string AutomatedFormatSelectionContentType
        {
            get
            {
                if (!_operationContext.OutgoingMessageProperties.ContainsKey(s_automatedFormatSelectionContentTypePropertyName))
                {
                    return null;
                }
                return _operationContext.OutgoingMessageProperties[s_automatedFormatSelectionContentTypePropertyName] as string;
            }
            set
            {
                _operationContext.OutgoingMessageProperties[s_automatedFormatSelectionContentTypePropertyName] = value;
            }
        }

        public Encoding BindingWriteEncoding
        {
            get
            {
                if (_bindingWriteEncoding == null)
                {
                    string endpointId = _operationContext.EndpointDispatcher.Id;
                    Fx.Assert(endpointId != null, "There should always be an valid EndpointDispatcher.Id");
                    foreach (ServiceEndpoint endpoint in _operationContext.Host.Description.Endpoints)
                    {
                        if (endpoint.Id == endpointId)
                        {
                            if (endpoint.Binding.CreateBindingElements().Find<WebMessageEncodingBindingElement>() is WebMessageEncodingBindingElement encodingElement)
                            {
                                _bindingWriteEncoding = encodingElement.WriteEncoding;
                            }
                        }
                    }
                }

                return _bindingWriteEncoding;
            }
        }

        internal HttpResponseMessageProperty MessageProperty
        {
            get
            {
                if (!_operationContext.OutgoingMessageProperties.ContainsKey(HttpResponseMessageProperty.Name))
                {
                    _operationContext.OutgoingMessageProperties.Add(HttpResponseMessageProperty.Name, new HttpResponseMessageProperty());
                }

                return _operationContext.OutgoingMessageProperties[HttpResponseMessageProperty.Name] as HttpResponseMessageProperty;
            }
        }

        public void SetETag(string entityTag)
        {
            ETag = GenerateValidEtagFromString(entityTag);
        }

        public void SetETag(int entityTag)
        {
            ETag = GenerateValidEtag(entityTag);
        }

        public void SetETag(long entityTag)
        {
            ETag = GenerateValidEtag(entityTag);
        }

        public void SetETag(Guid entityTag)
        {
            ETag = GenerateValidEtag(entityTag);
        }

        public void SetStatusAsCreated(Uri locationUri)
        {
            if (locationUri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(locationUri));
            }

            StatusCode = HttpStatusCode.Created;
            Location = locationUri.ToString();
        }

        public void SetStatusAsNotFound()
        {
            StatusCode = HttpStatusCode.NotFound;
        }

        public void SetStatusAsNotFound(string description)
        {
            StatusCode = HttpStatusCode.NotFound;
            StatusDescription = description;
        }

        internal static string GenerateValidEtagFromString(string entityTag)
        {
            // This method will generate a valid entityTag from a string by doing the following:
            //   1) Adding surrounding double quotes if the string doesn't already start and end with them
            //   2) Escaping any internal double quotes that aren't already escaped (preceded with a backslash)
            //   3) If a string starts with a double quote but doesn't end with one, or vice-versa, then the 
            //      double quote is considered internal and is escaped.

            if (string.IsNullOrEmpty(entityTag))
            {
                return null;
            }

            if (entityTag.StartsWith("W/\"", StringComparison.OrdinalIgnoreCase) &&
                entityTag.EndsWith("\"", StringComparison.OrdinalIgnoreCase))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.WeakEntityTagsNotSupported, entityTag)));
            }

            List<int> escapeCharacterInsertIndices = null;
            int lastEtagIndex = entityTag.Length - 1;
            bool startsWithQuote = entityTag[0] == '\"';
            bool endsWithQuote = entityTag[lastEtagIndex] == '\"';

            // special case where the entityTag is a single character, a double quote, '"'
            if (lastEtagIndex == 0 && startsWithQuote)
            {
                endsWithQuote = false;
            }

            bool needsSurroundingQuotes = !startsWithQuote || !endsWithQuote;

            if (startsWithQuote && !endsWithQuote)
            {
                if (escapeCharacterInsertIndices == null)
                {
                    escapeCharacterInsertIndices = new List<int>();
                }

                escapeCharacterInsertIndices.Add(0);
            }

            for (int x = 1; x < lastEtagIndex; x++)
            {
                if (entityTag[x] == '\"' && entityTag[x - 1] != '\\')
                {
                    if (escapeCharacterInsertIndices == null)
                    {
                        escapeCharacterInsertIndices = new List<int>();
                    }

                    escapeCharacterInsertIndices.Add(x + escapeCharacterInsertIndices.Count);
                }
            }

            // Possible that the ending internal quote is already escaped so must check the character before it
            if (!startsWithQuote && endsWithQuote && entityTag[lastEtagIndex - 1] != '\\')
            {
                if (escapeCharacterInsertIndices == null)
                {
                    escapeCharacterInsertIndices = new List<int>();
                }

                escapeCharacterInsertIndices.Add(lastEtagIndex + escapeCharacterInsertIndices.Count);
            }

            if (needsSurroundingQuotes || escapeCharacterInsertIndices != null)
            {
                int escapeCharacterInsertIndicesCount = (escapeCharacterInsertIndices == null) ? 0 : escapeCharacterInsertIndices.Count;
                StringBuilder editedEtag = new StringBuilder(entityTag, entityTag.Length + escapeCharacterInsertIndicesCount + 2);
                for (int x = 0; x < escapeCharacterInsertIndicesCount; x++)
                {
                    editedEtag.Insert(escapeCharacterInsertIndices[x], '\\');
                }

                if (needsSurroundingQuotes)
                {
                    editedEtag.Insert(entityTag.Length + escapeCharacterInsertIndicesCount, '\"');
                    editedEtag.Insert(0, '\"');
                }

                entityTag = editedEtag.ToString();
            }

            return entityTag;
        }

        internal static string GenerateValidEtag(object entityTag) => string.Format(CultureInfo.InvariantCulture, "\"{0}\"", entityTag.ToString());
    }
}
