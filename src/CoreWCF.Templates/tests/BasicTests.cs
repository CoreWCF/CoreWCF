// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Templates.Test.Helpers;
using Xunit;
using Xunit.Sdk;

namespace CoreWCF.Templates.Tests;

// Single test class on purpose. Parallelism across frameworks is provided by the GitHub Actions
// matrix in .github/workflows/{pr,ci}.yml — each entry passes a `Framework=<tfm>` filter to
// `dotnet test`, and every theory row produced by GetVariations() carries a matching `Framework`
// trait so the filter selects exactly the rows for that TFM.
//
// Adding a new TFM is a one-line change in GetFrameworks() plus one matrix entry per workflow
// (the workflow files are touched per-TFM anyway because the regular build matrix lists TFMs
// explicitly).
public class BasicTests : IClassFixture<ProjectFactoryFixture>
{
    public BasicTests(ProjectFactoryFixture projectFactory, ITestOutputHelper output)
    {
        ProjectFactory = projectFactory;
        _output = output;
    }

    public ProjectFactoryFixture ProjectFactory { get; }

    private readonly ITestOutputHelper _output;

    // Trait value used (and matched by the workflow `--filter Framework=default`) for the
    // variation that omits `--framework` and so picks up whatever the template defaults to.
    private const string DefaultFrameworkTraitValue = "default";

    private static IEnumerable<string> GetFrameworks()
    {
        // null = template default (no --framework argument)
        yield return null;
        yield return "net10.0";
        yield return "net9.0";
        yield return "net8.0";

        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        // .NET Framework coverage is intentionally limited to net472 to match the regular build
        // matrix in pr.yml / ci.yml. The 2022 introduction commit also exercised net48 and net462
        // but they have been dropped to keep the Windows test-templates critical path short.
        yield return "net472";
    }

    public static IEnumerable<TheoryDataRow<TestVariation>> GetVariations()
    {
        foreach (var framework in GetFrameworks())
        {
            var seed = framework is null
                ? TestVariation.New()
                : TestVariation.New().Framework(framework);

            var traitValue = framework ?? DefaultFrameworkTraitValue;

            foreach (var variation in ExpandVariations(seed))
            {
                yield return new TheoryDataRow<TestVariation>(variation)
                {
                    Traits = { ["Framework"] = new HashSet<string> { traitValue } }
                };
            }
        }
    }

    // Generates the 16-variation product (https × wsdl × programMain × invokerGenerator) for a
    // single framework seed.
    private static IEnumerable<TestVariation> ExpandVariations(TestVariation seed)
    {
        static IEnumerable<TestVariation> Branch(TestVariation v, Action<TestVariation> mutate)
        {
            yield return v.Clone();
            var mutated = v.Clone();
            mutate(mutated);
            yield return mutated;
        }

        foreach (var v1 in Branch(seed, v => v.NoHttps()))
        foreach (var v2 in Branch(v1, v => v.NoWsdl()))
        foreach (var v3 in Branch(v2, v => v.UseProgramMain()))
        foreach (var v4 in Branch(v3, v => v.UseOperationInvokerGenerator()))
        {
            yield return v4;
        }
    }

    public class TestVariation : IXunitSerializable, ICloneable
    {
        public List<string> Arguments { get; private set; } = new List<string>();

        public static TestVariation New() => new TestVariation();

        public bool AssertMetadataEndpoint { get; private set; } = true;

        public bool UseHttps { get; private set; } = true;

        public TestVariation UseProgramMain()
        {
            Arguments.Add("--use-program-main");
            return this;
        }

        public TestVariation NoWsdl()
        {
            Arguments.Add("--no-wsdl");
            AssertMetadataEndpoint = false;
            return this;
        }

        public TestVariation NoHttps()
        {
            UseHttps = false;
            Arguments.Add("--no-https");
            return this;
        }

        public TestVariation UseOperationInvokerGenerator()
        {
            Arguments.Add("--use-operation-invoker-generator");
            return this;
        }

