// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Runtime.Diagnostics
{
    internal sealed class EtwDiagnosticTrace
    {
        public static readonly Guid ImmutableDefaultEtwProviderId = new Guid("{c651f5f6-1c0d-492e-8ae1-b4efd7c9d503}");
        private static Guid s_defaultEtwProviderId = ImmutableDefaultEtwProviderId;

        public static Guid DefaultEtwProviderId
        {
            get
            {
                return s_defaultEtwProviderId;
            }
            set
            {
                s_defaultEtwProviderId = value;
            }
        }

        public EtwDiagnosticTrace(string traceSourceName, Guid etwProviderId)
        //: base(traceSourceName)
        {
            //            try
            //            {
            //                this.TraceSourceName = traceSourceName;
            //                this.EventSourceName = string.Concat(this.TraceSourceName, " ", EventSourceVersion);
            //                CreateTraceSource();
            //            }
            //            catch (Exception exception)
            //            {
            //                if (Fx.IsFatal(exception))
            //                {
            //                    throw;
            //                }

            //#pragma warning disable 618
            //                EventLogger logger = new EventLogger(this.EventSourceName, null);
            //                logger.LogEvent(TraceEventType.Error, TracingEventLogCategory, (uint)System.Runtime.Diagnostics.EventLogEventId.FailedToSetupTracing, false,
            //                    exception.ToString());
            //#pragma warning restore 618
            //            }

            //            try
            //            {
            //                CreateEtwProvider(etwProviderId);
            //            }
            //            catch (Exception exception)
            //            {
            //                if (Fx.IsFatal(exception))
            //                {
            //                    throw;
            //                }

            //                this.etwProvider = null;
            //#pragma warning disable 618
            //                EventLogger logger = new EventLogger(this.EventSourceName, null);
            //                logger.LogEvent(TraceEventType.Error, TracingEventLogCategory, (uint)System.Runtime.Diagnostics.EventLogEventId.FailedToSetupTracing, false,
            //                    exception.ToString());
            //#pragma warning restore 618

            //            }

            //            if (this.TracingEnabled || this.EtwTracingEnabled)
            //            {
            //#pragma warning disable 618
            //                this.AddDomainEventHandlersForCleanup();
            //#pragma warning restore 618
            //            }
        }
    }
}