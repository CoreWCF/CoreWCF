namespace CoreWCF.Channels
{
    // This interface needs to be implemented by requests that need to save 
    // the RequestReplyCorrelatorKey into the request during RequestReplyCorrelator.Add
    // operation. 
    internal interface ICorrelatorKey
    {
        RequestReplyCorrelator.Key RequestCorrelatorKey { get; set; }
    }
}