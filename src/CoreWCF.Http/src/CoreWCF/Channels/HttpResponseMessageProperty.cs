// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;

namespace CoreWCF.Channels
{
    public sealed class HttpResponseMessageProperty : IMessageProperty
    {
        public const HttpStatusCode DefaultStatusCode = HttpStatusCode.OK;
        public const string DefaultStatusDescription = null; // null means use description from status code

        private WebHeaderCollection _originalHeaders;
        private HttpStatusCode _statusCode;
        private WebHeaderCollection _headers;

        /// <summary>
        /// Initializes a new instance of the HttpResponseMessageProperty class with no headers and the default status code.
        /// </summary>
        public HttpResponseMessageProperty()
            : this((WebHeaderCollection)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseMessageProperty"/> class.
        /// </summary>
        /// <param name="originalHeaders">The original headers. This collection will be copied so that the original is not accidentally modified.</param>
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

        /// <summary>
        /// Gets a mutable list of headers.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        /// <value>The HTTP status code.</value>
        /// <exception cref="ArgumentOutOfRangeException">The status code is less than 100 or greater than 599</exception>
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

        /// <summary>
        /// Gets or sets the HTTP status description.
        /// </summary>
        /// <value>The HTTP status description.</value>
        /// <remarks>If this returns null, use the default description for the current HttpStatusCode.</remarks>
        public string StatusDescription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to suppress entity body.
        /// </summary>
        /// <remarks>If this is true, then the content-type will not be set.</remarks>
        public bool SuppressEntityBody { get; set; }

        //Note currently in use. Remove?
        public bool SuppressPreamble { get; set; }

        IMessageProperty IMessageProperty.CreateCopy()
        {
            return this;
        }
    }
}
