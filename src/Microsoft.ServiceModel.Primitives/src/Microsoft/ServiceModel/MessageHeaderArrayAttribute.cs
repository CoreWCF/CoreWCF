using System;

namespace Microsoft.ServiceModel
{
    [AttributeUsage(ServiceModelAttributeTargets.MessageMember, AllowMultiple = false, Inherited = false)]
    internal sealed class MessageHeaderArrayAttribute : MessageHeaderAttribute
    {
    }
}