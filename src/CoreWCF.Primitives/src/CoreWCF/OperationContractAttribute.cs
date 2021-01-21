// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using CoreWCF.Description;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.OperationContract)]
    public sealed class OperationContractAttribute : Attribute
    {
        private string _name;
        private string _action;
        private string _replyAction;
        private bool _isOneWay;

        public string Name
        {
            get { return _name; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                if (value == "")
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value),
                        SR.SFxNameCannotBeEmpty));
                }

                _name = value;
            }
        }

        internal const string ActionPropertyName = "Action";
        public string Action
        {
            get { return _action; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _action = value;
            }
        }

        internal const string ReplyActionPropertyName = "ReplyAction";
        public string ReplyAction
        {
            get { return _replyAction; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _replyAction = value;
            }
        }

        public bool AsyncPattern { get; set; }

        public bool IsOneWay
        {
            get { return _isOneWay; }
            set { _isOneWay = value; }
        }

        internal bool IsSessionOpenNotificationEnabled
        {
            get
            {
                return Action == OperationDescription.SessionOpenedAction;
            }
        }

        internal void EnsureInvariants(MethodInfo methodInfo, string operationName)
        {
            // This code is used for WebSockets open notification
            //if (IsSessionOpenNotificationEnabled)
            //{
            //    if (!IsOneWay
            //     || !this.IsInitiating
            //     || methodInfo.GetParameters().Length > 0)
            //    {
            //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
            //            SR.Format(SR.ContractIsNotSelfConsistentWhenIsSessionOpenNotificationEnabled, operationName, "Action", OperationDescription.SessionOpenedAction, "IsOneWay", "IsInitiating")));
            //    }
            //}
        }
    }
}