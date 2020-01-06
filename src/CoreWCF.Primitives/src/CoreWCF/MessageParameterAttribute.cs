using System;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.Parameter, Inherited = false)]
    internal sealed class MessageParameterAttribute : Attribute
    {
        string name;
        bool isNameSetExplicit;
        internal const string NamePropertyName = "Name";
        public string Name
        {
            get { return name; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                if (value == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value),
                        SR.SFxNameCannotBeEmpty));
                }
                name = value; isNameSetExplicit = true;
            }
        }

        internal bool IsNameSetExplicit
        {
            get { return isNameSetExplicit; }
        }
    }
}