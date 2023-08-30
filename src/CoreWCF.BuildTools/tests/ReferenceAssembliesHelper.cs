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
        XDocument document = XDocument.Load(Path.GetFullPath(csprojPath));
        return document.Root!.Descendants("PackageReference").Select(x =>
            new PackageIdentity(x.Attribute("Include")!.Value, x.Attribute("Version")!.Value));
    }
}
