// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace CoreWCF.Telemetry;

internal sealed class TelemetryContextMessageProperty
{
    public const string Name = "CoreWCF.Telemetry.TelemetryContextMessageProperty";

    public TelemetryContextMessageProperty(IDictionary<string, ActionMetadata> actionMappings)
    {
        ActionMappings = actionMappings;
    }

    public IDictionary<string, ActionMetadata> ActionMappings { get; set; }
}
