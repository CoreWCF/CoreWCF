namespace CoreWCF.Channels
{
    using System.ComponentModel;

    public enum SecurityHeaderLayout
    {
        Strict = 0,
        Lax = 1,
        LaxTimestampFirst = 2,
        LaxTimestampLast = 3
    }

    static class SecurityHeaderLayoutHelper
    {
        public static bool IsDefined(SecurityHeaderLayout value)
        {
            return (value == SecurityHeaderLayout.Lax
            || value == SecurityHeaderLayout.LaxTimestampFirst
            || value == SecurityHeaderLayout.LaxTimestampLast
            || value == SecurityHeaderLayout.Strict);
        }

        public static void Validate(SecurityHeaderLayout value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(SecurityHeaderLayout)));
            }
        }
    }
}
