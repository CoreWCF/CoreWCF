// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.BuildTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using VerifyAnalyzer = CSharpAnalyzerVerifier<CoreWCF.BuildTools.AuthorizationAttributesAnalyzer>;

namespace CoreWCF.BuildTools.Tests;

public class AuthorizationAttributeAnalyserTests
{
    private const string SSMNamespace = "System.ServiceModel";
    private const string CoreWCFNamespace = "CoreWCF";

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
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);
    }}

    public class IdentityService : IIdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public string Echo(string input) => input;
    }}
}}
"
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupported)
                        .WithSpan(14, 23, 14, 27)
                        .WithDefaultPath("/0/Test0.cs")
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task AuthorizeDataAuthenticationSchemesPropertySetTests(string attributeNamespace)
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
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);
    }}

    public class IdentityService : IIdentityService
    {{
        [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = ""Scheme1,Scheme2"")]
        public string Echo(string input) => input;
    }}
}}
"
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.AuthorizeDataAuthenticationSchemesPropertyIsNotSupported)
                        .WithSpan(14, 23, 14, 27)
                        .WithDefaultPath("/0/Test0.cs")
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task AuthorizeDataRolesPropertySetTests(string attributeNamespace)
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
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);
    }}

    public class IdentityService : IIdentityService
    {{
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = ""Role1,Role2"")]
        public string Echo(string input) => input;
    }}
}}
"
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.AuthorizeDataRolesPropertyIsNotSupported)
                        .WithSpan(14, 23, 14, 27)
                        .WithDefaultPath("/0/Test0.cs")
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task AuthorizeOnAUserProvidedOperationContractImplementationToBeGeneratedBySourceGenTests(string attributeNamespace)
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
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        int Echo(int input);
        [{attributeNamespace}.OperationContract(Name = ""EchoString"")]
        string Echo(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        [Microsoft.AspNetCore.Authorization.Authorize]
        public int Echo(int input, [CoreWCF.Injected] HttpContext context) => input;
        [Microsoft.AspNetCore.Authorization.Authorize]
        public string Echo(string input, [Microsoft.AspNetCore.Mvc.FromServices] HttpContext context) => input;
    }}
}}
", ("MyProject_IIdentityService_Echo.g.cs", @$"
namespace MyProject
{{
    public partial class IdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AuthorizeAttribute()]
        public int Echo(int input) => input;
    }}
}}
"), ("MyProject_IIdentityService_EchoString.g.cs", @$"
namespace MyProject
{{
    public partial class IdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AuthorizeAttribute()]
        public string Echo(string input) => input;
    }}
}}
")
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task AllowAnonymousOnAUserProvidedOperationContractImplementationToBeGeneratedBySourceGenTests(string attributeNamespace)
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
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        int Echo(int input);
        [{attributeNamespace}.OperationContract(Name = ""EchoString"")]
        string Echo(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public int Echo(int input, [CoreWCF.Injected] HttpContext context) => input;
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public string Echo(string input, [Microsoft.AspNetCore.Mvc.FromServices] HttpContext context) => input;
    }}
}}
", ("MyProject_IIdentityService_Echo.g.cs", @$"
namespace MyProject
{{
    public partial class IdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute()]
        public int Echo(int input) => input;
    }}
}}
"), ("MyProject_IIdentityService_EchoString.g.cs", @$"
namespace MyProject
{{
    public partial class IdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute()]
        public string Echo(string input) => input;
    }}
}}
")
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupported)
                        .WithSpan(16, 20, 16, 24)
                        .WithDefaultPath("/0/Test0.cs"),
                    new DiagnosticResult(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupported)
                        .WithSpan(18, 23, 18, 27)
                        .WithDefaultPath("/0/Test0.cs")
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task AllowAnonymousOnServiceContractImplementationTests(string attributeNamespace)
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
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);
    }}

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
    }}
}}
"
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.AllowAnonymousAttributeIsNotSupported)
                        .WithSpan(12, 18, 12, 33)
                        .WithDefaultPath("/0/Test0.cs")
                }
            }
        };
        await test.RunAsync();
    }

    [Theory]
    [InlineData(SSMNamespace)]
    [InlineData(CoreWCFNamespace)]
    public async Task AuthorizeOnServiceContractImplementationTests(string attributeNamespace)
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
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);
    }}

    [Microsoft.AspNetCore.Authorization.Authorize]
    public class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
    }}
}}
"
                },
                ExpectedDiagnostics =
                {
                    new DiagnosticResult(DiagnosticDescriptors.AuthorizeAttributeIsNotSupportedOnClass)
                        .WithSpan(12, 18, 12, 33)
                        .WithArguments("IdentityService")
                        .WithDefaultPath("/0/Test0.cs")
                }
            }
        };
        await test.RunAsync();
    }

    [Fact]
    public async Task ShouldWorkAsUsualForMVCControllers()
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
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class HomeController : Microsoft.AspNetCore.Mvc.Controller
    {{
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public string Echo(string input) => input;
    }}

    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class HomeController2 : Microsoft.AspNetCore.Mvc.Controller
    {{
        public string Echo(string input) => input;
    }}
}}
"
                }
            }
        };
        await test.RunAsync();
    }
}
