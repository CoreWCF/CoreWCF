# Microsoft Support

This document details Microsoft Support policy for CoreWCF.

We recognize how important support is to enterprise customers, and so we are pleased to announce that Microsoft Product Support will be available for CoreWCF customers.

Support for CoreWCF 1.x will depend on the support status for the underlying .NET platforms it runs on.

| **Runtime Version** | **Support dependency duration** |
| --- | --- |
| .NET Framework 4.x | The specific version of [.NET Framework](https://dotnet.microsoft.com/platform/support/policy/dotnet-framework), and [ASP.NET Core 2.1.](https://dotnet.microsoft.com/platform/support/policy/aspnet) |
| .NET Core 3.1 | .NET Core 3.1 LTS - December 13, 2022 |
| .NET 6 | .NET 6 LTS - November 12, 2024 |

CoreWCF will use Major.Minor versioning strategy:

- 1.0 will be the first major release of CoreWCF
- Minor releases will be numbered 1.x, and will have the same underlying platform requirements as 1.0.
- Minor version releases (1.x) will be API compatible with the 1.0 release.
- Support will be primarily for the latest major.minor release of each supported major version.
  - When new major or minor versions are released, then the previous release will be supported for 6 months from the date of the new release, provided the underlying runtime dependency being used also supported.
- Subsequent major versions, such as 2.0, may reduce the map of runtimes that are supported. In that case 1.x will continue to be supported beyond the 6 months on the runtimes that are not supported by 2.0, and support duration will be limited only by the underlying platform dependencies.
  - This will most likely apply to .NET Framework, and means that 1.x will be supported as long as both ASP.NET Core 2.1 and .NET Framework 4.x are under support.
