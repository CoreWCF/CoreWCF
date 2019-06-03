using System;

namespace CoreWCF
{
    [AttributeUsage(ServiceModelAttributeTargets.MessageMember, AllowMultiple = false, Inherited = false)]
    internal sealed class MessageHeaderArrayAttribute : MessageHeaderAttribute
    {
    }
}