using System;
using System.Globalization;
using System.Text;
using CoreWCF.Description;

namespace CoreWCF.Channels
{
    public class BindingContext
    {
        CustomBinding _binding;
        BindingParameterCollection _bindingParameters;
        Uri _listenUriBaseAddress;
        ListenUriMode _listenUriMode;
        string _listenUriRelativeAddress;
        BindingElementCollection _remainingBindingElements;  // kept to ensure each BE builds itself once

        public BindingContext(CustomBinding binding, BindingParameterCollection parameters)
            : this(binding, parameters, null, string.Empty, ListenUriMode.Explicit)
        {
        }

        public BindingContext(CustomBinding binding, BindingParameterCollection parameters, Uri listenUriBaseAddress, string listenUriRelativeAddress, ListenUriMode listenUriMode)
        {
            if (binding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(binding));
            }
            if (listenUriRelativeAddress == null)
            {
                listenUriRelativeAddress = string.Empty;
            }
            if (!ListenUriModeHelper.IsDefined(listenUriMode))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(listenUriMode)));
            }

            Initialize(binding, binding.Elements, parameters, listenUriBaseAddress, listenUriRelativeAddress, listenUriMode);
        }

        BindingContext(CustomBinding binding,
               BindingElementCollection remainingBindingElements,
               BindingParameterCollection parameters,
               Uri listenUriBaseAddress,
               string listenUriRelativeAddress,
               ListenUriMode listenUriMode)
        {
            Initialize(binding, remainingBindingElements, parameters, listenUriBaseAddress, listenUriRelativeAddress, listenUriMode);
        }

        private void Initialize(CustomBinding binding,
                BindingElementCollection remainingBindingElements,
                BindingParameterCollection parameters,
                Uri listenUriBaseAddress,
                string listenUriRelativeAddress,
                ListenUriMode listenUriMode)
        {
            _binding = binding;

            _remainingBindingElements = new BindingElementCollection(remainingBindingElements);
            _bindingParameters = new BindingParameterCollection(parameters);
            _listenUriBaseAddress = listenUriBaseAddress;
            _listenUriRelativeAddress = listenUriRelativeAddress;
            _listenUriMode = listenUriMode;
        }

        public CustomBinding Binding => _binding;

        public BindingParameterCollection BindingParameters => _bindingParameters;

        public Uri ListenUriBaseAddress
        {
            get { return _listenUriBaseAddress; }
            set { _listenUriBaseAddress = value; }
        }

        public ListenUriMode ListenUriMode
        {
            get { return _listenUriMode; }
            set { _listenUriMode = value; }
        }

        public string ListenUriRelativeAddress
        {
            get { return _listenUriRelativeAddress; }
            set { _listenUriRelativeAddress = value; }
        }

        public BindingElementCollection RemainingBindingElements => _remainingBindingElements;

        public T GetInnerProperty<T>()
            where T : class
        {
            if (_remainingBindingElements.Count == 0)
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
            return new BindingContext(_binding, _remainingBindingElements, _bindingParameters,
                _listenUriBaseAddress, _listenUriRelativeAddress, _listenUriMode);
        }

        BindingElement RemoveNextElement()
        {
            BindingElement element = _remainingBindingElements.Remove<BindingElement>();
            if (element != null)
                return element;
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                SR.NoChannelBuilderAvailable, _binding.Name, _binding.Namespace)));
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