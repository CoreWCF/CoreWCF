// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.OpenApi
{
    /// <summary>
    /// Top level information about the API.
    /// </summary>
    public sealed class OpenApiOptions
    {
        /// <summary>
        /// Version of the API.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Description of the API.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Title of the API.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Terms of service of the API.
        /// </summary>
        public Uri TermsOfService { get; set; }

        /// <summary>
        /// Name of the contact.
        /// </summary>
        public string ContactName { get; set; }

        /// <summary>
        /// Url with contact information.
        /// </summary>
        public string ContactEmail { get; set; }

        /// <summary>
        /// Email used for contact.
        /// </summary>
        public Uri ContactUrl { get; set; }

        /// <summary>
        /// Name of the license used for the API.
        /// </summary>
        public string LicenseName { get; set; }

        /// <summary>
        /// URL of the license for the API.
        /// </summary>
        public Uri LiceneUrl { get; set; }

        /// <summary>
        /// Description of an external document for the API.
        /// </summary>
        public string ExternalDocumentDescription { get; set; }

        /// <summary>
        /// URL of an external document for an API.
        /// </summary>
        public Uri ExternalDocumentUrl { get; set; }

        /// <summary>
        /// Any tags to hide.
        /// </summary>
        public IEnumerable<string> TagsToHide { get; set; } = new List<string>();
    }
}
