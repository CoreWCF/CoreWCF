using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.IdentityModel
{
    abstract class IdentityModelStrings
    {
        public abstract int Count { get; }
        public abstract string this[int index] { get; }
    }
}
