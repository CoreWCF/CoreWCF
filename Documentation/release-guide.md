# Release Guide

This document details the strategy for releases and the process to manage them.

## Branching Strategy

Branches are considered ephemeral and only exist for the duration that they are needed. Tags are the mechanism used to mark and track a release. Once a release has been finalized, the commit used to create the release will be tagged and the branch deleted shortly after. If a release needs to be serviced, the release branch will be recreated from the release tag and either the change pulled directly into the branch or cherry-picked from main. Once a servicing release has been built, the commit will be tagged and the branch deleted. Release branches and tags will use the release/vX.Y naming scheme.  
The primary development branch is main. New feature development will occur on a feature branch. Small bug fixes can be merged directly to main. When a feature is complete, it will be merged into main and the feature branch deleted. Usually a new minor release will be published shortly after.

## Versioning Strategy

The initial release will have a 0.1 version number. The major version number will be incremented to 1 once a few key features are available such as WSDL generation, at which point a 1.0 version will be released. New releases will happen with each new feature at which point the minor version number will be incremented. Releases will happen when features are completed and not aligned to a specific timetable. The major version will be incremented when there's a likelihood that an application will need to make changes to adopt the new version. For example, dropping support for older .NET Core/ASP.NET Core versions, or api breakages.

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
8. Commits first occur in main branch and are cherry-picked into release/vX.Y if needed.
9. Build release packages and tag vX.Y.Z from the release/vX.Y branch.
10. Push package and symbol package to NuGet.
11. Delete the release/vX.Y branch.
