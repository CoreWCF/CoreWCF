// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using OpenTelemetry.Trace;

namespace CoreWCF.Telemetry;


/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Enables the outgoing requests automatic data collection for WCF.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilderExtensions"/> being configured.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilderExtensions"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddWcfInstrumentation(this TracerProviderBuilder builder) =>
        AddWcfInstrumentation(builder, configure: null);

    /// <summary>
    /// Enables the outgoing requests automatic data collection for WCF.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilderExtensions"/> being configured.</param>
    /// <param name="configure">Wcf configuration options.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilderExtensions"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddWcfInstrumentation(this TracerProviderBuilder builder,
        Action<WcfInstrumentationOptions>? configure)
    {
        if (WcfInstrumentationActivitySource.Options != null)
        {
            throw new NotSupportedException(
                "WCF instrumentation has already been registered and doesn't support multiple registrations.");
        }

        var options = new WcfInstrumentationOptions();
        configure?.Invoke(options);

        WcfInstrumentationActivitySource.Options = options;

        return builder.AddSource(WcfInstrumentationActivitySource.ActivitySourceName);
    }
}
