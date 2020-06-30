using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace CoreWCF.IdentityModel
{

    internal abstract class TransformFactory
    {
        public abstract Transform CreateTransform(string transformAlgorithmUri);
    }
}
