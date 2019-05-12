using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CoreWCF.Security
{
    public enum UserNamePasswordValidationMode
    {
        Windows,
        MembershipProvider,
        Custom
    }

    static class UserNamePasswordValidationModeHelper
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
                    typeof(UserNamePasswordValidationMode)));
            }
        }

    }
}
