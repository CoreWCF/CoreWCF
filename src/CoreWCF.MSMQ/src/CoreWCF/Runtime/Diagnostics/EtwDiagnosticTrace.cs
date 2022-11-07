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
        {
        }
    }
}
