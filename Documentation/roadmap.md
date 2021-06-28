# Release Road map

This document gives a high level overview of the release road map for CoreWCF.

## Migration release

The goal of the first release is to make migration from .NET Framework as smooth as possible. Most extensibility implementations such as message inspectors should only need a namespace change. If you add the WCF client package System.ServiceModel.Primitives to your project, you can continue to use the contract attributes from the System.ServiceModel namespace.  

This release depends on ASP.NET Core 2.1 which is supported on .NET Framework. This allows you to migrate your WCF service to CoreWCF before your have completed making the necessary service implementation changes needed to from on .NET Core/.NET 5+. This enables still running on .NET Framework through the porting process. This provides a more granular migration effort and helps avoid the need for a monolithic all or nothing porting effort.

ASP.NET Core 2.1 was released as an OOB (out of band) set of packages and runs on .NET Core 3.1. We run all our tests on .NET Core 3.1 to validate this scenario. The [end of support date](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) for .NET Core 3.1 is December 3, 2022.

The first release will have a version number of 0.1. The major version being 0 is a reflection of some core features not currently available which would enable justification of a 1.0 release version. An example of a feature required for 1.0 is support for WSDL generation. Once enough features have been ported, the version number will change to 1.0. The 1.0 version will then receive no more new features and will only see bug fixes.  

The migration release will be supported long term and will only stop receiving bug fixes once the usage rate drops low and the majority are using later releases.

## Post 1.0

After we reach the 1.0 release, we will start working on CoreWCF 2.0 and do not anticipate any minor version releases for 1.0. At the start of work on CoreWCF 2.0, we will update our dependency to the latest LTS version of .NET and ASP.NET Core.  

### Major version changes

The CoreWCF major version number will be incremented when one of two things happen:

* API removal or breaking behavior change
* .NET/ASP.NET Core non-patch level dependency version update

If the major version is incremented due to breaking API changes, we do not anticipate updating the .NET/ASP.NET Core dependency version. API removal will generally happen only when there's a replacement API already available and keeping the old one around causes problems.

### Minor version changes

The CoreWCF minor version number will be incremented when new features are added which require a new API. If a feature is added with no new API, for example enabling a scenario which previously threw `PlatformNotSupportedException`, then it will be decided on a case by case basis whether it's considered a bug fix or a new feature.  

### Servicing

Product bugs will generally be fixed on the latest stable minor release, and potentially the 1.0 migration release. Back porting to earlier major versions will be decided on a case by case basis.
