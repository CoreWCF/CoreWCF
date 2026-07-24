// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Web;

namespace CoreWCF.OpenApi
{
    /// <summary>
    /// Captures information about a WCF contract needed by OpenAPI.
    /// </summary>
    public class OpenApiContractInfo
    {
        /// <summary>
        /// The actual contract.
        /// </summary>
        public Type Contract { get; set; }

        /// <summary>
        /// The URI the contract is registered on.
        /// </summary>
        public Uri Address { get; set; }

        /// <summary>
        /// Default format of the response.
        /// </summary>
        public WebMessageFormat ResponseFormat { get; set; }
    }
}
