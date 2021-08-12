// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    internal interface IPrefixGenerator
    {
        string GetPrefix(string namespaceUri, int depth, bool isForAttribute);
    }
}
