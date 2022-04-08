# Release Guide

This document details the strategy for releases and the process to manage them.

## Branching Strategy

Branches are considered ephemeral and only exist for the duration that they are needed. Tags are the mechanism used to mark and track a release. Once a release has been finalized, the commit used to create the release will be tagged and the branch deleted shortly after. If a release needs to be serviced, the release branch will be recreated from the release tag and either the change pulled directly into the branch or cherry-picked from main. Once a servicing release has been built, the commit will be tagged and the branch deleted. Git can sync to a tag as easily as a branch, so this keeps the repo cleaner. 

Release branches and tags will use the release/vX.Y naming scheme.

The primary development branch is main. New feature development will occur on a feature branch. Small bug fixes can be merged directly to main. When a feature is complete, it will be merged into main and the feature branch deleted. Usually a new minor release will be published shortly after.

## Versioning Strategy

Core WCF will use a \<Major\>.\<Minor\> versioning scheme. Releases will happen when features are completed and not aligned to a specific timetable.

### Major Releases

Major releases will represent major milestones in the project with significant functionality that is deemed stable and ready for production usage.

Fundamental changes to the capabilities will only be made as part of a newly numbered major release. For example, dropping support for older .NET Core/ASP.NET Core versions, or api breakages.

### Minor releases

Minor releases represent ongoing development of the product, and signify when new incremental functionality is available.

Minor releases will not be breaking with respect to their major release. If you app works with 2.0, it can be recompiled to work with 2.1 without code changes. Binary compatibility without recompilation is not a goal between different numbered releases. Minor releases may add support for new .NET releases, but will not remove any support.

### Preview Releases

When work starts on new major versions, that are not going to be 100% compatible, or will have fundamental changes, then they will be released as previews, and the packages will be marked as preview in nuget. For example 2.0 work will be released as a 2.0 preview, rather than as a 1.x.

### .NET Versions

Support for specific versions of the .NET runtime in Core WCF will be timeboxed by the support durations for those runtimes by Microsoft. For example .NET 5 will be supported until May 8 2022 (based on dates from [here](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core)). 

For engineering reasons, major releases of CoreWCF may drop support for older versions of .NET runtime. This is so that Core WCF can take advanatge of runtime features that are not present in the older runtime. For example, the current plan is that shortly after 1.0 is shippped, work will start on 2.0 which will drop .NET Framework and .NET Core 3.1 support, this is because we need to take a dependency on newer ASP.NET core features that are not available down level.

## Microsoft support

- CoreWCF is a subset of WCF. Support is not intended to provide or assist with missing functionality found during app migration from WCF or in new development.
- OS Platform support must match the OS platform support for the underlying versions of .NET or .NET Core.
- CoreWCF will major and minor versions. Major versions can take breaking changes, minor versions will not (and we will fix accidental breaks in minor versions).
  - E.g. 1.0 -> 2.0 is a major version update with breaking changes while 2.0 -> 2.1 is a minor update and no intentional breaking changes.
- Major versions will declare what .NET runtime is supported and may drop support for an older .NET runtime that is still in support. 
  - E.g. CoreWCF 1.0 may support 3.1 and 5.0 while CoreWCF 2.0 may only choose to support 6.0.
- Minor versions always support the same .NET runtime as the parent major, they do not drop support for a .NET runtime that was supported by the parent major as long as the .NET runtime itself is still in support. 
  - E.g. If CoreWCF  1.0 supported .NET Core 3.1 then Core WCF 1.1 will also support .NET Core 3.1 as long as .NET Core 3.1 remains in support. Core WCF 2.x will follow whatever was declared supported when 2.0 shipped. 
- Minor versions will be supported for 6 months after the successor ships. 
  - E.g. CoreWCF 1.0 will be supported for 6 months after CoreWCF 1.1 ships. During the overlap both versions would get security fixes, but non-sec fixes would be available only for the latest minor i.e. 1.1.
- The following packages will be supported:
  - [CoreWCF.Primitives](https://www.nuget.org/packages/CoreWCF.Primitives)
  - [CoreWCF.Http 1.0.0-preview1](https://www.nuget.org/packages/CoreWCF.Http/1.0.0-preview1)
  - [CoreWCF.NetTcp 1.0.0-preview1](https://www.nuget.org/packages/CoreWCF.NetTcp/1.0.0-preview1)
  - [CoreWCF.WebHttp 1.0.0-preview1](https://www.nuget.org/packages/CoreWCF.WebHttp/1.0.0-preview1)
  - [CoreWCF.ConfigurationManager 1.0.0-preview1](https://www.nuget.org/packages/CoreWCF.ConfigurationManager/1.0.0-preview1)

As a community project, support may also be available from other entities in conjunction with the use of their software & services.

## Security issues

In the case of a security issue, fixes will be made available for:
- The last major.minor release
- The last minor release for the previous major if the current major was released within the last 6 months
  - eg 2.3 would continue to supported until 6 months after 3.0 is released. 
- As an update to an existing preview or in the next preview depending on timing considerations.

Security issues should be reported via email to security@corewcf.net as described in SECURITY.md, those issues will then be routed to the project maintainers.

## Release Steps

Here are the steps to release a new version:

1. Update to the latest patch version of nuget dependencies
2. Update the CoreWCF.BuildTools AnalyzerReleases markdown documents if new analyzer rules have been created. Instructions for what changes may need to be made are located [here](https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md).
3. If any changes were made in steps 1 & 2, create a branch, commit the changes, push to your fork, create a PR and merge the changes. Update your local main branch after the PR has been merged.
4. Install Nerdbank.GitVersioning

   ```dos
       dotnet tool install --tool-path . nbgv
   ```

5. Prepare the release

   ```dos
       .\nbgv.exe prepare-release
   ```

   This creates the release/vX.Y release branch and updates the main branch to use the vX.(Y+1) version.

6. Push the main and release/vX.Y branches to GitHub so it reflects the changes made by NerdBank GitVersion.
7. Stabilization occurs in the release branch.
8. Commits should be made in the main branch and are cherry-picked into release/vX.Y if needed.
9. Build release packages and tag vX.Y.Z from the release/vX.Y branch.
10. Push package and symbol package to NuGet.
11. Delete the release/vX.Y branch.
