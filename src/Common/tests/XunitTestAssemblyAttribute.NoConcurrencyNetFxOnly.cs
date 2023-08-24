// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
#else
[assembly: Xunit.CollectionBehavior(MaxParallelThreads = -1)]
#endif