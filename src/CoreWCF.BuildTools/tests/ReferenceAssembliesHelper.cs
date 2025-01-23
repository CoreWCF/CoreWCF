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
        MSBuildLocator.RegisterDefaults();
    }

    internal static readonly Lazy<ReferenceAssemblies> Default = new(() =>
    {
        var coreWcfPrimitivesCsprojPath = Path.GetFullPath(Environment.CurrentDirectory + "../../../../../src/CoreWCF.Primitives/src/CoreWCF.Primitives.csproj");
        var coreWcfWebHttpCsprojPath = Path.GetFullPath(Environment.CurrentDirectory + "../../../../../src/CoreWCF.WebHttp/src/CoreWCF.WebHttp.csproj");
        var packages = ParsePackageReferences(coreWcfPrimitivesCsprojPath)
            .Union(ParsePackageReferences(coreWcfWebHttpCsprojPath))
            .Union(new[]
            {
                new PackageIdentity("System.ServiceModel.Primitives", "4.10.0"),
                new PackageIdentity("Microsoft.AspNetCore.Mvc", "2.1.3"),
                new PackageIdentity("Microsoft.AspNetCore.Authorization", "2.1.2")
            }).ToImmutableArray();
        return ReferenceAssemblies.Default.AddPackages(packages);
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
