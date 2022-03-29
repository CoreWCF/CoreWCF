### What is Core WCF? 

Core WCF is a port of Windows Communication Foundation (WCF) to .NET Core. The goal of this project is to enable existing WCF projects to move to .NET Core.

### Package Status

The latest released packages can be found at Nuget.org:

| Package                                                                                      | NuGet Stable                                                                                     | Downloads                                                                                     |
|:---------------------------------------------------------------------------------------------|:------------------------------------------------------------------------------------------------:|:---------------------------------------------------------------------------------------------:|
| [CoreWCF.Primitives](https://www.nuget.org/packages/CoreWCF.Primitives/)                     | ![CoreWCF.Primitives](https://img.shields.io/nuget/v/CoreWCF.Primitives.svg)                     | ![CoreWCF.Primitives](https://img.shields.io/nuget/dt/CoreWCF.Primitives)                     |
| [CoreWCF.Http](https://www.nuget.org/packages/CoreWCF.Http/)                                 | ![CoreWCF.Http](https://img.shields.io/nuget/v/CoreWCF.Http.svg)                                 | ![CoreWCF.Http](https://img.shields.io/nuget/dt/CoreWCF.Http)                                 |
| [CoreWCF.NetTcp](https://www.nuget.org/packages/CoreWCF.NetTcp/)                             | ![CoreWCF.NetTcp](https://img.shields.io/nuget/v/CoreWCF.NetTcp.svg)                             | ![CoreWCF.NetTcp](https://img.shields.io/nuget/dt/CoreWCF.NetTcp)                             |
| [CoreWCF.ConfigurationManager](https://www.nuget.org/packages/CoreWCF.ConfigurationManager/) | ![CoreWCF.ConfigurationManager](https://img.shields.io/nuget/v/CoreWCF.ConfigurationManager.svg) | ![CoreWCF.ConfigurationManager](https://img.shields.io/nuget/dt/CoreWCF.ConfigurationManager) |

### Code Quality

[![SonarCloud](https://sonarcloud.io/images/project_badges/sonarcloud-white.svg)](https://sonarcloud.io/summary/new_code?id=CoreWCF_CoreWCF)

### Announcements

To keep up to date on what's going on with CoreWCF, you can subscribe to the [announcements](https://github.com/CoreWCF/announcements) repo to be notified about major changes and other noteworthy announcements.

### How do I get started?

* Install the Nuget packages listed above, either via the Package Manager Console or the UI.
* The following sample projects show how to create a CoreWCF server or client
  * [NetCoreServer](src/Samples/NetCoreServer) - Server Project for .NET Core
  * [DesktopServer](src/Samples/DesktopServer) - .NET Framework 4.7.2 host using ServiceHost
  * [NetCoreClient](src/Samples/NetCoreClient) - Wraps StandardClient with a .NET Core Console Application
  * [DesktopClient](src/Samples/DesktopClient) - Wraps StandardClient with a .NET Framework Console Application
  * [StandardClient](src/Samples/StandardClient) - .NET Standard based client for the server projects 

The [Blog](https://corewcf.github.io/) has details on the design philosophy and which features are included in each new release.

### Daily Builds

There are pre-release packages available for daily builds of main from a NuGet feed hosted in Azure DevOps. You can download the packages by adding the following package source to your list of feeds.

    https://pkgs.dev.azure.com/dotnet/CoreWCF/_packaging/CoreWCF/nuget/v3/index.json

If you are using a nuget.config file with only the default nuget.org package source, after adding the CoreWCF feed it would look like this:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="CoreWCF" value="https://pkgs.dev.azure.com/dotnet/CoreWCF/_packaging/CoreWCF/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

### How do I contribute?

Please see the [CONTRIBUTING.md](CONTRIBUTING.md) file for details.

### License, etc.

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

Core WCF is Copyright &copy; 2019 .NET Foundation and other contributors under the [MIT license](LICENSE.txt).

### .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).
