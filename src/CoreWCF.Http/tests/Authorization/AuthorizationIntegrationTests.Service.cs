// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Http.Tests.Authorization;

public partial class AuthorizationIntegrationTests
{
    [ServiceContract]
    internal interface ISecuredService
    {
        [OperationContract]
        string Default(string text);

        [OperationContract]
        string Read(string text);

        [OperationContract]
        Task<string> Write(string text);

        [OperationContract]
        Task<string> Generated(string text);
    }

    public class SecuredServiceHolder
    {
        public bool IsDefaultCalled { get; set; }
        public bool IsReadCalled { get; set; }
        public bool IsWriteCalled { get; set; }
        public bool IsGeneratedCalled { get; set; }
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public partial class SecuredService : ISecuredService
    {
        private readonly SecuredServiceHolder _holder;

        public SecuredService(SecuredServiceHolder holder)
        {
            _holder = holder;
        }

        [Authorize]
        public string Default(string text)
        {
            _holder.IsDefaultCalled = true;
            return text;
        }

        [Authorize(Policy = Policies.Read)]
        public string Read(string text)
        {
            _holder.IsReadCalled = true;
            return text;
        }

        [Authorize(Policy = Policies.Write)]
        public Task<string> Write(string text)
        {
            _holder.IsWriteCalled = true;
            return Task.FromResult(text);
        }

        [Authorize(Policy = Policies.Write)]
        public Task<string> Generated(string text, [Injected] HttpContext httpContext)
        {
            _holder.IsGeneratedCalled = true;
            return Task.FromResult(text);
        }
    }

    private static class Policies
    {
        public const string Read = nameof(Read);
        public const string Write = nameof(Write);
    }

    private static class DefinedScopeValues
    {
        public const string Read = nameof(Read);
        public const string Write = nameof(Write);
    }
}
