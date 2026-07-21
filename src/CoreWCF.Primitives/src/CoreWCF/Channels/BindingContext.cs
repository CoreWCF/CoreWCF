// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Text;
using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    public class BindingContext
    {
        public BindingContext(CustomBinding binding, BindingParameterCollection parameters)
            : this(binding, parameters, null, string.Empty)
        {
        }

        public BindingContext(CustomBinding binding, BindingParameterCollection parameters, Uri listenUriBaseAddress, string listenUriRelativeAddress)
        {
            if (binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binding));
            }
            if (listenUriRelativeAddress == null)
            {
                listenUriRelativeAddress = string.Empty;
            }

            Initialize(binding, binding.Elements, parameters, listenUriBaseAddress, listenUriRelativeAddress);
        }

        private BindingContext(CustomBinding binding,
               BindingElementCollection remainingBindingElements,
               BindingParameterCollection parameters,
               Uri listenUriBaseAddress,
               string listenUriRelativeAddress)
        {
            Initialize(binding, remainingBindingElements, parameters, listenUriBaseAddress, listenUriRelativeAddress);
        }

        private void Initialize(CustomBinding binding,
                BindingElementCollection remainingBindingElements,
                BindingParameterCollection parameters,
                Uri listenUriBaseAddress,
                string listenUriRelativeAddress)
        {
            Binding = binding;
            RemainingBindingElements = new BindingElementCollection(remainingBindingElements);
            BindingParameters = new BindingParameterCollection(parameters);
            ListenUriBaseAddress = listenUriBaseAddress;
            ListenUriRelativeAddress = listenUriRelativeAddress;
        }

        public CustomBinding Binding { get; private set; }

        public BindingParameterCollection BindingParameters { get; private set; }

        public Uri ListenUriBaseAddress { get; set; }

        public string ListenUriRelativeAddress { get; set; }

        public BindingElementCollection RemainingBindingElements { get; private set; }

        public IServiceDispatcher BuildNextServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            return RemoveNextElement().BuildServiceDispatcher<TChannel>(this, innerDispatcher);
        }

        public bool CanBuildNextServiceDispatcher<TChannel>() where TChannel : class, IChannel
        {
            BindingContext clone = Clone();
            return clone.RemoveNextElement().CanBuildServiceDispatcher<TChannel>(clone);
        }

        public T GetInnerProperty<T>() where T : class
        {
            if (RemainingBindingElements.Count == 0)
            {
                return null;
            }
            else
            {
                BindingContext clone = Clone();
                return clone.RemoveNextElement().GetProperty<T>(clone);
            }
        }

        public BindingContext Clone()
        {
            return new BindingContext(Binding, RemainingBindingElements, BindingParameters,
                ListenUriBaseAddress, ListenUriRelativeAddress);
        }

        private BindingElement RemoveNextElement()
        {
            BindingElement element = RemainingBindingElements.Remove<BindingElement>();
            if (element != null)
            {
                return element;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                SR.NoChannelBuilderAvailable, Binding.Name, Binding.Namespace)));
        }

        internal void ValidateBindingElementsConsumed()
        {
            if (RemainingBindingElements.Count != 0)
            {
                StringBuilder builder = new StringBuilder();
                foreach (BindingElement bindingElement in RemainingBindingElements)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                        builder.Append(" ");
                    }
                    string typeString = bindingElement.GetType().ToString();
                    builder.Append(typeString.Substring(typeString.LastIndexOf('.') + 1));
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.NotAllBindingElementsBuilt, builder.ToString())));
            }
        }
    }
}
