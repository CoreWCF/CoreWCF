using ServiceContract;

namespace Services
{
    class Echo : ServiceContract.IEcho
    {       
        string IEcho.Echo(string inputValue)
        {
            return inputValue;
        }
    }
}
