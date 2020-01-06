using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.MessageMember, AllowMultiple = false, Inherited = false)]
    internal sealed class MessageHeaderArrayAttribute : MessageHeaderAttribute
    {
    }
}