// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Xunit;

public class NetCoreOnlyFactAttribute : FactAttribute
{
    public NetCoreOnlyFactAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
    {
        if (Environment.Version.Major < 6)
        {
            Skip = nameof(NetCoreOnlyFactAttribute);
        }
    }
}

public class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = nameof(WindowsOnlyFactAttribute);
        }
    }
}

public class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = nameof(LinuxOnlyFactAttribute);
        }
    }
}

public class WindowsOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsOnlyTheoryAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = nameof(WindowsOnlyTheoryAttribute);
        }
    }
}

public class WindowsNetCoreOnlyFactAttribute : FactAttribute
{
    public WindowsNetCoreOnlyFactAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
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
    public WindowsNetCoreOnlyTheoryAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
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
    public LinuxWhenCIOnlyFactAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
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
    public LinuxWhenCIOnlyTheoryAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
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
    public SkipOnGeneratedOperationInvokerFactAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
    {
        if (AppContext.TryGetSwitch("CoreWCF.Dispatcher.UseGeneratedOperationInvokers", out bool value) && value)
        {
            Skip = "Class-based service contracts are not supported by generated OperationInvoker";
        }
    }
}

public class SkipOnGeneratedOperationInvokerTheoryAttribute : TheoryAttribute
{
    public SkipOnGeneratedOperationInvokerTheoryAttribute(
        [CallerFilePath] string sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
          : base(sourceFilePath, sourceLineNumber)
    {
        if (AppContext.TryGetSwitch("CoreWCF.Dispatcher.UseGeneratedOperationInvokers", out bool value) && value)
        {
            Skip = "Class-based service contracts are not supported by generated OperationInvoker";
        }
    }
}
