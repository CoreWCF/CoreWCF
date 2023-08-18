// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Http.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using ServiceContract;

namespace Services;

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
internal class SinglePolicyOnMethodSecuredService : ISecuredService
{
    [Authorize(Policy = AuthorizationUtils.Policies.Read)]
    public string Echo(string text) => text;
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
[Authorize(Policy = AuthorizationUtils.Policies.Read)]
internal class MultiplePoliciesOnClassAndMethodSecuredService : ISecuredService
{
    [Authorize(Policy = AuthorizationUtils.Policies.Write)]
    public string Echo(string text) => text;
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
[Authorize(Policy = AuthorizationUtils.Policies.Read)]
internal class SinglePolicyOnClassSecuredService : ISecuredService
{
    public string Echo(string text) => text;
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
internal class DefaultPolicySecuredService : ISecuredService
{
    [Authorize]
    public string Echo(string text) => text;
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
internal class FallbackPolicySecuredService : ISecuredService
{
    public string Echo(string text) => text;
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
internal class MultiplePoliciesOnMethodSecuredService : ISecuredService
{
    [Authorize(Policy = AuthorizationUtils.Policies.Read)]
    [Authorize(Policy = AuthorizationUtils.Policies.Write)]
    public string Echo(string text) => text;
}
