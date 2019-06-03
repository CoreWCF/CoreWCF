using System.Reflection;

namespace CoreWCF.Dispatcher
{
    public interface IClientOperationSelector
    {
        bool AreParametersRequiredForSelection { get; }
        string SelectOperation(MethodBase method, object[] parameters);
    }
}