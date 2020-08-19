using CoreWCF;
using CoreWCF.Channels;
using Helpers;
using ServiceContract;

namespace Services
{
    public class MathService : IMathService
    {
        public int Add(int x, int y)
        {
            OperationContextUtility.VerifyRequestHavingMessageHeader(OperationContext.Current, Helpers.Constants.MessageHeaderNameUserId, Helpers.Constants.MessageHeaderNamespace);
            MessageHeader mh = MessageHeader.CreateHeader(Helpers.Constants.MessageHeaderNameMathServiceAddResponse, Helpers.Constants.MessageHeaderNamespace, string.Empty);
            OperationContext.Current.OutgoingMessageHeaders.Add(mh);

            return x + y;
        }

        public int Subtract(int x, int y)
        {
            OperationContextUtility.VerifyRequestHavingMessageHeader(OperationContext.Current, Helpers.Constants.MessageHeaderNameUserId, Helpers.Constants.MessageHeaderNamespace);

            return x - y;
        }

        public int Multiply(int x, int y)
        {
            OperationContextUtility.VerifyRequestHavingMessageHeader(OperationContext.Current, Helpers.Constants.MessageHeaderNameUserId, Helpers.Constants.MessageHeaderNamespace);
            MessageHeader mh = MessageHeader.CreateHeader(Helpers.Constants.MessageHeaderNameMathServiceMultiplyResponse, Helpers.Constants.MessageHeaderNamespace, string.Empty);
            OperationContext.Current.OutgoingMessageHeaders.Add(mh);

            return x * y;
        }
    }
}
