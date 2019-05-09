using System.Reflection;

namespace Microsoft.ServiceModel.Dispatcher
{
    public interface IClientOperationSelector
    {
        bool AreParametersRequiredForSelection { get; }
        string SelectOperation(MethodBase method, object[] parameters);
    }
}