// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace CoreWCF.Security
{
    public enum UserNamePasswordValidationMode
    {
        Windows,
        MembershipProvider,
        Custom
    }

    internal static class UserNamePasswordValidationModeHelper
    {
        public static bool IsDefined(UserNamePasswordValidationMode validationMode)
        {
            return validationMode == UserNamePasswordValidationMode.Windows
                || validationMode == UserNamePasswordValidationMode.MembershipProvider
                || validationMode == UserNamePasswordValidationMode.Custom;
        }

        public static void Validate(UserNamePasswordValidationMode value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(UserNamePasswordValidationMode)));
            }
        }
    }
}
