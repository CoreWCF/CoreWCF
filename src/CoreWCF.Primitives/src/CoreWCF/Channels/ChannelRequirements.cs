using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CoreWCF.Description;

namespace CoreWCF.Channels
{
    internal struct ChannelRequirements
    {
        public bool usesInput;
        public bool usesReply;
        public bool usesOutput;
        public bool usesRequest;
        public SessionMode sessionMode;

        public static void ComputeContractRequirements(ContractDescription contractDescription,
            out ChannelRequirements requirements)
        {
            requirements = new ChannelRequirements();

            requirements.usesInput = false;
            requirements.usesReply = false;
            requirements.usesOutput = false;
            requirements.usesRequest = false;
            requirements.sessionMode = contractDescription.SessionMode;

            for (int i = 0; i < contractDescription.Operations.Count; i++)
            {
                OperationDescription operation = contractDescription.Operations[i];
                bool oneWay = (operation.IsOneWay);
                if (!operation.IsServerInitiated())
                {
                    if (oneWay)
                    {
                        requirements.usesInput = true;
                    }
                    else
                    {
                        requirements.usesReply = true;
                    }
                }
                else
                {
                    if (oneWay)
                    {
                        requirements.usesOutput = true;
                    }
                    else
                    {
                        requirements.usesRequest = true;
                    }
                }
            }
        }

