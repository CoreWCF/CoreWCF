using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CoreWCF.Channels
{
    public sealed class HttpResponseMessageProperty : IMessageProperty
    {
        public const HttpStatusCode DefaultStatusCode = HttpStatusCode.OK;
        public const string DefaultStatusDescription = null; // null means use description from status code

        private WebHeaderCollection _originalHeaders;
        private HttpStatusCode _statusCode;
        private WebHeaderCollection _headers;

        public HttpResponseMessageProperty()
            : this((WebHeaderCollection)null)
        {
        }

        internal HttpResponseMessageProperty(WebHeaderCollection originalHeaders)
        {
            _originalHeaders = originalHeaders;
            _statusCode = DefaultStatusCode;
            StatusDescription = DefaultStatusDescription;
        }

        public static string Name
        {
            get { return "httpResponse"; }
        }

        public WebHeaderCollection Headers
        {
            get
            {
                if (_headers == null)
                {
                    _headers = new WebHeaderCollection();
                    if (_originalHeaders != null)
                    {
                        _headers.Add(_originalHeaders);
                        _originalHeaders = null;
                    }
                }

                return _headers;
            }
        }

        public HttpStatusCode StatusCode
        {
            get
            {
                return _statusCode;
            }
            set
            {
                int valueInt = (int)value;
                if (valueInt < 100 || valueInt > 599)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.Format(SR.ValueMustBeInRange, 100, 599)));
                }

                _statusCode = value;
                HasStatusCodeBeenSet = true;
            }
        }

        internal bool HasStatusCodeBeenSet { get; private set; }

        public string StatusDescription { get; set; }

        public bool SuppressEntityBody { get; set; }

        public bool SuppressPreamble { get; set; }

        IMessageProperty IMessageProperty.CreateCopy()
        {
            return this;
        }
    }
}