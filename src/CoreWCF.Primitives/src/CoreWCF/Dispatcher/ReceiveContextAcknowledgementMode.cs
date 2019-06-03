namespace CoreWCF.Dispatcher
{
    internal enum ReceiveContextAcknowledgementMode
    {
        AutoAcknowledgeOnReceive = 0,
        AutoAcknowledgeOnRPCComplete = 1,
        ManualAcknowledgement = 2
    }
}