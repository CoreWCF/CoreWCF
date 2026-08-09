// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using CoreWCF.Description;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    /// <summary>
    /// Pins the resolution order of the source-generated serializer switch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This default matters more than most: it decides which serializer implementation a released
    /// application uses. A silent flip would change wire behaviour on a package upgrade, so the
    /// order is asserted rather than assumed.
    /// </para>
    /// <para>
    /// Tests the pure decision function rather than the AppContext reads, deliberately. AppContext
    /// switches are process-global and cannot be unset, and
    /// <c>RuntimeFeature.IsDynamicCodeSupported</c> is read into <c>static readonly</c> fields
    /// elsewhere in CoreWCF - so a test that set it could silently change unrelated behaviour for
    /// the rest of the run, in a suite that runs concurrently.
    /// </para>
    /// <para>
    /// Reaches the internal type by reflection so no InternalsVisibleTo is needed, following
    /// NTAuthenticationNet8EncryptTests.
    /// </para>
    /// </remarks>
    public class GeneratedSerializerSwitchTests
    {
        private static bool Decide(bool? explicitSetting, bool? isDynamicCodeSupported)
        {
            Type type = typeof(DataContractSerializerOperationBehavior).Assembly
                .GetType("CoreWCF.Runtime.Serialization.GeneratedSerializerSwitch", throwOnError: true);

            MethodInfo decide = type.GetMethod("Decide", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(decide);

            return (bool)decide.Invoke(null, new object[] { explicitSetting, isDynamicCodeSupported });
        }

        [Theory]
        // An explicit setting always wins, whatever the runtime looks like.
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, null, true)]
        // Including opting out under Native AOT, where the reflection path may then fail. The
        // caller is entitled to make that choice.
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, null, false)]
        // With no explicit setting: on only where dynamic code is unavailable, because there the
        // reflection-based serializer is the broken one.
        [InlineData(null, false, true)]
        // Otherwise off - an ordinary application must not change serializer implementation just
        // because it upgraded CoreWCF.
        [InlineData(null, true, false)]
        [InlineData(null, null, false)]
        public void Decide_FollowsTheDocumentedOrder(bool? explicitSetting, bool? isDynamicCodeSupported, bool expected)
        {
            Assert.Equal(expected, Decide(explicitSetting, isDynamicCodeSupported));
        }

        [Fact]
        public void CreateAotSerializer_ReturnsNullByDefault()
        {
            // The stock behavior never produces a generated serializer, so the operation formatter
            // always falls back to the reflection path unless a derived behavior opts in. This is
            // what makes the whole feature inert until someone asks for it.
            DataContractSerializerOperationBehavior behavior = new DataContractSerializerOperationBehavior(null);

            Assert.Null(behavior.CreateAotSerializer(typeof(string), null, null, null));
        }
    }
}
