// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(input, e0);
        }}
    }}
}}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task LeadingInjectedParameterTests(string attributeNamespace)
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
        string Echo2(string input, int i);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2([CoreWCF.Injected] object a, string input, int i)
        {{
            return input;
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo2(string input, int i)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(d0, input, i);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(e0, input, i);
        }}
    }}
}}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task BetweenRegularParameterTests(string attributeNamespace)
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
        string Echo2(string input, int i);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a, int i)
        {{
            return input;
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo2(string input, int i)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(input, d0, i);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(input, e0, i);
        }}
    }}
}}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel", "internal ", "internal ")]
        [InlineData("System.ServiceModel", "public ", "public ")]
        [InlineData("System.ServiceModel", "", "internal ")]
        [InlineData("CoreWCF", "internal ", "internal ")]
        [InlineData("CoreWCF", "public ", "public ")]
        [InlineData("CoreWCF", "", "internal ")]
        public async Task ServiceImplementationAccessModifiersTests(string attributeNamespace, string accessModifier, string expectedAccessModifier)
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

    {accessModifier}partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From($@"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    {expectedAccessModifier}partial class IdentityService
    {{
        public string Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(input, e0);
        }}
    }}
}}
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    var d1 = scope.ServiceProvider.GetService<string>();
                    return Echo2(input, d0, d1);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            var e1 = serviceProvider.GetService<string>();
            return Echo2(input, e0, e1);
        }}
    }}
}}
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public void Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    Echo2(input, d0);
                    return;
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            Echo2(input, e0);
        }}
    }}
}}
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_Contracts_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject.Implementations
{{
    public partial class IdentityService
    {{
        public string Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(input, e0);
        }}
    }}
}}
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_Dummy_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject.Dummy
{{
    public partial class IdentityService
    {{
        public string Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(input, e0);
        }}
    }}
}}
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_Dummy_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject.Dummy
{{
    public partial class IdentityService
    {{
        public string Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(input, e0);
        }}
    }}
}}
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public async System.Threading.Tasks.Task Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    await Echo2(input, d0);
                    return;
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            await Echo2(input, e0);
        }}
    }}
}}
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public async System.Threading.Tasks.Task<string> Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return await Echo2(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return await Echo2(input, e0);
        }}
    }}
}}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };
            
            await test.RunAsync();
        }

// TODO: make the in-memory assembly compilation works on .NET Framework
#if !NETFRAMEWORK
        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ServiceContractFromOtherAssemblyTests(string attributeNamespace)
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
                        (typeof(OperationParameterInjectionGenerator), "MyProject_IIdentityService_Echo2.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo2(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(input, e0);
        }}
    }}
}}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            test.TestState.AdditionalReferences.Add(BuildInMemoryAssembly());

            await test.RunAsync();

            MetadataReference BuildInMemoryAssembly()
            {
                List<MetadataReference>  references = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("netstandard")).Location),
                    MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location),
                    MetadataReference.CreateFromFile(typeof(System.ServiceModel.ServiceContractAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(CoreWCF.ServiceContractAttribute).Assembly.Location)
                };

                string code = @$"
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
}}
";

                CSharpCompilation compilation = CSharpCompilation.Create(
                    "MyProject",
                    new[]
                    {
                        CSharpSyntaxTree.ParseText(code)
                    },
                    references.ToArray(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

                using MemoryStream memoryStream1 = new MemoryStream();
                EmitResult emitResult1 = compilation.Emit(memoryStream1);
                memoryStream1.Position = 0;

                return MetadataReference.CreateFromStream(memoryStream1);
            }
        }
#endif

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

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ShouldRaiseCompilationErrorWhenOperationContractIsAlreadyImplemented(string attributeNamespace)
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
        public string Echo2(string input) => input;
        public string Echo2(string input, [CoreWCF.Injected] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources = { },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("COREWCF_0102", DiagnosticSeverity.Error)
                    },
                },
                DiagnosticsFilter = (diagnostic, _) => diagnostic.Id.StartsWith("COREWCF")
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ShouldRaiseCompilationErrorWhenParentClassImplementAnInterfaceWithoutServiceContractAttribute(string attributeNamespace)
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
    public interface IIdentityService
    {{
        string Echo(string input);

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
                    GeneratedSources = { },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult("COREWCF_0101", DiagnosticSeverity.Error)
                    },
                },
                DiagnosticsFilter = (diagnostic, _) => diagnostic.Id.StartsWith("COREWCF")
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData("System.ServiceModel")]
        [InlineData("CoreWCF")]
        public async Task ShouldRaiseCompilationErrorWhenParentClassDoesNotImplementOrInheritAnInterfaceWithtServiceContractAttribute(string attributeNamespace)
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
    public partial class IdentityService
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
                        new DiagnosticResult("COREWCF_0101", DiagnosticSeverity.Error)
                    },
                },
                DiagnosticsFilter = (diagnostic, _) => diagnostic.Id.StartsWith("COREWCF")
            };

            await test.RunAsync();
        }
    }
}
