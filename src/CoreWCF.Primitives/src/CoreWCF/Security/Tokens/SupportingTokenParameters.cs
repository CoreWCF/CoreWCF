// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace CoreWCF.Security.Tokens
{
    public class SupportingTokenParameters
    {
        private readonly Collection<SecurityTokenParameters> signedEncrypted = new Collection<SecurityTokenParameters>();
        private readonly Collection<SecurityTokenParameters> endorsing = new Collection<SecurityTokenParameters>();
        private readonly Collection<SecurityTokenParameters> signedEndorsing = new Collection<SecurityTokenParameters>();

        private SupportingTokenParameters(SupportingTokenParameters other)
        {
            if (other == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("other");
            }

            foreach (SecurityTokenParameters p in other.Signed)
            {
                Signed.Add((SecurityTokenParameters)p.Clone());
            }

            foreach (SecurityTokenParameters p in other.signedEncrypted)
            {
                signedEncrypted.Add((SecurityTokenParameters)p.Clone());
            }

            foreach (SecurityTokenParameters p in other.endorsing)
            {
                endorsing.Add((SecurityTokenParameters)p.Clone());
            }

            foreach (SecurityTokenParameters p in other.signedEndorsing)
            {
                signedEndorsing.Add((SecurityTokenParameters)p.Clone());
            }
        }

        public SupportingTokenParameters()
        {
            // empty
        }

        public Collection<SecurityTokenParameters> Endorsing => endorsing;

        public Collection<SecurityTokenParameters> SignedEndorsing => signedEndorsing;

        public Collection<SecurityTokenParameters> Signed { get; } = new Collection<SecurityTokenParameters>();

        public Collection<SecurityTokenParameters> SignedEncrypted => signedEncrypted;

        public void SetKeyDerivation(bool requireDerivedKeys)
        {
            foreach (SecurityTokenParameters t in endorsing)
            {
                if (t.HasAsymmetricKey)
                {
                    t.RequireDerivedKeys = false;
                }
                else
                {
                    t.RequireDerivedKeys = requireDerivedKeys;
                }
            }
            foreach (SecurityTokenParameters t in signedEndorsing)
            {
                if (t.HasAsymmetricKey)
                {
                    t.RequireDerivedKeys = false;
                }
                else
                {
                    t.RequireDerivedKeys = requireDerivedKeys;
                }
            }
        }

        internal bool IsSetKeyDerivation(bool requireDerivedKeys)
        {
            foreach (SecurityTokenParameters t in endorsing)
            {
                if (t.RequireDerivedKeys != requireDerivedKeys)
                {
                    return false;
                }
            }

            foreach (SecurityTokenParameters t in signedEndorsing)
            {
                if (t.RequireDerivedKeys != requireDerivedKeys)
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            int k;

            if (endorsing.Count == 0)
            {
                sb.AppendLine("No endorsing tokens.");
            }
            else
            {
                for (k = 0; k < endorsing.Count; k++)
                {
                    sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "Endorsing[{0}]", k.ToString(CultureInfo.InvariantCulture)));
                    sb.AppendLine("  " + endorsing[k].ToString().Trim().Replace("\n", "\n  "));
                }
            }

            if (Signed.Count == 0)
            {
                sb.AppendLine("No signed tokens.");
            }
            else
            {
                for (k = 0; k < Signed.Count; k++)
                {
                    sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "Signed[{0}]", k.ToString(CultureInfo.InvariantCulture)));
                    sb.AppendLine("  " + Signed[k].ToString().Trim().Replace("\n", "\n  "));
                }
            }

            if (signedEncrypted.Count == 0)
            {
                sb.AppendLine("No signed encrypted tokens.");
            }
            else
            {
                for (k = 0; k < signedEncrypted.Count; k++)
                {
                    sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "SignedEncrypted[{0}]", k.ToString(CultureInfo.InvariantCulture)));
                    sb.AppendLine("  " + signedEncrypted[k].ToString().Trim().Replace("\n", "\n  "));
                }
            }

            if (signedEndorsing.Count == 0)
            {
                sb.AppendLine("No signed endorsing tokens.");
            }
            else
            {
                for (k = 0; k < signedEndorsing.Count; k++)
                {
                    sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "SignedEndorsing[{0}]", k.ToString(CultureInfo.InvariantCulture)));
                    sb.AppendLine("  " + signedEndorsing[k].ToString().Trim().Replace("\n", "\n  "));
                }
            }

            return sb.ToString().Trim();
        }

        public SupportingTokenParameters Clone()
        {
            SupportingTokenParameters parameters = CloneCore();
            /* if (parameters == null || parameters.GetType() != this.GetType())
             {
                 TraceUtility.TraceEvent(
                     TraceEventType.Error, 
                     TraceCode.Security, 
                     SR.GetString(SR.CloneNotImplementedCorrectly, new object[] { this.GetType(), (parameters != null) ? parameters.ToString() : "null" }));
             }*/

            return parameters;
        }

        protected virtual SupportingTokenParameters CloneCore()
        {
            return new SupportingTokenParameters(this);
        }

        internal bool IsEmpty()
        {
            return Signed.Count == 0 && signedEncrypted.Count == 0 && endorsing.Count == 0 && signedEndorsing.Count == 0;
        }
    }
}
