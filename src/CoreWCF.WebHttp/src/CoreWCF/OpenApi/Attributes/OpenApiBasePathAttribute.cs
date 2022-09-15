// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.OpenApi.Attributes
{
    /// <summary>
    /// Attribute to denote base path for the API.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public sealed class OpenApiBasePathAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="basePath">Base path for the API.</param>
        public OpenApiBasePathAttribute(string basePath)
        {
            BasePath = basePath;
        }

        /// <summary>
        /// Base path for the API.
        /// </summary>
        public string BasePath { get; }
    }
}
