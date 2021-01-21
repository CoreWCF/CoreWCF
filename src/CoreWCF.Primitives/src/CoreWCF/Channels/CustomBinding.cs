// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace CoreWCF.Channels
{
    public class CustomBinding : Binding
    {
        private BindingElementCollection _bindingElements = new BindingElementCollection();

        public CustomBinding()
        {
        }

        public CustomBinding(params BindingElement[] bindingElementsInTopDownChannelStackOrder)
        {
            if (bindingElementsInTopDownChannelStackOrder == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingElementsInTopDownChannelStackOrder));
            }

            foreach (BindingElement element in bindingElementsInTopDownChannelStackOrder)
            {
                _bindingElements.Add(element);
            }
        }

        public CustomBinding(string name, string ns, params BindingElement[] bindingElementsInTopDownChannelStackOrder)
            : base(name, ns)
        {
            if (bindingElementsInTopDownChannelStackOrder == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingElementsInTopDownChannelStackOrder));
            }

            foreach (BindingElement element in bindingElementsInTopDownChannelStackOrder)
            {
                _bindingElements.Add(element);
            }
        }

        public CustomBinding(IEnumerable<BindingElement> bindingElementsInTopDownChannelStackOrder)
        {
            if (bindingElementsInTopDownChannelStackOrder == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bindingElementsInTopDownChannelStackOrder));
            }

            foreach (BindingElement element in bindingElementsInTopDownChannelStackOrder)
            {
                _bindingElements.Add(element);
            }
        }

        public CustomBinding(Binding binding)
    : this(binding, SafeCreateBindingElements(binding))
        {
        }

        private static BindingElementCollection SafeCreateBindingElements(Binding binding)
        {
            if (binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binding));
            }
            return binding.CreateBindingElements();
        }

        internal CustomBinding(Binding binding, BindingElementCollection elements)
        {
            if (binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binding));
            }
            if (elements == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(elements));
            }

            Name = binding.Name;
            Namespace = binding.Namespace;
            CloseTimeout = binding.CloseTimeout;
            OpenTimeout = binding.OpenTimeout;
            ReceiveTimeout = binding.ReceiveTimeout;
            SendTimeout = binding.SendTimeout;

            for (int i = 0; i < elements.Count; i++)
            {
                _bindingElements.Add(elements[i]);
            }
        }

        public BindingElementCollection Elements
        {
            get
            {
                return _bindingElements;
            }
        }

        public override BindingElementCollection CreateBindingElements()
        {
            return _bindingElements.Clone();
        }

        public override string Scheme
        {
            get
            {
                TransportBindingElement transport = _bindingElements.Find<TransportBindingElement>();
                if (transport == null)
                {
                    return string.Empty;
                }

                return transport.Scheme;
            }
        }
    }
}