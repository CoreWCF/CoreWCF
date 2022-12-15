using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Templates.Test.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Templates.Tests;

[Collection("CoreWCF.Templates collection")]
public class BasicTests
{
    public BasicTests(ProjectFactoryFixture projectFactory, ITestOutputHelper output)
    {
        ProjectFactory = projectFactory;
        _output = output;
    }

    public ProjectFactoryFixture ProjectFactory { get; }

    private ITestOutputHelper _output;

    public static class Frameworks
    {
        public const string Net7 = "net7.0";
        public const string Net6 = "net6.0";
        public const string Net48 = "net48";
        public const string Net472 = "net472";
        public const string Net462 = "net462";
    }

    public class TestVariation
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

        public TestVariation Framework(string framework)
        {
            Arguments.Add($"--framework {framework}");
            return this;
        }

        public static implicit operator object[](TestVariation testVariation) => new object[] { testVariation };
    }

    public static IEnumerable<object[]> GetTestVariations()
    {
        yield return TestVariation.New();
        yield return TestVariation.New().Framework(Frameworks.Net7);
        yield return TestVariation.New().Framework(Frameworks.Net6);
        yield return TestVariation.New().NoHttps();
        yield return TestVariation.New().Framework(Frameworks.Net7).NoHttps();
        yield return TestVariation.New().Framework(Frameworks.Net6).NoHttps();
        yield return TestVariation.New().NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net7).NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net6).NoWsdl();
        yield return TestVariation.New().NoHttps().NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net7).NoHttps().NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net6).NoHttps().NoWsdl();
        yield return TestVariation.New().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net7).UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net6).UseProgramMain();
        yield return TestVariation.New().NoHttps().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net7).NoHttps().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net6).NoHttps().UseProgramMain();
        yield return TestVariation.New().NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net7).NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net6).NoWsdl().UseProgramMain();
        yield return TestVariation.New().NoHttps().NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net7).NoHttps().NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net6).NoHttps().NoWsdl().UseProgramMain();

        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        yield return TestVariation.New().Framework(Frameworks.Net48);
        yield return TestVariation.New().Framework(Frameworks.Net472);
        yield return TestVariation.New().Framework(Frameworks.Net462);
        yield return TestVariation.New().Framework(Frameworks.Net48).NoHttps();
        yield return TestVariation.New().Framework(Frameworks.Net472).NoHttps();
        yield return TestVariation.New().Framework(Frameworks.Net462).NoHttps();
        yield return TestVariation.New().Framework(Frameworks.Net48).NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net472).NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net462).NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net48).NoHttps().NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net472).NoHttps().NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net462).NoHttps().NoWsdl();
        yield return TestVariation.New().Framework(Frameworks.Net48).UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net472).UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net462).UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net48).NoHttps().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net472).NoHttps().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net462).NoHttps().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net48).NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net472).NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net462).NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net48).NoHttps().NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net472).NoHttps().NoWsdl().UseProgramMain();
        yield return TestVariation.New().Framework(Frameworks.Net462).NoHttps().NoWsdl().UseProgramMain();
    }

    [Theory]
    [MemberData(nameof(GetTestVariations))]
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
        var targetFramework = args.FirstOrDefault(arg => arg.StartsWith("--framework"))?.Split(" ")[1] ?? "net6.0";
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
                await aspNetProcess.AssertStatusCode("/Service.svc", System.Net.HttpStatusCode.BadRequest);
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
                await aspNetProcess.AssertStatusCode("/Service.svc", System.Net.HttpStatusCode.BadRequest);
            }
        }
    }
}
