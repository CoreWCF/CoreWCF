// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Runtime.Serialization
{
    /// <summary>
    /// Decides whether CoreWCF uses source-generated serializers in place of the reflection-based
    /// <see cref="System.Runtime.Serialization.DataContractSerializer"/>.
    /// </summary>
    /// <remarks>
    /// The generated path is an optimization with a fallback, not a replacement: when this is
    /// disabled, <c>CreateAotSerializer</c> returns null and the operation formatter behaves exactly
    /// as it always has. Resolution order mirrors
    /// <see cref="CoreWCF.Dispatcher.OperationInvokerBehavior"/>, which gates the operation-invoker
    /// generator the same way.
    /// </remarks>
    internal static class GeneratedSerializerSwitch
    {
        internal const string SwitchKey = "CoreWCF.Serialization.UseGeneratedDataContractSerializers";

        // Set by the host when dynamic code is unavailable - notably under Native AOT.
        private const string IsDynamicCodeSupportedKey =
            "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported";

        private static readonly Lazy<bool> s_enabled = new Lazy<bool>(ResolveEnabled);

        /// <summary>
        /// Whether generated serializers should be used. Resolved once per process on first read.
        /// </summary>
        internal static bool Enabled => s_enabled.Value;

        private static bool ResolveEnabled()
        {
            bool? explicitSetting = AppContext.TryGetSwitch(SwitchKey, out bool value)
                ? value
                : (bool?)null;

            bool? isDynamicCodeSupported = AppContext.TryGetSwitch(IsDynamicCodeSupportedKey, out bool dynamicCode)
                ? dynamicCode
                : (bool?)null;

            return Decide(explicitSetting, isDynamicCodeSupported);
        }

        /// <summary>
        /// The decision itself, as a pure function of its two inputs.
        /// </summary>
        /// <remarks>
        /// Separated from the AppContext reads so it can be tested without mutating process-global
        /// state. <c>IsDynamicCodeSupported</c> in particular is read into <c>static readonly</c>
        /// fields elsewhere in CoreWCF (see <c>Dispatcher/InvokerUtil</c> and
        /// <c>Dispatcher/DispatchOperationRuntimeHelpers</c>), so a test that set it could silently
        /// change unrelated behaviour for the rest of the run.
        /// </remarks>
        internal static bool Decide(bool? explicitSetting, bool? isDynamicCodeSupported)
        {
            // An explicit opt-in or opt-out always wins - including opting out under Native AOT,
            // where the reflection path may then fail. That is the caller's call to make.
            if (explicitSetting.HasValue)
            {
                return explicitSetting.Value;
            }

            // Otherwise on only where the reflection path is the broken one. Without dynamic code
            // DataContractSerializer cannot emit its IL, and the trimmer may already have removed
            // the members it wants to reflect over, so the generated path is the only one that can
            // be correct. Everywhere else the default stays off: a released application should not
            // change serializer implementation merely because it upgraded CoreWCF.
            return isDynamicCodeSupported.HasValue && !isDynamicCodeSupported.Value;
        }
    }
}
