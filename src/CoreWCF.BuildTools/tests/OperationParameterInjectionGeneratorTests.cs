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
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{
    public partial class IdentityService
    {
        public string Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
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
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{
    public partial class IdentityService
    {
        public string Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
            var d1 = serviceProvider.GetService<string>();
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
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{
    public partial class IdentityService
    {
        public void Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
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
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject.Implementations
{
    public partial class IdentityService
    {
        public string Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
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
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject.Dummy
{
    public partial class IdentityService
    {
        public string Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
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
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject.Dummy
{
    public partial class IdentityService
    {
        public string Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
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
        public async Task TaskReturnTypeTests(string attributeNamespace)
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
        System.Threading.Tasks.Task Echo2(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public System.Threading.Tasks.Task Echo2(string input, [CoreWCF.Injected] object a) => System.Threading.Tasks.Task.FromResult(input);
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{
    public partial class IdentityService
    {
        public async System.Threading.Tasks.Task Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
            await Echo2(input, d0);
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
        public async Task GenericTaskReturnTypeTests(string attributeNamespace)
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
        System.Threading.Tasks.Task<string> Echo2(string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public System.Threading.Tasks.Task<string> Echo2(string input, [CoreWCF.Injected] object a) => System.Threading.Tasks.Task.FromResult(input);
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{
    public partial class IdentityService
    {
        public async System.Threading.Tasks.Task<string> Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            var d0 = serviceProvider.GetService<object>();
            return await Echo2(input, d0);
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
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{
    public partial class IdentityService
    {
        public string Echo2(string input)
        {
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            using (var scope = serviceProvider.CreateScope())
            {
                var d0 = scope.ServiceProvider.GetService<object>();
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
                DiagnosticsFilter = (diagnostic, _) => diagnostic.Id.StartsWith("COREWCF")
            };

            await test.RunAsync();
        }
    }
}
