// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.Testing;

namespace CoreWCF.BuildTools.Tests;

internal static class ReferenceAssembliesHelper
{
    static ReferenceAssembliesHelper()
    {
        RegisterMSBuild();
    }

    private static void RegisterMSBuild()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        // Prefer the highest non-preview .NET SDK to avoid evaluation failures caused by
        // broken preview SDK builds (e.g., 11.0.100-preview.3.26207.106 ships an unresolvable
        // 'Microsoft.NET.SDK.WorkloadAutoImportPropsLocator' SDK resolver, which makes
        // ProjectCollection.LoadProject fail even for a netstandard2.0 csproj).
        //
        // We restrict discovery to DotNetSdk so MSBuildLocator points MSBuildSDKsPath at a
        // stable SDK's Sdks/ folder. On .NET Framework testhost (net472), the default query
        // also includes Visual Studio Build Tools (Version 17.x) which would win the
        // OrderByDescending(Version) sort over 11.0 SDK; registering a VS instance does NOT
        // redirect the SDK resolver, so the broken preview SDK on disk would still be loaded.
        // The csproj evaluated here targets netstandard2.0, so any stable SDK works.
        var queryOptions = new VisualStudioInstanceQueryOptions
        {
            DiscoveryTypes = DiscoveryType.DotNetSdk
        };
        var instance = MSBuildLocator.QueryVisualStudioInstances(queryOptions)
            .Where(i => !IsPreview(i))
            .OrderByDescending(i => i.Version)
            .FirstOrDefault();

        if (instance != null)
        {
            MSBuildLocator.RegisterInstance(instance);
        }
        else
        {
            // No stable SDK available; fall back to the default behavior so the failure
            // surfaces with the same diagnostics as before.
            MSBuildLocator.RegisterDefaults();
        }
    }

    private static bool IsPreview(Microsoft.Build.Locator.VisualStudioInstance instance)
    {
        if (!string.IsNullOrEmpty(instance.Name) &&
            instance.Name.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        // System.Version doesn't carry pre-release labels; inspect the SDK folder name,
        // which does (e.g., '11.0.100-preview.3.26207.106').
        try
        {
            var folderName = new DirectoryInfo(instance.MSBuildPath).Parent?.Name;
            if (!string.IsNullOrEmpty(folderName) &&
                folderName.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        catch
        {
            // Best-effort; treat as non-preview if the path can't be inspected.
        }

        return false;
    }

    internal static readonly Lazy<ReferenceAssemblies> Default = new(() =>
    {
        var coreWcfPrimitivesCsprojPath = Path.GetFullPath(Environment.CurrentDirectory + "../../../../../src/CoreWCF.Primitives/src/CoreWCF.Primitives.csproj");
        var coreWcfWebHttpCsprojPath = Path.GetFullPath(Environment.CurrentDirectory + "../../../../../src/CoreWCF.WebHttp/src/CoreWCF.WebHttp.csproj");
        var packages = ParsePackageReferences(coreWcfPrimitivesCsprojPath)
            .Union(ParsePackageReferences(coreWcfWebHttpCsprojPath))
            .Union(new[]
            {
                new PackageIdentity("System.ServiceModel.Primitives", "8.1.2"),
                new PackageIdentity("Microsoft.AspNetCore.Mvc", "2.3.9"),
                new PackageIdentity("Microsoft.AspNetCore.Authorization", "2.3.9")
            }).ToImmutableArray();
        return ReferenceAssemblies.Net.Net80.AddPackages(packages);
    });

    private static IEnumerable<PackageIdentity> ParsePackageReferences(string csprojPath)
    {
        var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
        var project = projectCollection.LoadProject(csprojPath);
        project.ReevaluateIfNecessary();

        var packageVersionsList = project.GetItems("PackageVersion")
            .Select(item => new PackageIdentity(item.EvaluatedInclude, item.GetMetadataValue("Version")))
            .Where(identity => !string.IsNullOrEmpty(identity.Version));
        Dictionary<string, PackageIdentity> packageVersions = new();
        foreach (var packageVersion in packageVersionsList)
        {
            if (packageVersions.ContainsKey(packageVersion.Id))
            {
                packageVersions[packageVersion.Id] = packageVersion;
            }
            else
            {
                packageVersions.Add(packageVersion.Id, packageVersion);
            }
        }
        var packageReferences = project.GetItems("PackageReference")
            .Where(item => packageVersions.ContainsKey(item.EvaluatedInclude))
            .Select(item =>
            {
                string version = string.Empty;
                if (!string.IsNullOrEmpty(item.GetMetadataValue("Version")))
                {
                    version = item.GetMetadataValue("Version");
                }
                else if(!string.IsNullOrEmpty(item.GetMetadataValue("VersionOverride")))
                {
                    version = item.GetMetadataValue("VersionOverride");
                }
                else if(packageVersions.TryGetValue(item.EvaluatedInclude, out var packageIdentity))
                {
                    version = packageIdentity.Version;
                }
                return new PackageIdentity(item.EvaluatedInclude, version);
            })
            .Where(identity => !string.IsNullOrEmpty(identity.Version));

        return packageReferences;
    }
}
