using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class ServiceContractName_784749_XmlCharacters_Service : IServiceContractName_784749_XmlCharacters_Service
    {
        string IServiceContractName_784749_XmlCharacters_Service.Method1(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractName_784749_WhiteSpace_Service : IServiceContractName_784749_WhiteSpace_Service
    {
        string IServiceContractName_784749_WhiteSpace_Service.Method2(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractName_784749_XMLEncoded_Service : IServiceContractName_784749_XMLEncoded_Service
    {
        string IServiceContractName_784749_XMLEncoded_Service.Method3(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractName_784749_NonAlphaCharacters_Service : IServiceContractName_784749_NonAlphaCharacters_Service
    {
        string IServiceContractName_784749_NonAlphaCharacters_Service.Method4(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractName_784749_LocalizedCharacters_Service : IServiceContractName_784749_LocalizedCharacters_Service
    {
        string IServiceContractName_784749_LocalizedCharacters_Service.Method5(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractName_784749_SurrogateCharacters_Service : IServiceContractName_784749_SurrogateCharacters_Service
    {
        string IServiceContractName_784749_SurrogateCharacters_Service.Method6(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractName_784749_XMLReservedCharacters_Service : IServiceContractName_784749_XMLReservedCharacters_Service
    {
        string IServiceContractName_784749_XMLReservedCharacters_Service.Method7(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractName_784749_URI_Service : IServiceContractName_784749_URI_Service
    {
        string IServiceContractName_784749_URI_Service.Method8(string input)
        {
            return input;
        }
    }
}