        public TestVariation Framework(string framework)
        {
            Arguments.Add($"--framework {framework}");
            return this;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            Arguments = JsonSerializer.Deserialize<List<string>>(info.GetValue<string>(nameof(Arguments)));
            AssertMetadataEndpoint = bool.Parse(info.GetValue<string>(nameof(AssertMetadataEndpoint)));
            UseHttps = bool.Parse(info.GetValue<string>(nameof(UseHttps)));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Arguments), JsonSerializer.Serialize(Arguments));
            info.AddValue(nameof(AssertMetadataEndpoint), AssertMetadataEndpoint.ToString());
            info.AddValue(nameof(UseHttps), UseHttps.ToString());
        }

        object ICloneable.Clone()
        {
            TestVariation clone = new();
            clone.Arguments = new();
            foreach (var argument in Arguments)
            {
                clone.Arguments.Add(argument);
            }

            clone.UseHttps = UseHttps;
            clone.AssertMetadataEndpoint = AssertMetadataEndpoint;

            return clone;
        }

        public TestVariation Clone()
        {
            ICloneable cloneable = this;
            return (TestVariation)cloneable.Clone();
        }
    }

    [Theory]
    [MemberData(nameof(GetVariations))]
    public async Task CoreWCFTemplateDefault(TestVariation variation)
    {
        await CoreWCFTemplateDefaultCore(variation.Arguments.ToArray(),
            variation.AssertMetadataEndpoint,
            variation.UseHttps,
            variation.UseHttps ? "https" : "http"
            );
    }

    private async Task CoreWCFTemplateDefaultCore(string[] args, bool assertMetadataEndpoint, bool useHttps, string expectedListeningUriScheme)
    {
        var targetFramework = args.FirstOrDefault(arg => arg.StartsWith("--framework"))?.Split(" ")[1] ?? "net8.0";
        var project = ProjectFactory.GetOrCreateProject($"corewcf-{Guid.NewGuid()}", targetFramework,  _output);

        var createResult = await project.RunDotNetNewAsync("corewcf", args: args);

        Assert.True(0 == createResult.ExitCode, ErrorMessages.GetFailedProcessMessage("create", project, createResult));

        var restoreResult = await project.RunDotNetRestoreAsync();

        Assert.True(0 == restoreResult.ExitCode, ErrorMessages.GetFailedProcessMessage("restore", project, restoreResult));

        var publishResult = await project.RunDotNetPublishAsync();
        Assert.True(0 == publishResult.ExitCode, ErrorMessages.GetFailedProcessMessage("publish", project, publishResult));

        // Run dotnet build after publish. The reason is that one uses Config = Debug and the other uses Config = Release
        // The output from publish will go into bin/Release/netcoreappX.Y/publish and won't be affected by calling build
        // later, while the opposite is not true.

        var buildResult = await project.RunDotNetBuildAsync();
        Assert.True(0 == buildResult.ExitCode, ErrorMessages.GetFailedProcessMessage("build", project, buildResult));

        using (var aspNetProcess = project.StartBuiltProjectAsync(useHttps: useHttps))
        {
            Assert.False(
               aspNetProcess.Process.HasExited,
               ErrorMessages.GetFailedProcessMessageOrEmpty("Run built project", project, aspNetProcess.Process));
            Assert.Equal(expectedListeningUriScheme, aspNetProcess.ListeningUri.Scheme);
            await aspNetProcess.AssertServiceEndpoint();
            if (assertMetadataEndpoint)
            {
                await aspNetProcess.AssertMetadataEndpoint();
            }
            else
            {
                await aspNetProcess.AssertStatusCode("/Service.svc", HttpStatusCode.BadRequest);
            }
        }

        using (var aspNetProcess = project.StartPublishedProjectAsync(
            useHttps: useHttps,
            usePublishedAppHost: OperatingSystem.IsWindows()))
        {
            Assert.False(
                aspNetProcess.Process.HasExited,
                ErrorMessages.GetFailedProcessMessageOrEmpty("Run published project", project, aspNetProcess.Process));
            Assert.Equal(expectedListeningUriScheme, aspNetProcess.ListeningUri.Scheme);
            await aspNetProcess.AssertServiceEndpoint();
            if (assertMetadataEndpoint)
            {
                await aspNetProcess.AssertMetadataEndpoint();
            }
            else
            {
                await aspNetProcess.AssertStatusCode("/Service.svc", HttpStatusCode.BadRequest);
            }
        }
    }
}
