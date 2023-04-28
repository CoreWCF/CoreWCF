using CoreWCF;
using System;

namespace Helpers
{
    internal static class OperationContextUtility
    {
        /// <summary>
        /// Helper method to check if a particular header is present in the request context.
        /// if NOT present then this method throws a TestCaseFailedException
        /// </summary>
        /// <exception cref="Exception"/>
        public static void VerifyRequestHavingMessageHeader(OperationContext oc, string headerName, string headerNamespace)
        {
            if (oc.RequestContext.RequestMessage.Headers != null)
            {
                if (oc.RequestContext.RequestMessage.Headers.FindHeader(headerName, headerNamespace) >= 0)
                {
                    Console.WriteLine($"Found the expected request header '{headerName}'");
                }
                else
                {
                    throw new Exception($"Expected request header '{headerName}' to be present, but didn't find it");
                }
            }
        }

        /// <summary>
        /// Helper method to check if a particular header is present in the response
        /// </summary>
        /// <exception cref="TestCaseFailedException"/>
        /// <param name="oc1"></param>
        /// <returns></returns>
        public static void VerifyResponseHavingMessageHeader(System.ServiceModel.OperationContext oc, string headerName, string headerNamespace)
        {
            if (oc.IncomingMessageHeaders != null)
            {
                if (oc.IncomingMessageHeaders.FindHeader(headerName, headerNamespace) >= 0)
                {
                    Console.WriteLine($"Found the expected response header '{headerName}'");
                }
                else
                {
                    throw new Exception($"Test case failed: Expected response header '{headerName}', but didn't find one.");
                }
            }
        }

        /// <summary>
        /// Adds a message header just for the sake of testing to see if this header could be retrieved on the server side
        /// </summary>
        /// <param name="oc1"></param>
        public static void AddMessageHeader(System.ServiceModel.OperationContext oc)
        {
            //create and add to outgoing message headers
            System.ServiceModel.Channels.MessageHeader header = System.ServiceModel.Channels.MessageHeader.CreateHeader(Constants.MessageHeaderNameUserId, Constants.MessageHeaderNamespace, "ABC1000");
            oc.OutgoingMessageHeaders.Add(header);
        }

        /// <summary>
        /// Checks for the required data in OperationContext's outgoing message headers and incoming message properties after the "await" call. Throws "TestCaseFailedException"
        /// if they are not found.
        /// </summary>
        /// <exception cref="Exception"/>
        /// <param name="oc1"></param>
        public static void VerifyMessageProperties(System.ServiceModel.OperationContext oc)
        {
            #region check for outgoing message headers

            bool foundRequiredOutgoingMessageHeadersData = false;
            if (oc != null && oc.OutgoingMessageHeaders != null)
            {
                if (oc.OutgoingMessageHeaders.FindHeader(Constants.MessageHeaderNameUserId, Constants.MessageHeaderNamespace) >= 0)
                {
                    foundRequiredOutgoingMessageHeadersData = true;
                }
            }

            //throw exception and fail the test
            if (!foundRequiredOutgoingMessageHeadersData)
            {
                throw new Exception($"Test failed: Expected outgoing message header '{Constants.MessageHeaderNameUserId}' to be present after the 'await' call. But didn't find one.");
            }

            #endregion

            #region check for incoming message properties

            bool foundRequiredIncomingMessagePropertyData = false;
            if (oc != null && oc.IncomingMessageProperties != null)
            {
                System.ServiceModel.Channels.HttpResponseMessageProperty responseProperty = oc.IncomingMessageProperties["httpResponse"] as System.ServiceModel.Channels.HttpResponseMessageProperty;

                if (responseProperty != null && responseProperty.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    foundRequiredIncomingMessagePropertyData = true;
                }
            }

            //throw exception and fail the test
            if (!foundRequiredIncomingMessagePropertyData)
            {
                throw new Exception("Test failed: Expected incoming message properties to be present after the 'await' call. But they are not present.");
            }

            #endregion
        }

        /// <summary>
        /// Checks for the required data in OperationContext's outgoing message headers before sending response. Throws "TestCaseFailedException"
        /// </summary>
        /// <exception cref="Exception"/>
        /// <param name="oc"></param>
        public static void VerifyOutgoingMessageHeader(OperationContext oc)
        {
            bool foundRequiredOutgoingMessageHeadersData = false;
            if (oc != null && oc.OutgoingMessageHeaders != null)
            {
                if (oc.OutgoingMessageHeaders.FindHeader(Constants.MessageHeaderNameUserId, Constants.MessageHeaderNamespace) >= 0)
                {
                    foundRequiredOutgoingMessageHeadersData = true;
                }
            }

            //throw exception and fail the test
            if (!foundRequiredOutgoingMessageHeadersData)
            {
                throw new Exception(string.Format("Test failed: Expected outgoing message header '{0}' to be present after the 'await' call. But didn't find one.", Constants.MessageHeaderNameUserId));
            }
        }
    }

    internal static class Constants
    {
        //Message header info which are passed between our test client and services
        public const string MessageHeaderNameUserId = "UserId";
        public const string MessageHeaderNameMathServiceAddResponse = "MathServiceAddResponse";
        public const string MessageHeaderNameMathServiceMultiplyResponse = "MathServiceMultiplyResponse";
        public const string MessageHeaderNameCustomerServiceGetCustomerResponse = "GetCustomer-ResponseHeader";
        public const string MessageHeaderNameCustomerServiceUpdateCustoemrResponse = "UpdateCustomer-ResponseHeader";
        public const string MessageHeaderNamespace = "http://Microsoft.WCF.Documentation";
    }
}
