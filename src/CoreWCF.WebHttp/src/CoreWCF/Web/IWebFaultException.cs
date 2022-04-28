// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;

namespace CoreWCF.Web
{
    internal interface IWebFaultException
    {
        HttpStatusCode StatusCode { get; }

        Type DetailType { get; }

        object DetailObject { get; }

        Type[] KnownTypes { get; }
    }
}
