// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;

namespace WebHttp
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    internal class WebApi : IWebApi
    {
        public string PathEcho(string param) => param;

        public string QueryEcho(string param) => param;

        public ExampleContract BodyEcho(ExampleContract param) => param;
    }
}
