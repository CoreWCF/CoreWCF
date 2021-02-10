// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Headers;

namespace CoreWCF.Channels
{
    // This type allows the use of a generally typed HttpHeaders property on Http{Request|Response}MessageProperty
    internal class ServiceModelHttpHeaders : HttpHeaders
    {
    }
}