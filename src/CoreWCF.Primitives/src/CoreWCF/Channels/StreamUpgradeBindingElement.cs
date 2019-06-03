using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    public abstract class StreamUpgradeBindingElement : BindingElement
    {
        protected StreamUpgradeBindingElement()
        {
        }

        protected StreamUpgradeBindingElement(StreamUpgradeBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
        }

        public abstract StreamUpgradeProvider BuildServerStreamUpgradeProvider(BindingContext context);
    }
}
