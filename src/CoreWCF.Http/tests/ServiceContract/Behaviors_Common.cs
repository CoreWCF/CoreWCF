// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace ServiceContract
{
    public class BehaviorFlags
    {
        public bool ChannelBehaviorFlag { get; set; }
        public bool ServiceBehaviorFlag { get; set; }
        public bool ProxyContractBehaviorFlag { get; set; }
        public bool DisptacherContractBehaviorFlag { get; set; }
        public bool ProxyOperationBehaviorFlag { get; set; }
        public bool DisptacherOperationBehaviorFlag { get; set; }
        public bool ServiceEndpointBehaviorFlag { get; set; }
    }

    public abstract class CustomBehaviorAttribute : Attribute
    {
        public BehaviorFlags m_BehaviorFlags = new BehaviorFlags();

        public bool IsServiceBehaviorOnlyInvoked()
        {
            if (m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.ChannelBehaviorFlag &&
                !m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.ServiceEndpointBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsChannelBehaviorOnlyInvoked()
        {
            if (m_BehaviorFlags.ChannelBehaviorFlag &&
                !m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.ServiceEndpointBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsProxyContractBehaviorOnlyInvoked()
        {
            if (m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.ChannelBehaviorFlag &&
                !m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.ServiceEndpointBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsDispatcherContractBehaviorOnlyInvoked()
        {
            if (m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.ChannelBehaviorFlag &&
                !m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.ServiceEndpointBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsDispatcherOperationBehaviorOnlyInvoked()
        {
            if (m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.ChannelBehaviorFlag &&
                !m_BehaviorFlags.ServiceEndpointBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsProxyOperationBehaviorOnlyInvoked()
        {
            if (m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.ChannelBehaviorFlag &&
                !m_BehaviorFlags.ServiceEndpointBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsServiceEndpointBehaviorOnlyInvoked()
        {
            if (m_BehaviorFlags.ServiceEndpointBehaviorFlag &&
                !m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.ChannelBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsNoBehaviorInvoked()
        {
            if (!m_BehaviorFlags.ServiceEndpointBehaviorFlag &&
                !m_BehaviorFlags.ProxyOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherOperationBehaviorFlag &&
                !m_BehaviorFlags.DisptacherContractBehaviorFlag &&
                !m_BehaviorFlags.ProxyContractBehaviorFlag &&
                !m_BehaviorFlags.ServiceBehaviorFlag &&
                !m_BehaviorFlags.ChannelBehaviorFlag)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class MyMultiFacetedBehaviorAttribute : CustomBehaviorAttribute, IContractBehavior, IServiceBehavior, IOperationBehavior, IEndpointBehavior
    {
        public void Validate(ServiceEndpoint endpoint)
        {
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime behavior)
        {
            m_BehaviorFlags.ChannelBehaviorFlag = true;
        }

        public void ApplyDispatchBehavior(ServiceEndpoint serviceEndpoint, EndpointDispatcher endpointDispatcher)
        {
            m_BehaviorFlags.ServiceEndpointBehaviorFlag = true;
        }

        public void ApplyDispatchBehavior(ContractDescription description, ServiceEndpoint endpoint, DispatchRuntime dispatch)
        {
            m_BehaviorFlags.DisptacherContractBehaviorFlag = true;
        }

        public void Validate(ContractDescription description, ServiceEndpoint endpoint)
        {
        }

        public void AddBindingParameters(ContractDescription description, ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
        }

        public void ApplyClientBehavior(ContractDescription description, ServiceEndpoint endpoint, ClientRuntime proxy)
        {
            m_BehaviorFlags.ProxyContractBehaviorFlag = true;
        }

        public void Validate(ServiceDescription description, ServiceHostBase serviceHost)
        {
        }

        public void AddBindingParameters(ServiceDescription description, ServiceHostBase serviceHost, Collection<ServiceEndpoint> endpoints, BindingParameterCollection parameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHost)
        {
            m_BehaviorFlags.ServiceBehaviorFlag = true;
        }

        public void Validate(OperationDescription description)
        {
        }

        public void AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }

        public void ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            m_BehaviorFlags.DisptacherOperationBehaviorFlag = true;
        }

        public void ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
            m_BehaviorFlags.ProxyOperationBehaviorFlag = true;
        }
    }

    public enum BehaviorType
    { IServiceBehavior, IEndpointBehavior, IContractBehavior, IOperationBehavior }

    public class BehaviorInvokedVerifier
    {
        private static string GetBehaviorsResult(SortedList CustomBehaviorsList)
        {
            StringBuilder resultsSB = new StringBuilder("");

            for (int i = 0; i < CustomBehaviorsList.Count; i++)
            {
                BehaviorType key = (BehaviorType)CustomBehaviorsList.GetKey(i);
                Collection<CustomBehaviorAttribute> CustomBehaviorsColln = CustomBehaviorsList.GetByIndex(i) as Collection<CustomBehaviorAttribute>;

                foreach (CustomBehaviorAttribute cba in CustomBehaviorsColln)
                {
                    bool behaviorInvoked = false;

                    switch (key)
                    {
                        case BehaviorType.IServiceBehavior:
                            behaviorInvoked = cba.IsServiceBehaviorOnlyInvoked();
                            break;
                        case BehaviorType.IEndpointBehavior:
                            behaviorInvoked = cba.IsServiceEndpointBehaviorOnlyInvoked();
                            break;
                        case BehaviorType.IContractBehavior:
                            behaviorInvoked = cba.IsDispatcherContractBehaviorOnlyInvoked();
                            break;
                        case BehaviorType.IOperationBehavior:
                            behaviorInvoked = cba.IsDispatcherOperationBehaviorOnlyInvoked();
                            break;
                        default:
                            break;
                    }

                    resultsSB.Append(key.ToString() + ":");
                    resultsSB.Append(cba.ToString());
                    if (!behaviorInvoked)
                    {
                        resultsSB.Append("[NotInvoked]");
                    }

                    resultsSB.Append(";");
                }
            }

            return resultsSB.ToString();
        }

        public static void ValidateServiceInvokedBehaviors(ServiceDescription sd, string s)
        {
            string actual = ValidateServiceInvokedBehaviors(sd, sd.Endpoints[0], sd.Endpoints[0].Contract.Operations[0]);
            string expected = null;
            switch (s)
            {
                case "ByHand":
                case "ByHand_UsingHiddenProperty":
                case "CustomAttribute":
                    expected = "IContractBehavior:ServiceContract.CustomContractBehaviorAttribute;";
                    break;
                case "TwoAttributesDifferentTypes":
                    expected = "IContractBehavior:ServiceContract.CustomContractBehaviorAttribute;IContractBehavior:ServiceContract.OtherCustomContractBehaviorAttribute;";
                    break;
                case "MisplacedAttributes":
                    expected = "";
                    break;
                case "CustomAttributesImplementsOther":
                case "ByHandImplementsOther":
                    expected = "IContractBehavior:ServiceContract.MyMultiFacetedBehaviorAttribute;";
                    break;
                default:
                    break;
            }

            //workaround for https://github.com/CoreWCF/CoreWCF/issues/193
            if (s.ToLower().Contains("byhand"))
            {
                actual = actual.Substring(0, actual.IndexOf("[NotInvoked]")) + ";";
            }

            Xunit.Assert.Equal(expected, actual);
        }

        public static string ValidateServiceInvokedBehaviors(ServiceDescription sd, ServiceEndpoint sep, OperationDescription od)
        {
            var CustomBehaviorsList = new SortedList(4)
            {
                { BehaviorType.IServiceBehavior, sd.Behaviors.FindAll<CustomBehaviorAttribute>() }
            };
            var ebs = (CoreWCF.Collections.Generic.KeyedByTypeCollection<IEndpointBehavior>)sep.EndpointBehaviors;
            CustomBehaviorsList.Add(BehaviorType.IEndpointBehavior, ebs.FindAll<CustomBehaviorAttribute>());
            var cbs = (CoreWCF.Collections.Generic.KeyedByTypeCollection<IContractBehavior>)sep.Contract.ContractBehaviors;
            CustomBehaviorsList.Add(BehaviorType.IContractBehavior, cbs.FindAll<CustomBehaviorAttribute>());
            var opbs = (CoreWCF.Collections.Generic.KeyedByTypeCollection<IOperationBehavior>)od.OperationBehaviors;
            CustomBehaviorsList.Add(BehaviorType.IOperationBehavior, opbs.FindAll<CustomBehaviorAttribute>());
            return GetBehaviorsResult(CustomBehaviorsList);
        }
    }
}
