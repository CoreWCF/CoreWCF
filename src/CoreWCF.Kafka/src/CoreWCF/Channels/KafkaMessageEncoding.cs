// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

public enum KafkaMessageEncoding
{
    /// <summary>
    /// Indicates using Binary message encoder.
    /// </summary>
    Binary,

    /// <summary>
    /// Indicates using Text message encoder.
    /// </summary>
    Text,
}

internal static class KafkaMessageEncodingHelper
{
    public static bool IsDefined(KafkaMessageEncoding value)
    {
        return value == KafkaMessageEncoding.Binary || value == KafkaMessageEncoding.Text;
    }
}
