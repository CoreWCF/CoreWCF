// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.Tracing;

namespace CoreWCF.Telemetry;


[EventSource(Name = "OpenTelemetry-Instrumentation-CoreWCF")]
internal sealed class WcfInstrumentationEventSource : EventSource
{
    public static readonly WcfInstrumentationEventSource Log = new();

    [NonEvent]
    public void RequestFilterException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
        {
            this.RequestFilterException(ex.ToInvariantString());
        }
    }

    [Event(EventIds.RequestIsFilteredOut, Message = "Request is filtered out.", Level = EventLevel.Verbose)]
    public void RequestIsFilteredOut()
    {
        this.WriteEvent(EventIds.RequestIsFilteredOut);
    }

    [Event(EventIds.RequestFilterException, Message = "InstrumentationFilter threw exception. Request will not be collected. Exception {0}.", Level = EventLevel.Error)]
    public void RequestFilterException(string exception)
    {
        this.WriteEvent(EventIds.RequestFilterException, exception);
    }

    [NonEvent]
    public void EnrichmentException(Exception exception)
    {
        if (this.IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
        {
            this.EnrichmentException(exception.ToInvariantString());
        }
    }

    [Event(EventIds.EnrichmentException, Message = "Enrichment threw exception. Exception {0}.", Level = EventLevel.Error)]
    public void EnrichmentException(string exception)
    {
        this.WriteEvent(EventIds.EnrichmentException, exception);
    }


    private class EventIds
    {
        public const int RequestIsFilteredOut = 1;
        public const int RequestFilterException = 2;
        public const int EnrichmentException = 3;
        public const int HttpServiceModelReflectionFailedToBind = 4;
        public const int AspNetReflectionFailedToBind = 5;
    }
}
