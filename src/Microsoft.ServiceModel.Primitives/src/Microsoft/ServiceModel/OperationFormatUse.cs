namespace Microsoft.ServiceModel
{
    internal enum OperationFormatUse
    {
        Literal,
        Encoded,
    }

    static class OperationFormatUseHelper
    {
        static public bool IsDefined(OperationFormatUse x)
        {
            return
                x == OperationFormatUse.Literal ||
                x == OperationFormatUse.Encoded ||
                false;
        }
    }
}