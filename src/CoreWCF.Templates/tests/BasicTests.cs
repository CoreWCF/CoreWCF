using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Templates.Test.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Templates.Tests;

public class BasicTests : IClassFixture<ProjectFactoryFixture>
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
        public const string Net8 = "net8.0";
        public const string Net6 = "net6.0";
        public const string Net48 = "net48";
        public const string Net472 = "net472";
        public const string Net462 = "net462";
    }

    public class TestVariations : TheoryData<TestVariation>
    {
        public TestVariations()
        {
            foreach (var testVariation in GetTestVariations())
            {
                Add(testVariation);
            }
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

    private static IEnumerable<TestVariation> GetTestVariations()
    {
        IEnumerable<TestVariation> GetFrameworksVariations()
        {
            yield return TestVariation.New();
            yield return TestVariation.New().Framework(Frameworks.Net8);
            yield return TestVariation.New().Framework(Frameworks.Net6);

            if (!OperatingSystem.IsWindows())
            {
                yield break;
            }

            yield return TestVariation.New().Framework(Frameworks.Net48);
            yield return TestVariation.New().Framework(Frameworks.Net472);
            yield return TestVariation.New().Framework(Frameworks.Net462);
        }

        IEnumerable<TestVariation> GetHttpsVariations(TestVariation testVariation)
        {
            yield return (TestVariation)testVariation.Clone();
            yield return ((TestVariation)testVariation.Clone()).NoHttps();
        }

        IEnumerable<TestVariation> GetNoWsdlVariations(TestVariation testVariation)
        {
            yield return (TestVariation)testVariation.Clone();
            yield return ((TestVariation)testVariation.Clone()).NoWsdl();
        }

        IEnumerable<TestVariation> GetUseProgramMainVariations(TestVariation testVariation)
        {
            yield return (TestVariation)testVariation.Clone();
            yield return ((TestVariation)testVariation.Clone()).UseProgramMain();
        }

        IEnumerable<TestVariation> GetUseOperationInvokerGeneratorVariations(TestVariation testVariation)
        {
            yield return (TestVariation)testVariation.Clone();
            yield return ((TestVariation)testVariation.Clone()).UseOperationInvokerGenerator();
        }

        foreach (var frameworksVariation in GetFrameworksVariations())
        {
            foreach (var httpsVariation in GetHttpsVariations(frameworksVariation))
            {
                foreach (var wsdlVariation in GetNoWsdlVariations(httpsVariation))
                {
                    foreach (var useProgramMainVariation in GetUseProgramMainVariations(wsdlVariation))
                    {
                        foreach (var useOperationInvokerGeneratorVariation in GetUseOperationInvokerGeneratorVariations(useProgramMainVariation))
                        {
                            yield return useOperationInvokerGeneratorVariation;
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [ClassData(typeof(TestVariations))]
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
        var targetFramework = args.FirstOrDefault(arg => arg.StartsWith("--framework"))?.Split(" ")[1] ?? Frameworks.Net8;
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
