// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.Channels;

internal static class SaslOauthbearerMethodHelper
{
    public static bool IsDefined(SaslOauthbearerMethod value)
    {
        return value == SaslOauthbearerMethod.Default
               || value == SaslOauthbearerMethod.Oidc;
    }
}
