using System;

namespace Microsoft.ServiceModel
{
    public enum HostNameComparisonMode
    {
        StrongWildcard = 0, // +
        Exact = 1,
        WeakWildcard = 2,   // *
    }
}