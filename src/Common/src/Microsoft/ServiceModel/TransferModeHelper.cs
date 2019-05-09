using System;
using System.ComponentModel;
using System.Globalization;

namespace Microsoft.ServiceModel
{
    static class TransferModeHelper
    {
        public static bool IsDefined(TransferMode v)
        {
            return ((v == TransferMode.Buffered) || (v == TransferMode.Streamed) ||
                (v == TransferMode.StreamedRequest) || (v == TransferMode.StreamedResponse));
        }

        public static bool IsRequestStreamed(TransferMode v)
        {
            return ((v == TransferMode.StreamedRequest) || (v == TransferMode.Streamed));
        }

        public static bool IsResponseStreamed(TransferMode v)
        {
            return ((v == TransferMode.StreamedResponse) || (v == TransferMode.Streamed));
        }

        public static void Validate(TransferMode value)
        {
            if (!IsDefined(value))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidEnumArgumentException(nameof(value), (int)value,
                    typeof(TransferMode)));
            }
        }
    }
}