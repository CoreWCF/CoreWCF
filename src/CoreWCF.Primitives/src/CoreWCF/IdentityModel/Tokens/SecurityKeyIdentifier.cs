using CoreWCF;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CoreWCF.IdentityModel.Tokens
{
    internal class SecurityKeyIdentifier : IEnumerable<SecurityKeyIdentifierClause>
    {
        const int InitialSize = 2;
        readonly List<SecurityKeyIdentifierClause> clauses;
        bool isReadOnly;

        public SecurityKeyIdentifier()
        {
            clauses = new List<SecurityKeyIdentifierClause>(InitialSize);
        }

        public SecurityKeyIdentifier(params SecurityKeyIdentifierClause[] clauses)
        {
            if (clauses == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("clauses");
            }
            this.clauses = new List<SecurityKeyIdentifierClause>(clauses.Length);
            for (int i = 0; i < clauses.Length; i++)
            {
                Add(clauses[i]);
            }
        }

        public SecurityKeyIdentifierClause this[int index]
        {
            get { return clauses[index]; }
        }

        public bool CanCreateKey
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    if (this[i].CanCreateKey)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public int Count
        {
            get { return clauses.Count; }
        }

        public bool IsReadOnly
        {
            get { return isReadOnly; }
        }

        public void Add(SecurityKeyIdentifierClause clause)
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
            if (clause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("clause"));
            }
            clauses.Add(clause);
        }

        public SecurityKey CreateKey()
        {
            for (int i = 0; i < Count; i++)
            {
                if (this[i].CanCreateKey)
                {
                    return this[i].CreateKey();
                }
            }
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.KeyIdentifierCannotCreateKey));
        }

        public TClause Find<TClause>() where TClause : SecurityKeyIdentifierClause
        {
            TClause clause;
            if (!TryFind<TClause>(out clause))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.NoKeyIdentifierClauseFound, typeof(TClause)), "TClause"));
            }
            return clause;
        }

        public IEnumerator<SecurityKeyIdentifierClause> GetEnumerator()
        {
            return clauses.GetEnumerator();
        }

        public void MakeReadOnly()
        {
            isReadOnly = true;
        }

        public override string ToString()
        {
            using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                writer.WriteLine("SecurityKeyIdentifier");
                writer.WriteLine("    (");
                writer.WriteLine("    IsReadOnly = {0},", IsReadOnly);
                writer.WriteLine("    Count = {0}{1}", Count, Count > 0 ? "," : "");
                for (int i = 0; i < Count; i++)
                {
                    writer.WriteLine("    Clause[{0}] = {1}{2}", i, this[i], i < Count - 1 ? "," : "");
                }
                writer.WriteLine("    )");
                return writer.ToString();
            }
        }

        public bool TryFind<TClause>(out TClause clause) where TClause : SecurityKeyIdentifierClause
        {
            for (int i = 0; i < clauses.Count; i++)
            {
                TClause c = clauses[i] as TClause;
                if (c != null)
                {
                    clause = c;
                    return true;
                }
            }
            clause = null;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

}
