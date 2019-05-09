using System;
using Microsoft.ServiceModel.Description;

namespace Microsoft.ServiceModel
{
    // TODO: Make this public
    internal abstract class MessageContractMemberAttribute : Attribute
    {
        string _name;
        string _ns;
        bool _isNameSetExplicit;
        bool _isNamespaceSetExplicit;
        //ProtectionLevel protectionLevel = ProtectionLevel.None;
        //bool hasProtectionLevel = false;

        internal const string NamespacePropertyName = "Namespace";
        public string Namespace
        {
            get { return _ns; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value.Length > 0)
                {
                    NamingHelper.CheckUriProperty(value, "Namespace");
                }
                _ns = value;
                _isNamespaceSetExplicit = true;
            }
        }

        internal bool IsNamespaceSetExplicit
        {
            get { return _isNamespaceSetExplicit; }
        }

        internal const string NamePropertyName = "Name";
        public string Name
        {
            get { return _name; }
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

                _name = value; _isNameSetExplicit = true;
            }
        }

        internal bool IsNameSetExplicit
        {
            get { return _isNameSetExplicit; }
        }

        //internal const string ProtectionLevelPropertyName = "ProtectionLevel";
        //public ProtectionLevel ProtectionLevel
        //{
        //    get
        //    {
        //        return this.protectionLevel;
        //    }
        //    set
        //    {
        //        if (!ProtectionLevelHelper.IsDefined(value))
        //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
        //        this.protectionLevel = value;
        //        this.hasProtectionLevel = true;
        //    }
        //}

        //public bool HasProtectionLevel
        //{
        //    get { return this.hasProtectionLevel; }
        //}
    }

}