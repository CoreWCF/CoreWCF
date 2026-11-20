// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    // Regression tests for https://github.com/CoreWCF/CoreWCF/issues/1735.
    //
    // Coalesced timeout cancellation token sources used to remain referenced by the shared
    // CancellationTokenSourceIOThreadTimer until the (potentially multi-hour) send timeout
    // expired, causing large numbers of them to accumulate under sustained load. The fix adds
    // an explicit unregistration path so a source is removed from the timer and disposed as
    // soon as the operation it was created for completes.
    //
    // The relevant types are internal and this test assembly does not have InternalsVisibleTo,
    // so the test drives them through reflection.
    public class RecoverableTimeoutCancellationTokenSourceTests
    {
        private const string AssemblyQualifiedNameFormat = "CoreWCF.Runtime.{0}, CoreWCF.Primitives";

        private static Type GetInternalType(string name)
            => Type.GetType(string.Format(AssemblyQualifiedNameFormat, name), throwOnError: true);

        [Fact]
        public void UnregisterRemovesSourceFromTimerAndDisposesIt()
        {
            Type tokenSourceType = GetInternalType("RecoverableTimeoutCancellationTokenSource");
            Type timeoutTokenSourceType = GetInternalType("TimeoutTokenSource");

            // CancellationToken FromTimeout(int millisecondsTimeout, out RecoverableTimeoutCancellationTokenSource tokenSource)
            MethodInfo fromTimeout = timeoutTokenSourceType.GetMethod(
                "FromTimeout",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(int), tokenSourceType.MakeByRefType() },
                modifiers: null);
            Assert.NotNull(fromTimeout);

            // A large timeout exercises the coalesced-timer path (the one that accumulated).
            var args = new object[] { (int)TimeSpan.FromHours(2).TotalMilliseconds, null };
            _ = fromTimeout.Invoke(null, args);

            object source = args[1];
            Assert.NotNull(source);

            // The source should have been registered with a coalesced timer...
            FieldInfo owningTimerField = tokenSourceType.GetField("_owningTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            object owningTimer = owningTimerField.GetValue(source);
            Assert.NotNull(owningTimer);

            // ...and present in that timer's tracking list.
            FieldInfo listField = owningTimer.GetType().GetField("_cancellationTokenSources", BindingFlags.NonPublic | BindingFlags.Instance);
            var trackedSources = (IList)listField.GetValue(owningTimer);
            Assert.Contains(source, trackedSources.Cast<object>());

            // Simulate completion of the operation the token was created for.
            MethodInfo unregister = tokenSourceType.GetMethod("Unregister", BindingFlags.NonPublic | BindingFlags.Instance);
            unregister.Invoke(source, parameters: null);

            // It must no longer be tracked (so it can be collected instead of lingering)...
            Assert.DoesNotContain(source, trackedSources.Cast<object>());

            // ...and it must have been disposed (accessing Token on a disposed source throws).
            var cts = (CancellationTokenSource)source;
            Assert.Throws<ObjectDisposedException>(() => _ = cts.Token);
        }

        [Fact]
        public void UnregisterIsIdempotent()
        {
            Type tokenSourceType = GetInternalType("RecoverableTimeoutCancellationTokenSource");
            Type timeoutTokenSourceType = GetInternalType("TimeoutTokenSource");

            MethodInfo fromTimeout = timeoutTokenSourceType.GetMethod(
                "FromTimeout",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(int), tokenSourceType.MakeByRefType() },
                modifiers: null);

            var args = new object[] { (int)TimeSpan.FromHours(2).TotalMilliseconds, null };
            _ = fromTimeout.Invoke(null, args);
            object source = args[1];

            MethodInfo unregister = tokenSourceType.GetMethod("Unregister", BindingFlags.NonPublic | BindingFlags.Instance);

            unregister.Invoke(source, parameters: null);
            // Second call must be a safe no-op rather than throwing.
            var exception = Record.Exception(() => unregister.Invoke(source, parameters: null));
            Assert.Null(exception);
        }
    }
}
