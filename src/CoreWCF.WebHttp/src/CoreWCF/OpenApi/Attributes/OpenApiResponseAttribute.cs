// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;

namespace CoreWCF.OpenApi.Attributes
{
    /// <summary>
    /// Attribute to denote information about a response from an operation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OpenApiResponseAttribute : Attribute
    {
        /// <summary>
        /// A status code that can be returned by an operation.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        /// <summary>
        /// A type that can be returned by an operation.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// A description of why this type and status code would be returned by an operation.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Content types this response can be returned as.
        /// </summary>
        public string[] ContentTypes { get; set; }
    }
}
