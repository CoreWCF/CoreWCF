using System;
using System.Collections.ObjectModel;
using System.Xml;

namespace CoreWCF.Channels
{
    public abstract class TransportBindingElement : BindingElement
    {
        bool manualAddressing;
        long maxBufferPoolSize;
        long maxReceivedMessageSize;

        protected TransportBindingElement()
        {
            manualAddressing = TransportDefaults.ManualAddressing;
            maxBufferPoolSize = TransportDefaults.MaxBufferPoolSize;
            maxReceivedMessageSize = TransportDefaults.MaxReceivedMessageSize;
        }

        protected TransportBindingElement(TransportBindingElement elementToBeCloned)
        {
            manualAddressing = elementToBeCloned.manualAddressing;
            maxBufferPoolSize = elementToBeCloned.maxBufferPoolSize;
            maxReceivedMessageSize = elementToBeCloned.maxReceivedMessageSize;
        }

        [System.ComponentModel.DefaultValueAttribute(false)]
        public virtual bool ManualAddressing
        {
            get
            {
                return manualAddressing;
            }

            set
            {
                manualAddressing = value;
            }
        }

        public virtual long MaxBufferPoolSize
        {
            get
            {
                return maxBufferPoolSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBeNonNegative));
                }
                maxBufferPoolSize = value;
            }
        }

        [System.ComponentModel.DefaultValueAttribute((long)65536)]
        public virtual long MaxReceivedMessageSize
        {
            get
            {
                return maxReceivedMessageSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                        SR.ValueMustBePositive));
                }
                maxReceivedMessageSize = value;
            }
        }

        public abstract string Scheme { get; }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }


            // to cover all our bases, let's iterate through the BindingParameters to make sure
            // we haven't missed a query (since we're the Transport and we're at the bottom)
            Collection<BindingElement> bindingElements = new Collection<BindingElement>();
            foreach (var param in context.BindingParameters)
            {
                if (param is BindingElement)
                {
                    bindingElements.Add((BindingElement)param);
                }
            }

            T result = default(T);
            for (int i = 0; i < bindingElements.Count; i++)
            {
                result = bindingElements[i].GetIndividualProperty<T>();
                if (result != default(T))
                {
                    return result;
                }
            }

            if (typeof(T) == typeof(MessageVersion))
            {
                return (T)(object)TransportDefaults.GetDefaultMessageEncoderFactory().MessageVersion;
            }

            if (typeof(T) == typeof(XmlDictionaryReaderQuotas))
            {
                return (T)(object)new XmlDictionaryReaderQuotas();
            }

            return null;
        }

        protected override bool IsMatch(BindingElement b)
        {
            if (b == null)
            {
                return false;
            }
            TransportBindingElement transport = b as TransportBindingElement;
            if (transport == null)
            {
                return false;
            }
            if (maxBufferPoolSize != transport.MaxBufferPoolSize)
            {
                return false;
            }
            if (maxReceivedMessageSize != transport.MaxReceivedMessageSize)
            {
                return false;
            }
            return true;
        }
    }
}