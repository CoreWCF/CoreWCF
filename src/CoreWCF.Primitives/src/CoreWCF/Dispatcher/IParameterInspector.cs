namespace CoreWCF.Dispatcher
{
    public interface IParameterInspector
    {
        object BeforeCall(string operationName, object[] inputs);
        void AfterCall(string operationName, object[] outputs, object returnValue, object correlationState);
    }
}