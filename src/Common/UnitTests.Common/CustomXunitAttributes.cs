// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Xunit;

public class NetCoreOnlyFactAttribute : FactAttribute
{
    public NetCoreOnlyFactAttribute()
    {
        if (Environment.Version.Major < 6)
        {
            Skip = nameof(NetCoreOnlyFactAttribute);
        }
    }
}

public class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = nameof(WindowsOnlyFactAttribute);
        }
    }
}

public class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = nameof(LinuxOnlyFactAttribute);
        }
    }
}

public class WindowsOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsOnlyTheoryAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = nameof(WindowsOnlyTheoryAttribute);
        }
    }
}

public class WindowsNetCoreOnlyFactAttribute : FactAttribute
{
    public WindowsNetCoreOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || Environment.Version.Major < 6)
        {
            Skip = nameof(WindowsNetCoreOnlyFactAttribute);
        }
    }
}

public class WindowsNetCoreOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsNetCoreOnlyTheoryAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || Environment.Version.Major < 6)
        {
            Skip = nameof(WindowsNetCoreOnlyTheoryAttribute);
        }
    }
}

public class LinuxWhenCIOnlyFactAttribute : FactAttribute
{
    public LinuxWhenCIOnlyFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CI") == "true")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Skip = nameof(LinuxWhenCIOnlyFactAttribute);
            }
        }
    }
}

public class LinuxWhenCIOnlyTheoryAttribute : TheoryAttribute
{
    public LinuxWhenCIOnlyTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("CI") == "true")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Skip = nameof(LinuxWhenCIOnlyFactAttribute);
            }
        }
    }
}

public class SkipOnGeneratedOperationInvokerFactAttribute : FactAttribute
{
    public SkipOnGeneratedOperationInvokerFactAttribute()
    {
        if (AppContext.TryGetSwitch("CoreWCF.Dispatcher.UseGeneratedOperationInvokers", out bool value) && value)
        {
            Skip = "Class-based service contracts are not supported by generated OperationInvoker";
        }
    }
}

public class SkipOnGeneratedOperationInvokerTheoryAttribute : TheoryAttribute
{
    public SkipOnGeneratedOperationInvokerTheoryAttribute()
    {
        if (AppContext.TryGetSwitch("CoreWCF.Dispatcher.UseGeneratedOperationInvokers", out bool value) && value)
        {
            Skip = "Class-based service contracts are not supported by generated OperationInvoker";
        }
    }
}
