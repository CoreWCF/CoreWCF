using System;

namespace CoreWCF
{
    public enum HostNameComparisonMode
    {
        StrongWildcard = 0, // +
        Exact = 1,
        WeakWildcard = 2,   // *
    }
}