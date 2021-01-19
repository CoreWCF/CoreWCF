using System.ComponentModel;

namespace CoreWCF.IdentityModel.Selectors
{
    public enum AudienceUriMode
    {
        Never,
        Always,
        BearerKeyOnly
    }

    public static class AudienceUriModeValidationHelper
    {
        public static bool IsDefined(AudienceUriMode validationMode)
        {
            return validationMode == AudienceUriMode.Never
                || validationMode == AudienceUriMode.Always
                || validationMode == AudienceUriMode.BearerKeyOnly;
        }

        internal static void Validate(AudienceUriMode value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException("value", (int)value,
                    typeof(AudienceUriMode)));
            }
        }
    }
}
