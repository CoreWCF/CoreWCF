// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Runtime.Diagnostics
{
    internal sealed class EtwDiagnosticTrace
    {
        public static readonly Guid ImmutableDefaultEtwProviderId = new Guid("{79f88dc7-9062-4cff-af90-09b2455644b6}");
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
