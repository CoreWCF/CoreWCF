// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using VerifyCS = CSharpSourceGeneratorVerifier<CoreWCF.BuildTools.OperationParameterInjectionGenerator>;

namespace CoreWCF.BuildTools.Tests
{
    public class OperationParameterInjectionGeneratorTests
    {
        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task DirectorTests(string attributeNamespace)
        {
            var test = new VerifyCS.Test
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

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@"
namespace MyProject
{
    public partial class IdentityService
    {
        public System.String Echo2(System.String input)
        {
            System.IServiceProvider serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<System.IServiceProvider>();
            if (serviceProvider == null) throw new System.InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = (System.Object)serviceProvider.GetService(typeof(System.Object));
            return Echo2(input, d0);
        }
    }
}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task MultipleParametersInjectedTests(string attributeNamespace)
        {
            var test = new VerifyCS.Test
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

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a, [CoreWCF.Injected] string b) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@"
namespace MyProject
{
    public partial class IdentityService
    {
        public System.String Echo2(System.String input)
        {
            System.IServiceProvider serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<System.IServiceProvider>();
            if (serviceProvider == null) throw new System.InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = (System.Object)serviceProvider.GetService(typeof(System.Object));
            var d1 = (System.String)serviceProvider.GetService(typeof(System.String));
            return Echo2(input, d0, d1);
        }
    }
}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task VoidOperationContractTests(string attributeNamespace)
        {
            var test = new VerifyCS.Test
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

        [{attributeNamespace}.OperationContract]
        void Echo2(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public void Echo2(string input, [CoreWCF.Injected] object a)
        {{
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@"
namespace MyProject
{
    public partial class IdentityService
    {
        public void Echo2(System.String input)
        {
            System.IServiceProvider serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<System.IServiceProvider>();
            if (serviceProvider == null) throw new System.InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = (System.Object)serviceProvider.GetService(typeof(System.Object));
            Echo2(input, d0);
        }
    }
}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ContractAndServiceWithDifferentNamespacesTests(string attributeNamespace)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject.Contracts
{{
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}
}}
",
$@"
namespace MyProject.Implementations
{{
    public partial class IdentityService : MyProject.Contracts.IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_Contracts_IIdentityService_Echo2.cs", SourceText.From(@"
namespace MyProject.Implementations
{
    public partial class IdentityService
    {
        public System.String Echo2(System.String input)
        {
            System.IServiceProvider serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<System.IServiceProvider>();
            if (serviceProvider == null) throw new System.InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = (System.Object)serviceProvider.GetService(typeof(System.Object));
            return Echo2(input, d0);
        }
    }
}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ComposedNamespaceTests(string attributeNamespace)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject.Dummy
{{
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_Dummy_IIdentityService_Echo2.cs", SourceText.From(@"
namespace MyProject.Dummy
{
    public partial class IdentityService
    {
        public System.String Echo2(System.String input)
        {
            System.IServiceProvider serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<System.IServiceProvider>();
            if (serviceProvider == null) throw new System.InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = (System.Object)serviceProvider.GetService(typeof(System.Object));
            return Echo2(input, d0);
        }
    }
}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ContractAndImplementationInSeparateFilesTests(string attributeNamespace)
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject.Dummy
{{
    [{attributeNamespace}.ServiceContract]
    public interface IIdentityService
    {{
        [{attributeNamespace}.OperationContract]
        string Echo(string input);

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}


}}
",
@$"
namespace MyProject.Dummy
{{
    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_Dummy_IIdentityService_Echo2.cs", SourceText.From(@"
namespace MyProject.Dummy
{
    public partial class IdentityService
    {
        public System.String Echo2(System.String input)
        {
            System.IServiceProvider serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<System.IServiceProvider>();
            if (serviceProvider == null) throw new System.InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = (System.Object)serviceProvider.GetService(typeof(System.Object));
            return Echo2(input, d0);
        }
    }
}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("CoreWCF")]
        public async Task InstanceContextModeSingleTests(string attributeNamespace)
        {
            var test = new VerifyCS.Test
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

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    [{attributeNamespace}.ServiceBehavior(InstanceContextMode = {attributeNamespace}.InstanceContextMode.Single)]
    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@"
namespace MyProject
{
    public partial class IdentityService
    {
        public System.String Echo2(System.String input)
        {
            System.IServiceProvider serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<System.IServiceProvider>();
            if (serviceProvider == null) throw new System.InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            using (var scope = serviceProvider.CreateScope())
            {
                var d0 = (System.Object)scope.GetService(typeof(System.Object));
                return Echo2(input, d0);
            }
        }
    }
}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ShouldRaiseCompilationErrorWhenServiceImplementationIsNotPartial(string attributeNamespace)
        {
            var test = new VerifyCS.Test
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

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    public class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources = { },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("COREWCF_0100", DiagnosticSeverity.Error)
                    },
                },
            };

            await test.RunAsync();
        }
    }
}
