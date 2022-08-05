// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    public sealed class MsmqTransportBindingElement : MsmqBindingElementBase
    {
        private int _maxPoolSize = MsmqDefaults.MaxPoolSize;
        private bool _useActiveDirectory = MsmqDefaults.UseActiveDirectory;

        public MsmqTransportBindingElement() { }

        private MsmqTransportBindingElement(MsmqTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _useActiveDirectory = elementToBeCloned._useActiveDirectory;
            _maxPoolSize = elementToBeCloned._maxPoolSize;
        }


        public int MaxPoolSize
        {
            get
            {
                return _maxPoolSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ArgumentOutOfRangeException("value", value, SR.MsmqNonNegativeArgumentExpected));
                }
                _maxPoolSize = value;
            }
        }

        public override string Scheme
        {
            get
            {
                return "net.msmq";
            }
        }

        public bool UseActiveDirectory
        {
            get
            {
                return _useActiveDirectory;
            }
            set
            {
                _useActiveDirectory = value;
            }
        }

        public override BindingElement Clone()
        {
            return new MsmqTransportBindingElement(this);
        }
    }
}
