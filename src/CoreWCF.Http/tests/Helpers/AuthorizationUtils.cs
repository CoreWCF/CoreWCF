// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Http.Tests.Helpers;

public static class AuthorizationUtils
{
    public static class Policies
    {
        public const string Read = nameof(Read);
        public const string Write = nameof(Write);
    }

    public static class DefinedScopeValues
    {
        public const string Read = nameof(Read);
        public const string Write = nameof(Write);
    }
}
