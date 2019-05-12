namespace CoreWCF.Channels
{
    public enum ReceiveContextState
    {
        Received,
        Completing,
        Completed,
        Abandoning,
        Abandoned,
        Faulted
    }
}