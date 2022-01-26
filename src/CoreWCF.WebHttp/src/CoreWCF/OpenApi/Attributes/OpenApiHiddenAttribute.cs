// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.OpenApi.Attributes
{
    /// <summary>
    /// Attribute to denote that something is hidden.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class OpenApiHiddenAttribute : Attribute { }
}
