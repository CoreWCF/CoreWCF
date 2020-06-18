using CoreWCF;
using ServiceContract;

namespace Services
{
    [ServiceBehavior]
    public class ServiceContractNamespace_784756_XmlCharacters_Service : IServiceContractNamespace_784756_XmlCharacters_Service
    {
        string IServiceContractNamespace_784756_XmlCharacters_Service.Method1(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractNamespace_784756_WhiteSpace_Service : IServiceContractNamespace_784756_WhiteSpace_Service
    {
        string IServiceContractNamespace_784756_WhiteSpace_Service.Method2(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractNamespace_784756_XMLEncoded_Service : IServiceContractNamespace_784756_XMLEncoded_Service
    {
        string IServiceContractNamespace_784756_XMLEncoded_Service.Method3(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractNamespace_784756_NonAlphaCharacters_Service : IServiceContractNamespace_784756_NonAlphaCharacters_Service
    {
        string IServiceContractNamespace_784756_NonAlphaCharacters_Service.Method4(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractNamespace_784756_LocalizedCharacters_Service : IServiceContractNamespace_784756_LocalizedCharacters_Service
    {
        string IServiceContractNamespace_784756_LocalizedCharacters_Service.Method5(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractNamespace_784756_SurrogateCharacters_Service : IServiceContractNamespace_784756_SurrogateCharacters_Service
    {      
        string IServiceContractNamespace_784756_SurrogateCharacters_Service.Method6(string input)
        {            
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractNamespace_784756_XMLReservedCharacters_Service : IServiceContractNamespace_784756_XMLReservedCharacters_Service
    {
        string IServiceContractNamespace_784756_XMLReservedCharacters_Service.Method7(string input)
        {
            return input;
        }
    }

    [ServiceBehavior]
    public class ServiceContractNamespace_784756_URI_Service : IServiceContractNamespace_784756_URI_Service
    {
        string IServiceContractNamespace_784756_URI_Service.Method8(string input)
        {
            return input;
        }
    }
}