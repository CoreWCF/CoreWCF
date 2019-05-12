using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.IdentityModel
{
    abstract class IdentityModelStrings
    {
        public abstract int Count { get; }
        public abstract string this[int index] { get; }
    }
}
