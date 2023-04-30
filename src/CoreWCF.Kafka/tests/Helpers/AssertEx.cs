// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace Xunit;

internal static class AssertEx
{
    public static async Task RetryAsync(Action assertion, int retries = 10, [CallerArgumentExpression("assertion")]string expression = "")
    {
        Exception e = null;
        while (retries > 0)
        {
            try
            {
                assertion.Invoke();
                return;
            }
            catch (Exception exception)
            {
                e = exception;

            }
            await Task.Delay(1000);
            retries--;
        }

        throw new XunitException($"Assertion failed: {expression}", e);
    }
}
