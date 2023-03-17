// using System;
// using System.Net;
// using System.Reflection;
// using System.Threading;
// using Xunit.Sdk;
//
// [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
// public class UseEndpointConnectionLimitAttribute : BeforeAfterTestAttribute
// {
//     private readonly int _limit;
//     private static readonly SemaphoreSlim s_semaphoreSlim = new (1);
//
//     public UseEndpointConnectionLimitAttribute(int limit)
//     {
//         _limit = limit;
//     }
//
//     public override void Before(MethodInfo methodUnderTest)
//     {
//         s_semaphoreSlim.Wait(TimeSpan.FromMinutes(3));
//         ServicePointManager.DefaultConnectionLimit = _limit;
//     }
//
//     public override void After(MethodInfo methodUnderTest)
//     {
//         ServicePointManager.DefaultConnectionLimit = ServicePointManager.DefaultPersistentConnectionLimit;
//         s_semaphoreSlim.Release();
//     }
// }
