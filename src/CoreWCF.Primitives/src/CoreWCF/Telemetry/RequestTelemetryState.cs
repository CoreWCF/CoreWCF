// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace CoreWCF.Telemetry;

internal sealed class RequestTelemetryState
{
    public IDisposable? SuppressionScope { get; set; }

    public Activity? Activity { get; set; }
}
