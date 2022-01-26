// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.OpenApi.Attributes
{
    /// <summary>
    /// Attribute to denote a tag for a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
    public sealed class OpenApiTagAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tag">The tag name.</param>
        public OpenApiTagAttribute(string tag)
        {
            Tag = tag;
        }

        /// <summary>
        /// The tag name.
        /// </summary>
        public string Tag { get; }
    }
}
