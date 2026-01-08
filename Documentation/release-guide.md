# Release Guide

This document details the strategy for releases and the process to manage them.

## Branching Strategy

Branches are considered ephemeral and only exist for the duration that they are needed. Tags are the mechanism used to mark and track a release. Once a release has been finalized, the commit used to create the release will be tagged and the branch deleted shortly after. If a release needs to be serviced, the release branch will be recreated from the release tag and either the change pulled directly into the branch or cherry-picked from main. Once a servicing release has been built, the commit will be tagged and the branch deleted. Git can sync to a tag as easily as a branch, so this keeps the repo cleaner. 

Release branches and tags will use the release/vX.Y naming scheme.

The primary development branch is main. New feature development will occur on a feature branch. Small bug fixes can be merged directly to main. When a feature is complete, it will be merged into main and the feature branch deleted. Usually a new minor release will be published shortly after.

## Versioning Strategy

CoreWCF will use a \<Major\>.\<Minor\> versioning scheme. Releases will happen when features are completed and not aligned to a specific timetable.

### Major Releases

Major releases will represent major milestones in the project with significant functionality that is deemed stable and ready for production usage.

Fundamental changes to the capabilities will only be made as part of a newly numbered major release. For example, dropping support for older .NET Core/ASP.NET Core versions, or api breakages.

### Minor releases

Minor releases represent ongoing development of the product, and signify when new incremental functionality is available.

Minor releases will not be breaking with respect to their major release. If you app works with 2.0, it can be recompiled to work with 2.1 without code changes. Binary compatibility without recompilation is not a goal between different numbered releases. Minor releases may add support for new .NET releases, but will not remove any support.

### Preview Releases

When work starts on new major versions, that are not going to be 100% compatible, or will have fundamental changes, then they will be released as previews, and the packages will be marked as preview in nuget. For example 2.0 work will be released as a 2.0 preview, rather than as a 1.x.

### .NET Versions

Support for specific versions of the .NET runtime in CoreWCF will be timeboxed by the support durations for those runtimes by Microsoft. For example .NET 5 will be supported until May 8 2022 (based on dates from [here](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core)). 

For engineering reasons, major releases of CoreWCF may drop support for older versions of .NET runtime. This is so that CoreWCF can take advanatge of runtime features that are not present in the older runtime. For example, the current plan is that shortly after 1.0 is shippped, work will start on 2.0 which will drop .NET Framework and .NET Core 3.1 support, this is because we need to take a dependency on newer ASP.NET core features that are not available down level.

## Microsoft support

We recognize how important support is to enterprise customers, and so we are pleased to announce that Microsoft Product Support will be available for CoreWCF customers.
  
- The following packages will be supported:
  - [CoreWCF.Primitives](https://www.nuget.org/packages/CoreWCF.Primitives)
  - [CoreWCF.Http](https://www.nuget.org/packages/CoreWCF.Http)
  - [CoreWCF.NetTcp](https://www.nuget.org/packages/CoreWCF.NetTcp)
  - [CoreWCF.WebHttp](https://www.nuget.org/packages/CoreWCF.WebHttp)
  - [CoreWCF.ConfigurationManager](https://www.nuget.org/packages/CoreWCF.ConfigurationManager)

See https://aka.ms/corewcf/support for more details on the support policy.

> Note: As a community project, support may also be available from other entities in conjunction with the use of their software & services.

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

   ```dos
       dotnet tool install --global dotnet-outdated-tool
       dotnet outdated -u -vl Minor -inc Microsoft.AspNetCore -inc Microsoft.CodeAnalysis -inc System CoreWCF.sln
       dotnet outdated -u -inc Microsoft.NET CoreWCF.sln
       dotnet outdated -u -vl Major -inc Microsoft.IdentityModel CoreWCF.sln
       dotnet outdated -u -exc Microsoft -exc Nerdbank.GitVersioning -exc System -exc RabbitMQ CoreWCF.sln
       dotnet outdated -u -vl Minor -inc Microsoft.Build CoreWCF.sln
   ```

   Check and manually update the version of `Nerdbank.GitVersioning` if needed. The version is specified in [Directory.Packages.props](/Directory.Packages.props).

   ```dos
       dotnet outdated -inc Nerdbank.GitVersioning CoreWCF.sln
   ```

   Check the pending changes to make sure the package version updated have been applied correctly.

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

6. Update the version.json file for any preview packages to match the main version.json file. As of January 2025 this is only the NetNamedPipe project, but that should change in the future. Search for any version.json files to be certain.

7. Push the main and release/vX.Y branches to GitHub so it reflects the changes made by NerdBank GitVersion.
8. Stabilization occurs in the release branch.
9. Commits should be made in the main branch and are cherry-picked into release/vX.Y if needed.
10. Build release packages and tag vX.Y.Z from the release/vX.Y branch.

   ```dos
       git tag -a -m "CoreWCF vX.Y.Z" vX.Y.Z
       git push upstream tag vX.Y.Z
   ```

11. Push package and symbol package to NuGet.
12. Delete the release/vX.Y branch.
