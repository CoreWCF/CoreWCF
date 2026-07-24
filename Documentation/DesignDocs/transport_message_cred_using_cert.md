# Objective

The objective of this document is to provide details on how TransportMessageCredential security feature works int CoreWCF/WCF Client to provide confidentiality , integrity and authenticity for the messages that are transmitted over the wire between WCF client and CoreWCF service.

# Why TransportMessageCredential Security ?

The collection of authentication procedures that can be employed to guarantee that the client is authenticated to the service is restricted to what the HTTPS transport supports.

To get around this restriction, CoreWCF provides a security mode called TransportWithMessageCredential. When this security mode is set up, service authentication , message confidentiality and integrity are ensured using HTTPS transport security. For client authentication, the client credential is entered into the message header to complete the client authentication. This allows you to use various credential types (UserName/Password, Certificate, Windows Auth) that is supported by the message security mode for the client authentication while keeping the performance benefit of transport security mode.

In the remainder of the document, we will describe the Certificate client credential type. For username/password or Windows auth client credential type, the process is the same.

# Assumption

We assume that the reader of this document should have basic concept of

* [SOAP](https://en.wikipedia.org/wiki/SOAP)
* [Asymmetric Key Cryptography](https://en.wikipedia.org/wiki/Public-key_cryptography)
* [Symmetric Key Cryptography](https://en.wikipedia.org/wiki/Symmetric-key_algorithm)
* [XML Signing](https://en.wikipedia.org/wiki/XML_Signature)

# Design Details

The design is documented into 2 sections.

* We have provided enough information **(Details Overview)** in the first section without going into code or specifics about the different CoreWCF components. Those who wish to have a thorough understanding of the security principles used in the framework without having to go through all of CoreWCF's internal components may find this to be helpful.
* In the second section, **(Implementation Details)**, we will discuss the main COREWCF components and how they interact, as well as how all security principles are put into practice to accomplish the goal outlined in the first section. (This is specifically designed for developers who may want to contribute to this framework in the future.)

## Details Overview

### Initial Handshake (Phase 1)

![Handshake](/Documentation/DesignDocs/TransportMessageCredential/Diagram_authentication_phase1.png?raw=true)

The main goal of the initial handshake is for the client to establish confidence with the server and initiate a session so that messages can be sent securely.

1. Client
    * Generates and hashes a header data set that mostly consists of a time stamp.
    * The client encrypts the header data with its Certificate private key (the client authentication method is Certificate) and stores the encrypted data as an XML signature value.
    * The client embeds the base 64-encoded binary certificate into the header.
    * The entire contents above (encrypted header, xml signature value, certificate) is added as a SOAP XML header.
    * The client generates and stores a random key (**Client Entropy Key**) in its local cache and embeds it into the SOAP body.
2. Once server receives the request,
    * Server reads the certificate and ensures the client certificate is valid by validating chain/trust validation. If it fails, returns a fault exception with generic message.
    * Retrieves the Public Key (Asymmetric key) from the certificate, reads the header data from SOAP header and calculate the XML signature-value of header data by using the public key.
    * Compares the signature value with the passed signature value in the SOAP header to ensure message hasn't been modified. If it doesn't match, returns a fault exception with generic failure message. If it passes, it proceeds further.
    (This ensure the client who has signed the message is the owner of certificate as he must have signed with private key.)
    * One server ensures the message has come from an authenticated client
        * It generates one server random secret (**server entropy key**) and uses client passed entropy key and hashes both of them to generate a symmetric key.
        * Server creates a random unique id (**session token**), associate the symmetric key as the value to session token(key) and store the key/value pair into the server cache.
        * Server embeds the session token and server entropy and returns the response.
3. Once client receives the response
    * It uses it's **client entropy** key and **server entropy key** to generate same symmetric key. (The hashing algorithm that needs to be used is also passed)
    * It saves the Symmetric key along with **Session Token** (which is passed from server) into it's local cache.
4. For further communication header is encrypted(by client)/decrypted(by server) by **Symmetric Key** and both sides retrieve the symmetric key using **Session Token**. The session token remains mandatory to pass between client/server for further communication.
5. You can view the XML SOAP request and response in the **Appendix/RST Request** section.

### Service Call using Session Token (Phase 2)

![Service Invoke](/Documentation/DesignDocs/TransportMessageCredential/Diagram_authentication_phase2.png?raw=true)

1. Client
    * creates header data (primarily consisting of a time stamp) and hashes it.
    * The client fetches the **Symmetric Key** stored in its cache by using the **Session Token** and uses it to encrypt header data and put this as an XML signature value.
    * All of the above is added as SOAP XML header along with **Session Token**.
    * In the body, the client embeds input information to invoke real service (service logic or contract defined by the customer)
2. Once server  receives the request,
    * It first validates the XML signature.
        * It retrieves the "Symmetric Key" from the server cache using the "Session Token. The session token is always passed in the SOAP header.
        * Reads the XML header data and, using the symmetric key, calculates the signature value.
        * If the evaluated  value is same as passed signature value, it proceeds further else returns a generic fault message.
    * Once signature is validated, the Session token expiry is extended (this is done continuously until a session close or session expires).
    * The server (CoreWCF framework) reads the SOAP body to find which service logic to invoke, populate all desired inputs for the service, dispatch the invocation to the customer service, and return the results to the client.
3. Steps 1 and 2 continue one-time or multiple times until a session is alive between the client and server for multiple service executions.

### Close session (Phase 3)

* The client sends a request to close the session. The server should clean up all resources and clear the session token and symmetric key from its servers. The server sends a successful closed message to the client, and the client also does the clean-up.
* Cleanup of symmetric key and session close can also happen automatically if either client didn't send request for a longer period of time and cache expiry happens on the server side or server didn't respond to client request with multiple retries.

## Implementation Details

### Class Diagram

![Class Diagram](/Documentation/DesignDocs/TransportMessageCredential/class_diagram_tmc.png?raw=true)

### IServiceDispatcher

* `IServiceDispatcher` provides signatures for one important method `CreateServiceChannelDispatcherAsync`.
* The goal of `CreateServiceChannelDispatcherAsync` method is to build an `IServiceChannelDispatcher` concrete class.

```csharp
public interface IServiceDispatcher
{
    Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel);
}
```

### IServiceChannelDispatcher

* `IServiceChannelDispatcher` provides signatures for `DispatchAsync` method which takes 2 types of arguments.
* The objective of `DispatchAsync` method is dispatch service given a SOAP message.

```csharp
public interface IServiceChannelDispatcher
{
    Task DispatchAsync(RequestContext context);
    Task DispatchAsync(Message message);
}
```

### ReplyChannelDemuxer

* `ReplyChannelDemuxer`, which inherits 'TypedDemuxer', implements `CreateServiceChannelDispatcherAsync` to return `ReplyChannelDispatcher`, which is of type `IServiceChannelDispatcher`.
* `ReplyChannelDispatcher` implements the `DispatchAsync` method, which eventually gets called once the request reaches the transport layer.
* `ReplyChannelDispatcher` has a property `MessageFilterTable` to decide which type of incoming message it can respond to.
* When request arrives `DispatchAsync`, it first checks `MessageFilterTable` to match if it can invoke the `ServiceDispatcher` type it is holding onto.
* If not, it calls the `EndpointNotFoundAsync` method and returns a fault message. If yes, it invokes the `ServiceDispatcher.DispatchAsync` method.

```csharp
     public abstract Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel);
     public abstract IServiceDispatcher AddDispatcher(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter);
```

### SecurityServiceDispatcher

* `SecurityServiceDispatcher` is the primary class responsible for executing needed modules internal to the CoreWCF framework for establishing secure context between client and service.
* It wraps another internal class called `SecurityReplyChannelDispatcher`, which exposes two important methods, `DispatchAsync` and `ProcessReceivedRequestAsync`.
* `ProcessReceivedRequestAsync` main goal is to ensure the message header signature is validated, the client credential is correct, and the client is the original owner of the credential. It achieves this by calling the`ReceiveSecurityHeader.ProcessAsync` method.
* Once the security header is validated, the `DispatchAsync` method calls happen to invoke `SecuritySessionSecurityTokenAuthenticator` to create a secure conversation token and symmetric key.
* The property `InnerServiceDispatcher` holds the `ServiceDispatcher` object to invoke the customer-implemented service.
* `SecurityAuthServiceDispatcher` holds the `ServiceDispatcher` instance of the `SecuritySessionSecurityTokenAuthenticator` class.

**Few important methods/class signature are added , details code can be found in Github**

```csharp

 internal class SecurityServiceDispatcher : IServiceDispatcher, IDisposable
 {
   public IServiceDispatcher InnerServiceDispatcher { get; set; }

   public IServiceDispatcher SecurityAuthServiceDispatcher { get; set; }

   internal SecuritySessionServerSettings SessionServerSettings{get;}

   internal async Task<IServiceChannelDispatcher> GetAuthChannelDispatcher(IChannel outerChannel) {}
    
   internal Task<IServiceChannelDispatcher> GetInnerServiceChannelDispatcher(IChannel outerChannel) {}

    internal class SecurityReplyChannelDispatcher : ServerSecurityChannelDispatcher<IReplyChannel>, IReplyChannel
    {
        internal async ValueTask<RequestContext> ProcessReceivedRequestAsync(RequestContext requestContext) {}

        public override async Task DispatchAsync(RequestContext context) {}

    }
 }
```

### ReceiveSecurityHeader

* `ReceiveSecurityHeader` primary responsibility is to
  * Fetch client credentials
  * Validate client credentials
  * Fetch an asymmetric key or symmetric key from the SOAP security header.
  * Calculate the signature and ensure the XML signature is valid.
  * If any of the steps fail, it returns a failure message.
* It does all of the above in the primary method `ExecuteFullPassAsync`.
* As a part of `ExecuteFullPassAsync` method call, it reads each of the xml tag.
  * When it reads token (e:g- `BinarySecurityToken`), it calls the `ReadTokenAsync`.Which internally calls the `WSSecurityTokenSerializer.ReadTokenCore` method to return the `X509SecurityToken` (we are only considering about certificate auth).
    * `WSSecurityTokenSerializer` has list of token reader `TokenEntry` to read various credential types (e:g:- username/password, saml token, binary token etc)
    * Once the token (here the client certificate) is read, it invokes the concrete class implementing `SecurityTokenAuthenticator`. For certificate authentication mode, it calls the `X509SecurityTokenAuthenticator`. This ensures certificate trust/chain in correct before making any further progress.
  * When it reads the references, it ensures the references transformation can be validated by calling `ProcessReferenceList`.
  * When it reads the XML signature it calls the `ReadSignature` to read XML signature of whole header and validate by invoking `ProcessSupportingSignatureAsync`/ `ProcessPrimarySignatureAsync` by using the key retrieved during `ReadTokenAsync`. The key to generate signature varies depending on the xml tag.
    * For initial handshake the key is an asymmetric key (for certificate authentication mode), which `ReadTokenAsync` returns. (for `RST/SCT` request type).
    * Once secure session established it uses the session token to retrieve the symmetric key to generate signature. (header should have `SecurityContextToken` tag).

```csharp
    // Code snippet from ReceiveSecurityHeader.ExecuteFullPassAsync
      internal async ValueTask ExecuteFullPassAsync(XmlDictionaryReader reader)
        {
            bool primarySignatureFound = !RequireMessageProtection;
            int position = 0;
            while (reader.IsStartElement())
            {
                if (IsReaderAtSignature(reader))
                {
                    SignedXml signedXml = ReadSignature(reader, AppendPosition, null);
                    if (primarySignatureFound)
                    {
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Endorsing);
                        await ProcessSupportingSignatureAsync(signedXml, false);
                    }
                    else
                    {
                        primarySignatureFound = true;
                        ElementManager.SetBindingMode(position, ReceiveSecurityHeaderBindingModes.Primary);
                        await ProcessPrimarySignatureAsync(signedXml, false);
                    }
                }
                else if (IsReaderAtReferenceList(reader))
                {
                    ReferenceList referenceList = ReadReferenceList(reader);
                    ProcessReferenceList(referenceList);
                }
                else if (StandardsManager.WSUtilitySpecificationVersion.IsReaderAtTimestamp(reader))
                {
                    ReadTimestamp(reader);
                }
                else if (IsReaderAtEncryptedKey(reader))
                {
                    ReadEncryptedKey(reader, true);
                }
                else if (IsReaderAtEncryptedData(reader))
                {
                    EncryptedData encryptedData = ReadEncryptedData(reader);
                    primarySignatureFound = await ProcessEncryptedDataAsync(encryptedData, _timeoutHelper.RemainingTime(), position, true, primarySignatureFound);
                }
                else if (StandardsManager.SecurityVersion.IsReaderAtSignatureConfirmation(reader))
                {
                    ReadSignatureConfirmation(reader, AppendPosition, null);
                }
                else if (IsReaderAtSecurityTokenReference(reader))
                {
                    ReadSecurityTokenReference(reader);
                }
                else
                {
                    await ReadTokenAsync(reader, AppendPosition, null, null, null, _timeoutHelper.RemainingTime());
                }
                position++;
            }
            reader.ReadEndElement(); // wsse:Security
            reader.Close();
        }
```

### SecurityTokenAuthenticator

* As described above, `SecurityTokenAuthenticator` is the base class that provides two important signatures: `CanValidateToken` and `ValidateTokenCoreAsync`.
* `CanValidateToken` to ensure if there is a concrete implementation object who can validate the token is passed
* `ValidateTokenCoreAsync` to return `IAuthorizationPolicy` capturing `ClaimSet` based on the token.
* The classes that implement `SecurityTokenAuthenticator` are `CustomUserNameSecurityTokenAuthenticator`, `X509SecurityTokenAuthenticator`, 'WindowsSecurityTokenAuthenticator', and `SamlSecurityTokenAuthenticator`. Each of these classes represents the type of token or identity the client is passing to provide its authentication..

```csharp
    public abstract class SecurityTokenAuthenticator
    {
        protected SecurityTokenAuthenticator() { }

        public bool CanValidateToken(SecurityToken token)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }
            return CanValidateTokenCore(token);
        }

        protected abstract bool CanValidateTokenCore(SecurityToken token);
      
        protected virtual ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token) {}
    }
```

### WSSecurityOneDotZeroReceiveSecurityHeader

* `WSSecurityOneDotZeroReceiveSecurityHeader` class overrides two methods: `ReceiveSecurityHeader`. One is `ReadSignatureCore` and the other is `VerifySignatureAsync`.
* The `ReadSignatureCore` reads the signature and converts to [SignedXML](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.xml.signedxml?view=dotnet-plat-ext-8.0)
* `VerifySignatureAsync` implements the XML signature verification of security header (Either using  Asymmetric key or Symmetric key).

***Displaying important methods, for full codebase refer to Github***

```csharp
    internal class WSSecurityOneDotZeroReceiveSecurityHeader : ReceiveSecurityHeader
    {
      protected override SignedXml ReadSignatureCore(XmlDictionaryReader signatureReader) {}

      protected override async ValueTask<SecurityToken> VerifySignatureAsync(SignedXml signedXml, bool isPrimarySignature, SecurityHeaderTokenResolver resolver, object signatureTarget, string id) {}
    }
    
```

### SecuritySessionSecurityTokenAuthenticator

`SecuritySessionSecurityTokenAuthenticator` primary responsibilities are,

* Create a random server secret (entropy). Use the passed client entry and server entropy to generate the symmetric key.
* Create a random id as a secure conversation token (session token).
* Create an entry in the `SessionTokenCache` with key as session token and symmetric key as value.
* Create `RequestSecurityTokenResponse` as a SOAP response for the client (for the initial handshake) and include the session token and server entropy in the response. It achieves all of the above via the method `ProcessIssueRequest`.
* As a part of `ProcessIssueRequest`, it also invokes 'NotifyOperationCompletion', which ensures which `ServiceDispatcher` (in this case, `SessionInitiationMessageServiceDispatcher`) will be invoked by `ChannelDemuxer` when it receives the next request with a unique `SecurityContextToken`.

### SecuritySessionServerSettings

**Few important methods/class signature are added , details code can be found in Github**

```csharp
 internal sealed class SecuritySessionServerSettings : IServiceDispatcherSecureConversationSessionSettings, ISecurityCommunicationObject
 {
    private Task SetupSessionTokenAuthenticatorAsync(){}
    
    private void OnTimer(object state){}

    private void AddPendingSession(UniqueId sessionId, SecurityContextSecurityToken securityToken, MessageFilter filter) {}

    internal bool RemovePendingSession(UniqueId sessionId){}

    internal class SessionInitiationMessageServiceDispatcher : IServiceDispatcher
    {
      public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel){}
    }

    private abstract class ServerSecuritySimplexSessionChannel : ServerSecuritySessionChannel
    {
      protected override void OnCloseMessageReceived(RequestContext requestContext, Message message, SecurityProtocolCorrelationState correlationState, CancellationToken token){}

      public class SecurityReplySessionServiceChannelDispatcher : ServerSecuritySimplexSessionChannel, IServiceChannelDispatcher, IReplySessionChannel
      {
         
         private async ValueTask<(Message, SecurityProtocolCorrelationState, bool)> ProcessRequestContextAsync(RequestContext requestContext, TimeSpan timeout) {}

        public async Task DispatchAsync(RequestContext context) {}
      }
    }
```

* `SecuritySessionServerSettings` is central class for managing session across client and service.
* The `SetupSessionTokenAuthenticatorAsync` method sets up the `SessionTokenAuthenticator`, which creates a session token, generates a response, and saves the symmetric key and session ID to cache.
* The `OnTimer` method keeps tabs on the pending session. `PendingSession` is a session that the server has initiated by passing the response to the client with `SecurityContextToken` as the session ID and waiting for client to send service request with `SecurityContextToken`. 
* If, after a period of time, it doesn't receive the service call from the same client, it cleans up the session by calling 'RemovePendingSession', assuming that the client has aborted.
* `SessionInitiationMessageServiceDispatcher` is the `ServiceDispatcher` that gets called when a service invocation happens to execute a customer service call. The `SessionInitiationMessageServiceDispatcher` instance is created by calling the `OnTokenIssued` method from `SetupSessionTokenAuthenticatorAsync`.
* To ensure per client one instance of `SessionInitiationMessageServiceDispatcher` exists, for each session id (`SecurityContextToken`), one instance of `SessionInitiationMessageServiceDispatcher` is created and added to 'ChannelDemuxer'.
* `SessionInitiationMessageServiceDispatcher` dispatch customer service invocation via  `SecurityReplySessionServiceChannelDispatcher` method `DispatchAsync`.
* The `SecurityReplySessionServiceChannelDispatcher` class `DispatchAsync` method receives the message either to execute customer service or to close the session from the client. In case it receives the request to execute the service, it invokes customer service and returns the service result to the client. If it receives a request to close the session, it closes all resources and sends a close response to the client.



## Workflow Diagram

![CoreWCF Generic Queue](/Documentation/DesignDocs/TransportMessageCredential/workflow_diagram.png?raw=true)

* Once the RST/SCT (RequestSecurityToken) request is received at the `SecurityServiceDispatcher` (CoreWCF Transport), the request header is verified by calling 'ReceiveHeader.Verify'.
* `ReceiveHeader` deserializes the binary token to X509SecurityToken and validates the certificates.
* If the above validation succeeds, it goes to the next step; otherwise, it returns a fault exception with a generic message without any further details on the failure.
* From the X509SecurityToken, the certificate public key is extracted and used as an asymmetric key to calculate the XML signature of the header.
* If the calculated signature is the same as in the XML header, it goes to the next step; otherwise, it returns a fault exception with a generic message without any further details on the failure.
* Next, the `SecuritySessionSecurityTokenAuthenticator` is invoked to generate a symmetric key and session token (random id). The session token as a key and the symmetric key as a value are stored in the server cache. `SecuritySessionSecurityTokenAuthenticator` returns the response with the session token and server entropy.
* In the next request, the client sends the real inputs to CoreWCF along with a signed header and session token.
* The server validates the signature using the symmetric key by fetching it from the server cache using the session token as the key.
* Once signature validation is successful, it invokes customer service by passing the inputs it received from the client. Based on the result, it returns a response to the client.
* The above steps continue multiple times until the client doesn't send any requests or explicitly requests to close the session. In that case, the server closes the session, cleans up the cache, and uses any other resources.

## Appendix

## RST XML Request

### Request

```XML
<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:u="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/02/trust/RST/SCT</a:Action>
    <a:MessageID>urn:uuid:11048819-ca8b-4cc3-8054-ab18ff4abef3</a:MessageID>
    <a:ReplyTo>
      <a:Address>http://www.w3.org/2005/08/addressing/anonymous</a:Address>
    </a:ReplyTo>
    <a:To s:mustUnderstand="1" u:Id="_1">https://localhost:60867/WSHttpWcfService/basichttp.svc</a:To>
    <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <u:Timestamp u:Id="_0">
        <u:Created>2024-02-14T02:05:51.482Z</u:Created>
        <u:Expires>2024-02-14T02:10:51.482Z</u:Expires>
      </u:Timestamp>
      <o:BinarySecurityToken u:Id="uuid-e5dd3983-ef40-4470-839e-2c640d93da5e-1" ValueType="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3" EncodingType="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary">MIIDDTCCAfWgAwIBAgIJALAy3/cWOlvIMA0GCSqGSIb3DQEBCwUAMBQxeMlY7FZittmOBDnCi1hLPDX3sDlb7nn09VRxYvbRa6bk/LW2mTNt+O+yHKrtCLybgf9rECLaKDjkPcREkqLYHbhYH3Q6QecHytD0DcLaK3gu0QK5vCS0wvv7C4BxYCqh9yY8gQTiBSOTFihbMzquPZR2sL0f8Fej/8+9gcCQHBhlCe3FaZls4fCs0x3v7mA17Y4sFrAplRulgdbNDuqH3OBRQ107Cp+njKNirph2HTdzlZwtgMFEFzVr7/4fC5jeQ=</o:BinarySecurityToken>
      <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
        <SignedInfo>
          <CanonicalizationMethod Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
          <SignatureMethod Algorithm="http://www.w3.org/2000/09/xmldsig#rsa-sha1" />
          <Reference URI="#_0">
            <Transforms>
              <Transform Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
            </Transforms>
            <DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1" />
            <DigestValue>jl957D9ajY2i6C98yyoX4tYsohU=</DigestValue>
          </Reference>
          <Reference URI="#_1">
            <Transforms>
              <Transform Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
            </Transforms>
            <DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1" />
            <DigestValue>N83cBDD7qUhDg579CZ98aA8LplI=</DigestValue>
          </Reference>
        </SignedInfo>
        <SignatureValue>XMQOcLOj+c1uesrjnXmxNPiTU8+j9RpI/LeRZ9YNsK4oPpbDZYAEydWIrS7ocS90hJp7iqR1jZqvdtvE5e3SwmQBj68YApwaeSO05W2dUsEM1JkWJ034O8tvz0CxBaXq9rQzZg==</SignatureValue>
        <KeyInfo>
          <o:SecurityTokenReference>
            <o:Reference ValueType="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3" URI="#uuid-e5dd3983-ef40-4470-839e-2c640d93da5e-1" />
          </o:SecurityTokenReference>
        </KeyInfo>
      </Signature>
    </o:Security>
  </s:Header>
  <s:Body>
    <t:RequestSecurityToken xmlns:t="http://schemas.xmlsoap.org/ws/2005/02/trust">
      <t:TokenType>http://schemas.xmlsoap.org/ws/2005/02/sc/sct</t:TokenType>
      <t:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</t:RequestType>
      <t:Entropy>
        <t:BinarySecret u:Id="uuid-4751c816-b23b-4229-b5fe-6f3727786382-1" Type="http://schemas.xmlsoap.org/ws/2005/02/trust/Nonce">1TmBzNeSMhkxqHU+yWuwplJDS7NejEdnFlgRbGPyAOI=</t:BinarySecret>
      </t:Entropy>
      <t:KeySize>256</t:KeySize>
    </t:RequestSecurityToken>
  </s:Body>
</s:Envelope>
```

### Response

```XML
<s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/02/trust/RSTR/SCT</a:Action>
    <a:RelatesTo>urn:uuid:11048819-ca8b-4cc3-8054-ab18ff4abef3</a:RelatesTo>
  </s:Header>
  <s:Body>
    <t:RequestSecurityTokenResponse xmlns:t="http://schemas.xmlsoap.org/ws/2005/02/trust" xmlns:u="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
      <t:TokenType>http://schemas.xmlsoap.org/ws/2005/02/sc/sct</t:TokenType>
      <t:RequestedSecurityToken>
        <c:SecurityContextToken u:Id="uuid-e07815b0-d900-49c8-8ec6-a8ee018263c9-1" xmlns:c="http://schemas.xmlsoap.org/ws/2005/02/sc">
          <c:Identifier>urn:uuid:40859149-0ab7-4ee2-a7cc-22bc21adfe08</c:Identifier>
        </c:SecurityContextToken>
      </t:RequestedSecurityToken>
      <t:RequestedAttachedReference>
        <o:SecurityTokenReference xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
          <o:Reference ValueType="http://schemas.xmlsoap.org/ws/2005/02/sc/sct" URI="#uuid-e07815b0-d900-49c8-8ec6-a8ee018263c9-1">
          </o:Reference>
        </o:SecurityTokenReference>
      </t:RequestedAttachedReference>
      <t:RequestedUnattachedReference>
        <o:SecurityTokenReference xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
          <o:Reference URI="urn:uuid:40859149-0ab7-4ee2-a7cc-22bc21adfe08" ValueType="http://schemas.xmlsoap.org/ws/2005/02/sc/sct">
          </o:Reference>
        </o:SecurityTokenReference>
      </t:RequestedUnattachedReference>
      <t:RequestedProofToken>
        <t:ComputedKey>http://schemas.xmlsoap.org/ws/2005/02/trust/CK/PSHA1</t:ComputedKey>
      </t:RequestedProofToken>
      <t:Entropy>
        <t:BinarySecret u:Id="uuid-e07815b0-d900-49c8-8ec6-a8ee018263c9-2" Type="http://schemas.xmlsoap.org/ws/2005/02/trust/Nonce">X10bPPRFJzVr13nwxYYVLpmd5Fsu6RR7jkF5xtCV/kM=</t:BinarySecret>
      </t:Entropy>
      <t:Lifetime>
        <u:Created>2024-02-14T02:06:53.311Z</u:Created>
        <u:Expires>2024-02-14T17:06:53.311Z</u:Expires>
      </t:Lifetime>
      <t:KeySize>256</t:KeySize>
    </t:RequestSecurityTokenResponse>
  </s:Body>
</s:Envelope>

```

## Service Request and Response

### Service Request

```xml
<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:u="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://tempuri.org/IEchoService/EchoString</a:Action>
    <a:MessageID>urn:uuid:abd7511b-95ad-4a9e-9647-1c07b9a11ef9</a:MessageID>
    <a:ReplyTo>
      <a:Address>http://www.w3.org/2005/08/addressing/anonymous</a:Address>
    </a:ReplyTo>
    <a:To s:mustUnderstand="1">https://localhost:60867/WSHttpWcfService/basichttp.svc</a:To>
    <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <u:Timestamp u:Id="_0">
        <u:Created>2024-02-14T02:07:04.784Z</u:Created>
        <u:Expires>2024-02-14T02:12:04.784Z</u:Expires>
      </u:Timestamp>
      <c:SecurityContextToken u:Id="uuid-e07815b0-d900-49c8-8ec6-a8ee018263c9-1" xmlns:c="http://schemas.xmlsoap.org/ws/2005/02/sc">
        <c:Identifier>urn:uuid:40859149-0ab7-4ee2-a7cc-22bc21adfe08</c:Identifier>
      </c:SecurityContextToken>
      <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
        <SignedInfo>
          <CanonicalizationMethod Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
          <SignatureMethod Algorithm="http://www.w3.org/2000/09/xmldsig#hmac-sha1" />
          <Reference URI="#_0">
            <Transforms>
              <Transform Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
            </Transforms>
            <DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1" />
            <DigestValue>Ez9dbXPzjM7hrzmienuYWiT3QDE=</DigestValue>
          </Reference>
        </SignedInfo>
        <SignatureValue>u1Ea4tTYJ6xCsT00WjiqxF5fNow=</SignatureValue>
        <KeyInfo>
          <o:SecurityTokenReference>
            <o:Reference ValueType="http://schemas.xmlsoap.org/ws/2005/02/sc/sct" URI="#uuid-e07815b0-d900-49c8-8ec6-a8ee018263c9-1" />
          </o:SecurityTokenReference>
        </KeyInfo>
      </Signature>
    </o:Security>
  </s:Header>
  <s:Body>
    <EchoString xmlns="http://tempuri.org/">
      <echo>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</echo>
    </EchoString>
  </s:Body>
</s:Envelope>
```

### Service Response

```xml
<s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://tempuri.org/IEchoService/EchoStringResponse</a:Action>
  </s:Header>
  <s:Body>
    <EchoStringResponse xmlns="http://tempuri.org/">
      <EchoStringResult>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</EchoStringResult>
    </EchoStringResponse>
  </s:Body>
</s:Envelope>
```

## Close Request and Response

### Request

```XML
<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:u="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/02/trust/RST/SCT/Cancel</a:Action>
    <a:MessageID>urn:uuid:4018d6c9-62a0-41e4-9dd7-cf04b0f8b19b</a:MessageID>
    <a:ReplyTo>
      <a:Address>http://www.w3.org/2005/08/addressing/anonymous</a:Address>
    </a:ReplyTo>
    <a:To s:mustUnderstand="1">https://localhost:60867/WSHttpWcfService/basichttp.svc</a:To>
    <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <u:Timestamp u:Id="_0">
        <u:Created>2024-02-14T02:08:26.004Z</u:Created>
        <u:Expires>2024-02-14T02:13:26.004Z</u:Expires>
      </u:Timestamp>
      <c:SecurityContextToken u:Id="uuid-e07815b0-d900-49c8-8ec6-a8ee018263c9-1" xmlns:c="http://schemas.xmlsoap.org/ws/2005/02/sc">
        <c:Identifier>urn:uuid:40859149-0ab7-4ee2-a7cc-22bc21adfe08</c:Identifier>
      </c:SecurityContextToken>
      <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
        <SignedInfo>
          <CanonicalizationMethod Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
          <SignatureMethod Algorithm="http://www.w3.org/2000/09/xmldsig#hmac-sha1" />
          <Reference URI="#_0">
            <Transforms>
              <Transform Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
            </Transforms>
            <DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1" />
            <DigestValue>3Mt6VCsGhHlkMnsrDBTWGQSUX/o=</DigestValue>
          </Reference>
        </SignedInfo>
        <SignatureValue>En1kfMeqpMJdjhS1F+O02mKOK14=</SignatureValue>
        <KeyInfo>
          <o:SecurityTokenReference>
            <o:Reference ValueType="http://schemas.xmlsoap.org/ws/2005/02/sc/sct" URI="#uuid-e07815b0-d900-49c8-8ec6-a8ee018263c9-1" />
          </o:SecurityTokenReference>
        </KeyInfo>
      </Signature>
    </o:Security>
  </s:Header>
  <s:Body>
    <t:RequestSecurityToken xmlns:t="http://schemas.xmlsoap.org/ws/2005/02/trust">
      <t:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Cancel</t:RequestType>
      <t:CancelTarget>
        <o:SecurityTokenReference xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
          <o:Reference URI="urn:uuid:40859149-0ab7-4ee2-a7cc-22bc21adfe08" ValueType="http://schemas.xmlsoap.org/ws/2005/02/sc/sct">
          </o:Reference>
        </o:SecurityTokenReference>
      </t:CancelTarget>
    </t:RequestSecurityToken>
  </s:Body>
</s:Envelope>
```

### Response

```XML
<s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
  <s:Header>
    <a:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/02/trust/RSTR/SCT/Cancel</a:Action>
    <a:RelatesTo>urn:uuid:3856fb84-ac4a-471e-9764-a47d3fa3ed5f</a:RelatesTo>
  </s:Header>
  <s:Body>
    <t:RequestSecurityTokenResponse xmlns:t="http://schemas.xmlsoap.org/ws/2005/02/trust" xmlns:u="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
      <t:RequestedTokenCancelled>
      </t:RequestedTokenCancelled>
    </t:RequestSecurityTokenResponse>
  </s:Body>
</s:Envelope>
```
