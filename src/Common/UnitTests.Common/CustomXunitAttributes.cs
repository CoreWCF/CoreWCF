// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Xunit;

internal static class VersionConstants
{
    internal const int MinimumSupportedNetCoreMajorVersion = 6;
    internal const int MaximumSupportedNetMajorVersion = 9;
    internal const int UnsupportedNetMajorVersion = 10;
}

internal static class AttributeExtensions
{
    public static FactAttribute SkipOnNetVersion(this FactAttribute attribute, int majorVersion)
    {
        if (attribute.Skip == null && Environment.Version.Major == majorVersion)
        {
            attribute.Skip = $"Test skipped on .NET {majorVersion}.";
        }

        return attribute;
    }

    public static FactAttribute RequireWindows(this FactAttribute attribute)
    {
        if (attribute.Skip == null && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            attribute.Skip = "Test requires Windows.";
        }

        return attribute;
    }

    public static FactAttribute RequireLinux(this FactAttribute attribute)
    {
        if (attribute.Skip == null && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            attribute.Skip = "Test requires Linux.";
        }

        return attribute;
    }

    public static FactAttribute RequireMinNetVersion(this FactAttribute attribute, int minMajor)
    {
        if (attribute.Skip == null && Environment.Version.Major < minMajor)
        {
            attribute.Skip = $"Test requires .NET version >= {minMajor}.";
        }

        return attribute;
    }

    public static FactAttribute RequireNetVersionRange(this FactAttribute attribute, int minMajor, int maxMajor)
    {
        if (attribute.Skip == null && (Environment.Version.Major < minMajor || Environment.Version.Major > maxMajor))
        {
            attribute.Skip = $"Test requires .NET version between {minMajor} and {maxMajor} (inclusive).";
        }

        return attribute;
    }

    public static FactAttribute RequireLinuxOnCI(this FactAttribute attribute)
    {
        if (attribute.Skip == null && Environment.GetEnvironmentVariable("CI") == "true")
        {
            attribute.RequireLinux();
        }

        return attribute;
    }
}

public class FactSkipUnsupportedNetVersionAttribute : FactAttribute
{
    public FactSkipUnsupportedNetVersionAttribute() => this.SkipOnNetVersion(VersionConstants.UnsupportedNetMajorVersion);
}

public class TheorySkipOnUnsupportedNetVersionAttribute : TheoryAttribute
{
    public TheorySkipOnUnsupportedNetVersionAttribute() => this.SkipOnNetVersion(VersionConstants.UnsupportedNetMajorVersion);
}

public class SupportedNetCoreFactAttribute : FactAttribute
{
    public SupportedNetCoreFactAttribute() => this.RequireNetVersionRange(VersionConstants.MinimumSupportedNetCoreMajorVersion, VersionConstants.MaximumSupportedNetMajorVersion);
}

public class SupportedNetCoreTheoryAttribute : TheoryAttribute
{
    public SupportedNetCoreTheoryAttribute() => this.RequireNetVersionRange(VersionConstants.MinimumSupportedNetCoreMajorVersion, VersionConstants.MaximumSupportedNetMajorVersion);
}

public class NetCoreOnlyFactAttribute : FactAttribute
{
    public NetCoreOnlyFactAttribute() => this.RequireMinNetVersion(VersionConstants.MinimumSupportedNetCoreMajorVersion);
}

public class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute() => this.RequireWindows();
}

public class LinuxOnlyFactAttribute : FactAttribute
{
    public LinuxOnlyFactAttribute() => this.RequireLinux();
}

public class WindowsOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsOnlyTheoryAttribute() => this.RequireWindows();
}

public class WindowsSkipUnsupportedNetVersionFactAttribute : FactAttribute
{
    public WindowsSkipUnsupportedNetVersionFactAttribute() => this.RequireWindows().SkipOnNetVersion(VersionConstants.UnsupportedNetMajorVersion);
}

public class WindowsNetCoreOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsNetCoreOnlyTheoryAttribute() => this.RequireWindows().RequireMinNetVersion(VersionConstants.MinimumSupportedNetCoreMajorVersion);
}

public class LinuxWhenCIOnlyFactAttribute : FactAttribute
{
    public LinuxWhenCIOnlyFactAttribute() => this.RequireLinuxOnCI();
}

public class LinuxWhenCIOnlyTheoryAttribute : TheoryAttribute
{
    public LinuxWhenCIOnlyTheoryAttribute() => this.RequireLinuxOnCI();
}
