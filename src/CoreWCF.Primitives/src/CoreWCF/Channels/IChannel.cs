namespace CoreWCF.Channels
{
    public interface IChannel : ICommunicationObject
    {
        T GetProperty<T>() where T : class;
    }
}