        public static Type[] ComputeRequiredChannels(ref ChannelRequirements requirements)
        {
            if (requirements.usesOutput || requirements.usesRequest)
            {
                switch (requirements.sessionMode)
                {
                    case SessionMode.Allowed:
                        return new[] {
                            typeof(IDuplexChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.Required:
                        return new[] {
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.NotAllowed:
                        return new[] {
                            typeof(IDuplexChannel),
                        };
                }
            }
            else if (requirements.usesInput && requirements.usesReply)
            {
                switch (requirements.sessionMode)
                {
                    case SessionMode.Allowed:
                        return new[] {
                            typeof(IRequestChannel),
                            typeof(IRequestSessionChannel),
                            typeof(IDuplexChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.Required:
                        return new[] {
                            typeof(IRequestSessionChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.NotAllowed:
                        return new[] {
                            typeof(IRequestChannel),
                            typeof(IDuplexChannel),
                        };
                }
            }
            else if (requirements.usesInput)
            {
                switch (requirements.sessionMode)
                {
                    case SessionMode.Allowed:
                        return new[] {
                            typeof(IOutputChannel),
                            typeof(IOutputSessionChannel),
                            typeof(IRequestChannel),
                            typeof(IRequestSessionChannel),
                            typeof(IDuplexChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.Required:
                        return new[] {
                            typeof(IOutputSessionChannel),
                            typeof(IRequestSessionChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.NotAllowed:
                        return new[] {
                            typeof(IOutputChannel),
                            typeof(IRequestChannel),
                            typeof(IDuplexChannel),
                        };
                }
            }
            else if (requirements.usesReply)
            {
                switch (requirements.sessionMode)
                {
                    case SessionMode.Allowed:
                        return new[] {
                            typeof(IRequestChannel),
                            typeof(IRequestSessionChannel),
                            typeof(IDuplexChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.Required:
                        return new[] {
                            typeof(IRequestSessionChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.NotAllowed:
                        return new[] {
                            typeof(IRequestChannel),
                            typeof(IDuplexChannel),
                        };
                }
            }
            else
            {
                switch (requirements.sessionMode)
                {
                    case SessionMode.Allowed:
                        return new[] {
                            typeof(IOutputSessionChannel),
                            typeof(IOutputChannel),
                            typeof(IRequestSessionChannel),
                            typeof(IRequestChannel),
                            typeof(IDuplexChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.Required:
                        return new[] {
                            typeof(IOutputSessionChannel),
                            typeof(IRequestSessionChannel),
                            typeof(IDuplexSessionChannel),
                        };

                    case SessionMode.NotAllowed:
                        return new[] {
                            typeof(IOutputChannel),
                            typeof(IRequestChannel),
                            typeof(IDuplexChannel),
                        };
                }
            }
            return null;
        }

        public static bool IsSessionful(Type channelType)
        {
            return (channelType == typeof(IDuplexSessionChannel) ||
                    channelType == typeof(IOutputSessionChannel) ||
                    channelType == typeof(IInputSessionChannel) ||
                    channelType == typeof(IReplySessionChannel) ||
                    channelType == typeof(IRequestSessionChannel));
        }

        public static bool IsOneWay(Type channelType)
        {
            return (channelType == typeof(IOutputChannel) ||
                    channelType == typeof(IInputChannel) ||
                    channelType == typeof(IInputSessionChannel) ||
                    channelType == typeof(IOutputSessionChannel));
        }

        public static bool IsRequestReply(Type channelType)
        {
            return (channelType == typeof(IRequestChannel) ||
                    channelType == typeof(IReplyChannel) ||
                    channelType == typeof(IReplySessionChannel) ||
                    channelType == typeof(IRequestSessionChannel));
        }

        public static bool IsDuplex(Type channelType)
        {
            return (channelType == typeof(IDuplexChannel) ||
                    channelType == typeof(IDuplexSessionChannel));
        }

        public static Exception CantCreateChannelException(IEnumerable<Type> supportedChannels, IEnumerable<Type> requiredChannels, string bindingName)
        {
            string contractChannelTypesString = "";
            string bindingChannelTypesString = "";

            Exception exception = BindingContractMismatchException(supportedChannels, requiredChannels, bindingName,
                ref contractChannelTypesString, ref bindingChannelTypesString);

            if (exception == null)
            {
                // none of the obvious speculations about the failure holds, so we fall back to the generic error message
                exception = new InvalidOperationException(SR.Format(SR.CouldnTCreateChannelForType2, bindingName, contractChannelTypesString));
            }

            return exception;
        }

        public static Exception BindingContractMismatchException(IEnumerable<Type> supportedChannels, IEnumerable<Type> requiredChannels,
            string bindingName, ref string contractChannelTypesString, ref string bindingChannelTypesString)
        {
            StringBuilder contractChannelTypes = new StringBuilder();
            bool contractRequiresOneWay = true;
            bool contractRequiresRequestReply = true;
            bool contractRequiresDuplex = true;
            bool contractRequiresTwoWay = true;  // request-reply or duplex
            bool contractRequiresSession = true;
            bool contractRequiresDatagram = true;
            foreach (Type channelType in requiredChannels)
            {
                if (contractChannelTypes.Length > 0)
                {
                    contractChannelTypes.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                    contractChannelTypes.Append(" ");
                }
                string typeString = channelType.ToString();
                contractChannelTypes.Append(typeString.Substring(typeString.LastIndexOf('.') + 1));

                if (!IsOneWay(channelType))
                {
                    contractRequiresOneWay = false;
                }
                if (!IsRequestReply(channelType))
                {
                    contractRequiresRequestReply = false;
                }
                if (!IsDuplex(channelType))
                {
                    contractRequiresDuplex = false;
                }
                if (!(IsRequestReply(channelType) || IsDuplex(channelType)))
                {
                    contractRequiresTwoWay = false;
                }
                if (!IsSessionful(channelType))
                {
                    contractRequiresSession = false;
                }
                else
                {
                    contractRequiresDatagram = false;
                }
            }

            StringBuilder bindingChannelTypes = new StringBuilder();
            bool bindingSupportsOneWay = false;
            bool bindingSupportsRequestReply = false;
            bool bindingSupportsDuplex = false;
            bool bindingSupportsSession = false;
            bool bindingSupportsDatagram = false;
            bool bindingSupportsAtLeastOneChannelType = false;
            foreach (Type channelType in supportedChannels)
            {
                bindingSupportsAtLeastOneChannelType = true;
                if (bindingChannelTypes.Length > 0)
                {
                    bindingChannelTypes.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                    bindingChannelTypes.Append(" ");
                }
                string typeString = channelType.ToString();
                bindingChannelTypes.Append(typeString.Substring(typeString.LastIndexOf('.') + 1));

                if (IsOneWay(channelType))
                {
                    bindingSupportsOneWay = true;
                }
                if (IsRequestReply(channelType))
                {
                    bindingSupportsRequestReply = true;
                }
                if (IsDuplex(channelType))
                {
                    bindingSupportsDuplex = true;
                }
                if (IsSessionful(channelType))
                {
                    bindingSupportsSession = true;
                }
                else
                {
                    bindingSupportsDatagram = true;
                }
            }
            bool bindingSupportsTwoWay = bindingSupportsRequestReply || bindingSupportsDuplex;

            if (!bindingSupportsAtLeastOneChannelType)
            {
                return new InvalidOperationException(SR.Format(SR.BindingDoesnTSupportAnyChannelTypes1, bindingName));
            }
            if (contractRequiresSession && !bindingSupportsSession)
            {
                return new InvalidOperationException(SR.Format(SR.BindingDoesnTSupportSessionButContractRequires1, bindingName));
            }
            if (contractRequiresDatagram && !bindingSupportsDatagram)
            {
                return new InvalidOperationException(SR.Format(SR.BindingDoesntSupportDatagramButContractRequires, bindingName));
            }
            if (contractRequiresDuplex && !bindingSupportsDuplex)
            {
                return new InvalidOperationException(SR.Format(SR.BindingDoesnTSupportDuplexButContractRequires1, bindingName));
            }
            if (contractRequiresRequestReply && !bindingSupportsRequestReply)
            {
                return new InvalidOperationException(SR.Format(SR.BindingDoesnTSupportRequestReplyButContract1, bindingName));
            }
            if (contractRequiresOneWay && !bindingSupportsOneWay)
            {
                return new InvalidOperationException(SR.Format(SR.BindingDoesnTSupportOneWayButContractRequires1, bindingName));
            }
            if (contractRequiresTwoWay && !bindingSupportsTwoWay)
            {
                return new InvalidOperationException(SR.Format(SR.BindingDoesnTSupportTwoWayButContractRequires1, bindingName));
            }

            contractChannelTypesString = contractChannelTypes.ToString();
            bindingChannelTypesString = bindingChannelTypes.ToString();

            return null;
        }

    }
}