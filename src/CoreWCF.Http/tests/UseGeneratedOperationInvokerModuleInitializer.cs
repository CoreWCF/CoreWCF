// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using System.Runtime.CompilerServices;
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    file sealed class ModuleInitializerAttribute : Attribute { }
}
#endif

namespace CoreWCF.Http.GeneratedOperationInvokers.Tests
{
    internal static class UseGeneratedOperationInvokerModuleInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            AppContext.SetSwitch("CoreWCF.Dispatcher.UseGeneratedOperationInvokers", true);
        }
    }
}
