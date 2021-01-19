using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    interface IAcceptorSecuritySessionProtocol
    {
        bool ReturnCorrelationState { get; set; }
        SecurityToken GetOutgoingSessionToken();
        void SetOutgoingSessionToken(SecurityToken token);
        void SetSessionTokenAuthenticator(UniqueId sessionId, SecurityTokenAuthenticator sessionTokenAuthenticator, SecurityTokenResolver sessionTokenResolver);
    }
}
