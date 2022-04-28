// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.OpenApi.Attributes
{
    /// <summary>
    /// Attribute to denote information about a parameter to an operation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class OpenApiParameterAttribute : Attribute
    {
        /// <summary>
        /// A description of the parameter.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Content types the parameter can be provided as.
        /// </summary>
        public string[] ContentTypes { get; set; }
    }
}
