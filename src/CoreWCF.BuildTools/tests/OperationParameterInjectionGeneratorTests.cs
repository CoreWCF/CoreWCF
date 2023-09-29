// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Xunit;

using VerifyGenerator = CSharpGeneratorVerifier<CoreWCF.BuildTools.OperationParameterInjectionGenerator>;

namespace CoreWCF.BuildTools.Tests
{
    public class OperationParameterInjectionGeneratorTests
    {
        private const string SSMNamespace = "System.ServiceModel";
        private const string CoreWCFNamespace = "CoreWCF";

        private const string CoreWCFInjectedAttribute = "CoreWCF.Injected";
        private const string MVCFromServicesAttribute = "Microsoft.AspNetCore.Mvc.FromServices";

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { SSMNamespace, CoreWCFInjectedAttribute };
            yield return new object[] { SSMNamespace, MVCFromServicesAttribute };
            yield return new object[] { CoreWCFNamespace, CoreWCFInjectedAttribute };
            yield return new object[] { CoreWCFNamespace, MVCFromServicesAttribute };
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task BasicTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task BasicTestsWithCustomAttributeContainingArrayParameter(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject
{{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class DummyAttribute : System.Attribute
    {{
        public DummyAttribute(string[] args)
        {{
        }}
    }}

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
        [Dummy(new [] {{""A""}})]
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        [MyProject.DummyAttribute(new [] {{""A""}})]
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
        [MemberData(nameof(GetTestVariations))]
        public async Task BasicTestsWithoutNamespace(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
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
    public string Echo2(string input, [{attribute}] object a) => input;
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
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
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task BasicTestsWithMatchingSignatureButNonMatchingParameterNames(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string otherName, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task AttributeTypedConstantsSupport(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        [CoreWCF.OpenApi.Attributes.OpenApiResponse(ContentTypes = new[] {{ ""application/json"", ""text/xml"" }}, Description = ""Success"", StatusCode = System.Net.HttpStatusCode.OK, Type = typeof(string))]
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        [CoreWCF.OpenApi.Attributes.OpenApiResponseAttribute(ContentTypes = new [] {{""application/json"", ""text/xml""}}, Description = ""Success"", StatusCode = System.Net.HttpStatusCode.OK, Type = typeof(string))]
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
        [MemberData(nameof(GetTestVariations))]
        public async Task MethodOverloadsTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public int Echo(int input, [{attribute}] object a) => input;
        public string Echo(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public int Echo(int input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo(input, e0);
        }}
    }}
}}
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo(input, e0);
        }}
    }}
}}
", Encoding.UTF8, SourceHashAlgorithm.Sha256))
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task BasicTestsWithAuthorizeAttribute(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = ""MyPolicy"")]
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AuthorizeAttribute(Policy = ""MyPolicy"")]
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
        [MemberData(nameof(GetTestVariations))]
        public async Task BasicTestsWithAuthorizeAttributeResolveConstantValue(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject
{{
    public static class Constants
    {{
        public const string MyPolicy = ""MyPolicy"";
    }}

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
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = Constants.MyPolicy)]
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        [Microsoft.AspNetCore.Authorization.AuthorizeAttribute(Policy = ""MyPolicy"")]
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
        [MemberData(nameof(GetTestVariations))]
        public async Task BasicTestsWithAuthorizeRoleAttribute(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject
{{
    public static class Constants
    {{
        public const string MyPolicy = ""MyPolicy"";
    }}

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
        [CoreWCF.AuthorizeRole(""Role1"", ""Role2"")]
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        [CoreWCF.AuthorizeRoleAttribute(new [] {{""Role1"", ""Role2""}})]
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
        [MemberData(nameof(GetTestVariations))]
        public async Task MultipleOperations(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo(string input, [{attribute}] object a) => input;
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo(string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo(input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo(input, e0);
        }}
    }}
}}
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

        [Fact]
        public async Task FromServicesAttributeShouldWorkAsUsualForMvcControllers()
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject
{{
    public class HomeController : Microsoft.AspNetCore.Mvc.Controller
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [{MVCFromServicesAttribute}] object a) => input;
    }}
}}
"
                    }
                },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task FromServicesAttributeShouldWorkAsUsualForMvcControllersWhenCombinedWithInjected()
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject
{{
    public class HomeController : Microsoft.AspNetCore.Mvc.Controller
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [{MVCFromServicesAttribute}] object a, [{CoreWCFInjectedAttribute}] object b) => input;
    }}
}}
"
                    }
                },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task FromServicesAttributeShouldWorkAsUsualForMVCControllers_ControllerInheritance()
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
namespace MyProject
{{
    public class MyBaseController : Microsoft.AspNetCore.Mvc.Controller {{ }}
    public class HomeController : MyBaseController
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [{MVCFromServicesAttribute}] object a) => input;
    }}
}}
"
                    }
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task HttpContextTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a, [{attribute}] Microsoft.AspNetCore.Http.HttpContext b) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
            var httpContext = (CoreWCF.OperationContext.Current.RequestContext.RequestMessage.Properties.TryGetValue(""Microsoft.AspNetCore.Http.HttpContext"", out var @object)
                && @object is Microsoft.AspNetCore.Http.HttpContext context)
                ? context
                : null;
            if (httpContext == null) throw new InvalidOperationException(""Missing HttpContext in RequestMessage properties"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    var d1 = httpContext;
                    return Echo2(input, d0, d1);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            var e1 = httpContext;
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
        [MemberData(nameof(GetTestVariations))]
        public async Task HttpRequestTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a, [{attribute}] Microsoft.AspNetCore.Http.HttpRequest b) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
            var httpContext = (CoreWCF.OperationContext.Current.RequestContext.RequestMessage.Properties.TryGetValue(""Microsoft.AspNetCore.Http.HttpContext"", out var @object)
                && @object is Microsoft.AspNetCore.Http.HttpContext context)
                ? context
                : null;
            if (httpContext == null) throw new InvalidOperationException(""Missing HttpContext in RequestMessage properties"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    var d1 = httpContext.Request;
                    return Echo2(input, d0, d1);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            var e1 = httpContext.Request;
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
        [MemberData(nameof(GetTestVariations))]
        public async Task HttpResponseTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a, [{attribute}] Microsoft.AspNetCore.Http.HttpResponse b) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
            var httpContext = (CoreWCF.OperationContext.Current.RequestContext.RequestMessage.Properties.TryGetValue(""Microsoft.AspNetCore.Http.HttpContext"", out var @object)
                && @object is Microsoft.AspNetCore.Http.HttpContext context)
                ? context
                : null;
            if (httpContext == null) throw new InvalidOperationException(""Missing HttpContext in RequestMessage properties"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    var d1 = httpContext.Response;
                    return Echo2(input, d0, d1);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            var e1 = httpContext.Response;
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
        [MemberData(nameof(GetTestVariations))]
        public async Task NestedClassesMultipleLevelsTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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

    public partial class ContainerA
    {{
        public partial class ContainerB
        {{
            public partial class ContainerC
            {{
                public partial class IdentityService : IIdentityService
                {{
                    public string Echo(string input) => input;
                    public string Echo2(string input, [{attribute}] object a) => input;
                }}
            }}
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class ContainerA
    {{
        public partial class ContainerB
        {{
            public partial class ContainerC
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
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "public ", "public ", "public ", "public ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "public ", "internal ", "public ", "internal ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "public ", "protected ", "public ", "protected ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "public ", "private ", "public ", "private ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "public ", "", "public ", "private ")]

        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "internal ", "public ", "internal ", "public ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "internal ", "internal ", "internal ", "internal ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "internal ", "protected ", "internal ", "protected ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "internal ", "private ", "internal ", "private ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "internal ", "", "internal ", "private ")]

        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "public ", "public ", "public ", "public ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "public ", "internal ", "public ", "internal ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "public ", "protected ", "public ", "protected ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "public ", "private ", "public ", "private ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "public ", "", "public ", "private ")]

        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "internal ", "public ", "internal ", "public ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "internal ", "internal ", "internal ", "internal ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "internal ", "protected ", "internal ", "protected ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "internal ", "private ", "internal ", "private ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "internal ", "", "internal ", "private ")]

        [InlineData(SSMNamespace, MVCFromServicesAttribute, "public ", "public ", "public ", "public ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "public ", "internal ", "public ", "internal ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "public ", "protected ", "public ", "protected ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "public ", "private ", "public ", "private ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "public ", "", "public ", "private ")]

        [InlineData(SSMNamespace, MVCFromServicesAttribute, "internal ", "public ", "internal ", "public ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "internal ", "internal ", "internal ", "internal ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "internal ", "protected ", "internal ", "protected ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "internal ", "private ", "internal ", "private ")]
        [InlineData(SSMNamespace, MVCFromServicesAttribute, "internal ", "", "internal ", "private ")]

        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "public ", "public ", "public ", "public ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "public ", "internal ", "public ", "internal ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "public ", "protected ", "public ", "protected ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "public ", "private ", "public ", "private ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "public ", "", "public ", "private ")]

        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "internal ", "public ", "internal ", "public ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "internal ", "internal ", "internal ", "internal ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "internal ", "protected ", "internal ", "protected ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "internal ", "private ", "internal ", "private ")]
        [InlineData(CoreWCFNamespace, MVCFromServicesAttribute, "internal ", "", "internal ", "private ")]
        public async Task NestedClassContainingTypeHierarchyAccessModifiersTests(string attributeNamespace, string attribute, string containerModifiers, string implementationModifiers, string expectedContainerModifiers, string expectedImplementationModifiers)
        {
            var test = new VerifyGenerator.Test
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

    {containerModifiers}partial class Container
    {{
        {implementationModifiers}partial class IdentityService : IIdentityService
        {{
            public string Echo(string input) => input;
            public string Echo2(string input, [{attribute}] object a) => input;
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    {expectedContainerModifiers}partial class Container
    {{
        {expectedImplementationModifiers}partial class IdentityService
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
}}
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task RefParameterTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        string Echo2(ref string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(ref string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo2(ref string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(ref input, d0);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(ref input, e0);
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
        [MemberData(nameof(GetTestVariations))]
        public async Task OutParameterTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        string Echo2(out string input);
    }}

    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2([{attribute}] object a, out string input)
        {{
            input = null;
            return input;
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
namespace MyProject
{{
    public partial class IdentityService
    {{
        public string Echo2(out string input)
        {{
            var serviceProvider = CoreWCF.OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>();
            if (serviceProvider == null) throw new InvalidOperationException(""Missing IServiceProvider in InstanceContext extensions"");
            if (CoreWCF.OperationContext.Current.InstanceContext.IsSingleton)
            {{
                using (var scope = serviceProvider.CreateScope())
                {{
                    var d0 = scope.ServiceProvider.GetService<object>();
                    return Echo2(d0, out input);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            return Echo2(e0, out input);
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
        [MemberData(nameof(GetTestVariations))]
        public async Task LeadingInjectedParameterTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2([{attribute}] object a, string input, int i)
        {{
            return input;
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task BetweenRegularParameterTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a, int i)
        {{
            return input;
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "public ", "public ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "internal ", "internal ")]
        [InlineData(SSMNamespace, CoreWCFInjectedAttribute, "", "internal ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "public ", "public ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "internal ", "internal ")]
        [InlineData(CoreWCFNamespace, CoreWCFInjectedAttribute, "", "internal ")]
        public async Task ServiceImplementationContainingTypeAccessModifiersTests(string attributeNamespace, string attribute, string accessModifier, string expectedAccessModifier)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From($@"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task MultipleParametersInjectedTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a, [{attribute}] Guid b) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
                    var d1 = scope.ServiceProvider.GetService<Guid>();
                    return Echo2(input, d0, d1);
                }}
            }}
            var e0 = serviceProvider.GetService<object>();
            var e1 = serviceProvider.GetService<Guid>();
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
        [MemberData(nameof(GetTestVariations))]
        public async Task VoidOperationContractTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public void Echo2(string input, [{attribute}] object a)
        {{
        }}
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task ContractAndServiceWithDifferentNamespacesTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task ContractAndServiceWithDifferentNamespacesTests2(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
public partial class IdentityService : MyProject.Contracts.IIdentityService
{{
    public string Echo(string input) => input;
    public string Echo2(string input, [{attribute}] object a) => input;
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
using System;
using Microsoft.Extensions.DependencyInjection;
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
", Encoding.UTF8, SourceHashAlgorithm.Sha256)),
                    },
                },
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task ContractAndServiceWithDifferentNamespacesTests3(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
            {
                TestState =
                {
                    Sources =
                    {
@$"
[{attributeNamespace}.ServiceContract]
public interface IIdentityService
{{
    [{attributeNamespace}.OperationContract]
    string Echo(string input);

    [{attributeNamespace}.OperationContract]
    string Echo2(string input);
}}
",
$@"
namespace MyProject.Implementations
{{
    public partial class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task ComposedNamespaceTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task ContractAndImplementationInSeparateFilesTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task TaskReturnTypeTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public System.Threading.Tasks.Task Echo2(string input, [{attribute}] object a) => System.Threading.Tasks.Task.FromResult(input);
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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
        [MemberData(nameof(GetTestVariations))]
        public async Task GenericTaskReturnTypeTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public System.Threading.Tasks.Task<string> Echo2(string input, [{attribute}] object a) => System.Threading.Tasks.Task.FromResult(input);
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task ServiceContractFromOtherAssemblyTests(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    GeneratedSources =
                    {
                        (typeof(OperationParameterInjectionGenerator), "OperationParameterInjection.g.cs", SourceText.From(@$"
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

            test.TestState.AdditionalReferences.Add(await BuildInMemoryAssembly());

            await test.RunAsync();

            async Task<MetadataReference> BuildInMemoryAssembly()
            {
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

                var references =
                    await ReferenceAssembliesHelper.Default.Value.ResolveAsync(LanguageNames.CSharp,
                        CancellationToken.None);

                CSharpCompilation compilation = CSharpCompilation.Create(
                    "MyProject",
                    new[]
                    {
                        CSharpSyntaxTree.ParseText(code)
                    },
                    references.Union(new[] { MetadataReference.CreateFromFile(typeof(CoreWCF.ServiceContractAttribute).Assembly.Location) }),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

                using MemoryStream memoryStream1 = new();
                EmitResult emitResult1 = compilation.Emit(memoryStream1);
                memoryStream1.Position = 0;

                return MetadataReference.CreateFromStream(memoryStream1);
            }
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task ShouldRaiseCompilationErrorWhenServiceImplementationIsNotPartial(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(DiagnosticDescriptors.OperationParameterInjectionGenerator_01XX.ParentClassShouldBePartialError)
                            .WithDefaultPath("/0/Test0.cs")
                            .WithSpan(14, 18, 14, 33)
                            .WithArguments("IdentityService", "Echo2")
                    },
                }
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task ShouldRaiseCompilationErrorWhenParentClassOfServiceImplementationIsNotPartial(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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

    public class ContainerA
    {{
        public partial class IdentityService : IIdentityService
        {{
            public string Echo(string input) => input;
            public string Echo2(string input, [{attribute}] object a) => input;
        }}
    }}
}}
"
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(DiagnosticDescriptors.OperationParameterInjectionGenerator_01XX.ParentClassShouldBePartialError)
                            .WithDefaultPath("/0/Test0.cs")
                            .WithSpan(14, 18, 14, 28)
                            .WithArguments("ContainerA", "Echo2")
                    }
                }
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task ShouldRaiseCompilationErrorWhenGrandParentClassOfServiceImplementationIsNotPartial(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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

    public class ContainerA
    {{
        public partial class ContainerB
        {{
            public partial class IdentityService : IIdentityService
            {{
                public string Echo(string input) => input;
                public string Echo2(string input, [{attribute}] object a) => input;
            }}
        }}
    }}
}}
"
                    },
                    ExpectedDiagnostics =
                    {
                        new DiagnosticResult(DiagnosticDescriptors.OperationParameterInjectionGenerator_01XX.ParentClassShouldBePartialError)
                            .WithSpan(14, 18, 14, 28)
                            .WithArguments("ContainerA", "Echo2")
                            .WithDefaultPath("/0/Test0.cs"),
                    },
                }
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task ShouldNotRaiseCompilationErrorWhenServiceImplementationIsNotPartialButImplementedInterfaceIsNotAServiceContract(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        [{attributeNamespace}.OperationContract]
        string Echo(string input);

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    public class IdentityService : IIdentityService
    {{
        public string Echo(string input) => input;
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    }
                }
            };

            await test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public async Task ShouldNotRaiseCompilationErrorWhenParentClassOfServiceImplementationIsNotPartialButImplementedInterfaceIsNotAServiceContract(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        [{attributeNamespace}.OperationContract]
        string Echo(string input);

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    public class ContainerA
    {{
        public partial class IdentityService : IIdentityService
        {{
            public string Echo(string input) => input;
            public string Echo2(string input, [{attribute}] object a) => input;
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
        [MemberData(nameof(GetTestVariations))]
        public async Task ShouldNotRaiseCompilationErrorWhenGrandParentClassOfServiceImplementationIsNotPartialButImplementedInterfaceIsNotAServiceContract(string attributeNamespace, string attribute)
        {
            var test = new VerifyGenerator.Test
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
        [{attributeNamespace}.OperationContract]
        string Echo(string input);

        [{attributeNamespace}.OperationContract]
        string Echo2(string input);
    }}

    public class ContainerA
    {{
        public partial class ContainerB
        {{
            public partial class IdentityService : IIdentityService
            {{
                public string Echo(string input) => input;
                public string Echo2(string input, [{attribute}] object a) => input;
            }}
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
        [InlineData(CoreWCFInjectedAttribute)]
        [InlineData(MVCFromServicesAttribute)]
        public async Task ShouldNotRaiseCompilationErrorWhenParentClassImplementAnInterfaceWithoutServiceContractAttribute(string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    }
                }
            };

            await test.RunAsync();
        }

        [Theory]
        [InlineData(CoreWCFInjectedAttribute)]
        [InlineData(MVCFromServicesAttribute)]
        public async Task ShouldNotRaiseCompilationErrorWhenParentClassDoesNotImplementOrInheritAnInterfaceWithServiceContractAttribute(string attribute)
        {
            var test = new VerifyGenerator.Test
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
        public string Echo2(string input, [{attribute}] object a) => input;
    }}
}}
"
                    }
                }
            };

            await test.RunAsync();
        }
    }
}
