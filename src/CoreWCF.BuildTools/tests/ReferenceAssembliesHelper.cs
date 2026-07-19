// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Testing;

namespace CoreWCF.BuildTools.Tests;

internal static class ReferenceAssembliesHelper
{
    // Parsing the csproj/Directory.Packages.props as plain XML rather than going through
    // Microsoft.Build.Evaluation avoids loading the .NET SDK at runtime. The .NET SDK
    // resolver picks the highest installed SDK on the machine, and a broken preview SDK
    // (e.g., 11.0.100-preview.3.26207.106 was missing the
    // 'Microsoft.NET.SDK.WorkloadAutoImportPropsLocator' resolver) makes
    // ProjectCollection.LoadProject throw even for a netstandard2.0 csproj. We only need
    // the union of <PackageReference> ids and their resolved versions from CPM, which is
    // straightforward XML.
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
        var packageVersions = LoadCentralPackageVersions(csprojPath);
        var csproj = XDocument.Load(csprojPath);

        var results = new List<PackageIdentity>();
        foreach (var item in csproj.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
        {
            var id = (string)item.Attribute("Include");
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var version = GetMetadataValue(item, "Version") ?? GetMetadataValue(item, "VersionOverride");
            if (string.IsNullOrEmpty(version) && packageVersions.TryGetValue(id, out var centralVersion))
            {
                version = centralVersion;
            }

            if (!string.IsNullOrEmpty(version))
            {
                results.Add(new PackageIdentity(id, version));
            }
        }

        return results;
    }

    private static string GetMetadataValue(XElement item, string name)
    {
        var attr = item.Attribute(name);
        if (attr != null && !string.IsNullOrEmpty(attr.Value))
        {
            return attr.Value;
        }

        var child = item.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        return child != null && !string.IsNullOrEmpty(child.Value) ? child.Value : null;
    }

    // Walk up from the csproj directory collecting Directory.Packages.props files (CPM
    // walks up the tree, stopping at the first file that sets ManagePackageVersionsCentrally
    // or at the repo root). For our two well-known csprojs this is a single file at the
    // repo root, but the loop keeps the helper resilient to layout changes.
    private static Dictionary<string, string> LoadCentralPackageVersions(string csprojPath)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = new DirectoryInfo(Path.GetDirectoryName(csprojPath));
        var files = new List<string>();
        while (dir != null)
        {
            var file = Path.Combine(dir.FullName, "Directory.Packages.props");
            if (File.Exists(file))
            {
                files.Add(file);
            }
            dir = dir.Parent;
        }

        // Closest file wins, so process from root downward.
        files.Reverse();
        foreach (var file in files)
        {
            var doc = XDocument.Load(file);
            foreach (var item in doc.Descendants().Where(e => e.Name.LocalName == "PackageVersion"))
            {
                var include = (string)item.Attribute("Include");
                var update = (string)item.Attribute("Update");
                var version = (string)item.Attribute("Version");
                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(include))
                {
                    if (!versions.ContainsKey(include))
                    {
                        versions[include] = version;
                    }
                }
                else if (!string.IsNullOrEmpty(update) && versions.ContainsKey(update))
                {
                    versions[update] = version;
                }
            }
        }

        return versions;
    }
}
