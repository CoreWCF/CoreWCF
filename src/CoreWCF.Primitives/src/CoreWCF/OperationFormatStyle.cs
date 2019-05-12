namespace CoreWCF
{
    internal enum OperationFormatStyle
    {
        Document,
        Rpc,
    }

    static class OperationFormatStyleHelper
    {
        static public bool IsDefined(OperationFormatStyle x)
        {
            return
                x == OperationFormatStyle.Document ||
                x == OperationFormatStyle.Rpc ||
                false;
        }
    }
}