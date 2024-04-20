// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyAnalyzer = CSharpAnalyzerVerifier<CoreWCF.BuildTools.PrivateServiceContractAnalyzer>;

namespace CoreWCF.BuildTools.Tests;

public class PrivateServiceContractAnalyzerTests
{
    private const string SSMNamespace = "System.ServiceModel";
    private const string CoreWCFNamespace = "CoreWCF";

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task DefaultNonOptinTests(string attributeNamespace)
    {
        var test = new VerifyAnalyzer.Test
        {
            TestState =
            {
                Sources =
                {
@$"
namespace MyProject
{{
    class Container
    {{
        [{attributeNamespace}.ServiceContract]
        private interface IIdentityService
        {{
            [{attributeNamespace}.OperationContract]
            string Echo(string input);
        }}

        public class IdentityService : IIdentityService
        {{
            public string Echo(string input) => input;
        }}

        static Container()
        {{
            new IdentityService().Echo("""");
        }}
    }}

}}
"
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task BasicTests(string attributeNamespace)
    {
        var test = new VerifyAnalyzer.Test
        {
            TestState =
            {
                Sources =
                {
                    @$"
namespace MyProject
{{
    class Container
    {{
        [{attributeNamespace}.ServiceContract]
        private interface IIdentityService
        {{
            [{attributeNamespace}.OperationContract]
            string Echo(string input);
        }}

        public class IdentityService : IIdentityService
        {{
            public string Echo(string input) => input;
        }}

        static Container()
        {{
            MyProject.Container.IdentityService service = new IdentityService();
            service.Echo("""");
        }}
    }}

}}
"
                },
                AnalyzerConfigFiles =
                {
                    (typeof(OperationInvokerGenerator),"/.globalconfig", """
                                                                         is_global = true
                                                                         build_property.EnableCoreWCFOperationInvokerGenerator = true
                                                                         """)
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.PrivateServiceContractAnalyzer__03XX.PrivateServiceContract)
                        .WithSpan(21, 13, 21, 29)
                        .WithDefaultPath("/0/Test0.cs")
                        .WithArguments("MyProject.Container.IIdentityService")
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task BasicTestsWithInterfaceVariable(string attributeNamespace)
    {
        var test = new VerifyAnalyzer.Test
        {
            TestState =
            {
                Sources =
                {
                    @$"
namespace MyProject
{{
    class Container
    {{
        [{attributeNamespace}.ServiceContract]
        private interface IIdentityService
        {{
            [{attributeNamespace}.OperationContract]
            string Echo(string input);
        }}

        public class IdentityService : IIdentityService
        {{
            public string Echo(string input) => input;
        }}

        static Container()
        {{
            MyProject.Container.IIdentityService service = new IdentityService();
            service.Echo("""");
        }}
    }}

}}
"
                },
                AnalyzerConfigFiles =
                {
                    (typeof(OperationInvokerGenerator),"/.globalconfig", """
                                                                         is_global = true
                                                                         build_property.EnableCoreWCFOperationInvokerGenerator = true
                                                                         """)
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.PrivateServiceContractAnalyzer__03XX.PrivateServiceContract)
                        .WithSpan(21, 13, 21, 29)
                        .WithDefaultPath("/0/Test0.cs")
                        .WithArguments("MyProject.Container.IIdentityService")
                }
            }
        };
        await test.RunAsync();
    }

}
