// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Testing;
using static Microsoft.CodeAnalysis.Testing.ReferenceAssemblies;

namespace CoreWCF.BuildTools.Tests;

internal static class ReferenceAssembliesHelper
{
    public static ReferenceAssemblies ReferenceAssembliesDefaults
    {
        get
        {
#if NET472
            return NetFramework.Net472.Default;
#elif NET6_0
            return Net.Net60;
#elif NET7_0
            return Net.Net70;
#elif NET8_0
            return Net.Net80;
#elif NET9_0
            return Net.Net90;
#endif
            throw new PlatformNotSupportedException();
        }
    }

    internal static readonly Lazy<ReferenceAssemblies> Default = new(() =>
	{
		var coreWcfPrimitivesCsprojPath = Path.GetFullPath(Environment.CurrentDirectory + "../../../../../src/CoreWCF.Primitives/src/CoreWCF.Primitives.csproj");
		var coreWcfWebHttpCsprojPath = Path.GetFullPath(Environment.CurrentDirectory + "../../../../../src/CoreWCF.WebHttp/src/CoreWCF.WebHttp.csproj");
		var packages = ParsePackageReferences(coreWcfPrimitivesCsprojPath)
			.Union(ParsePackageReferences(coreWcfWebHttpCsprojPath))
			.Union(new[]
			{
#if NET8_0_OR_GREATER
                new PackageIdentity("System.ServiceModel.Primitives", "8.0.0"),
#elif NET6_0_OR_GREATER
                new PackageIdentity("System.ServiceModel.Primitives", "6.2.0"),
#else
                new PackageIdentity("System.ServiceModel.Primitives", "4.10.3"),
#endif
                new PackageIdentity("Microsoft.AspNetCore.Mvc", "2.1.3"),
                new PackageIdentity("Microsoft.AspNetCore.Authorization", "2.1.2")
            }).ToImmutableArray();
		return ReferenceAssembliesDefaults.AddPackages(packages);
	});

	private static IEnumerable<PackageIdentity> ParsePackageReferences(string csprojPath)
	{
		XDocument document = XDocument.Load(Path.GetFullPath(csprojPath));

        // See CoreWCF.Primitives .csproj -file for Conditions to filter off
        return document.Root!.Descendants("ItemGroup")
            .Where(x => x.Attribute("Condition")?.Value != "$(IsAspNetCore) != true")
#if NET8_0_OR_GREATER
            .Where(x => x.Attribute("Condition")?.Value != "'$(TargetFramework)'=='net6.0' or $(IsAspNetCore) != true")
#elif NET6_0_OR_GREATER
            .Where(x => x.Attribute("Condition")?.Value != "'$(TargetFramework)'=='net8.0'" && x.Attribute("Condition")?.Value != "'$(TargetFramework)'=='net8.0'")
#else
            .Where(x => x.Attribute("Condition")?.Value != "'$(TargetFramework)'=='net8.0'")
#endif
            .Descendants("PackageReference")
			.Select(x => new PackageIdentity(x.Attribute("Include")!.Value, x.Attribute("Version")!.Value));
	}
}
