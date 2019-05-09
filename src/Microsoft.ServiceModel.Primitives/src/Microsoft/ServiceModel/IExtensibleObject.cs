namespace Microsoft.ServiceModel
{
    public interface IExtensibleObject<T> where T : IExtensibleObject<T>
    {
        IExtensionCollection<T> Extensions { get; }
    }
}