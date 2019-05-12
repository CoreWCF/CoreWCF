namespace CoreWCF
{
    public interface IExtensionCollection<T> : System.Collections.Generic.ICollection<IExtension<T>> where T : IExtensibleObject<T>
    {
        E Find<E>();
        System.Collections.ObjectModel.Collection<E> FindAll<E>();
    }
}