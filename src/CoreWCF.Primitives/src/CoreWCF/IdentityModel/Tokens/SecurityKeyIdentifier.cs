// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SecurityKeyIdentifier : IEnumerable<SecurityKeyIdentifierClause>
    {
        private const int InitialSize = 2;
        private readonly List<SecurityKeyIdentifierClause> _clauses;

        public SecurityKeyIdentifier()
        {
            _clauses = new List<SecurityKeyIdentifierClause>(InitialSize);
        }

        public SecurityKeyIdentifier(params SecurityKeyIdentifierClause[] clauses)
        {
            if (clauses == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(clauses));
            }
            _clauses = new List<SecurityKeyIdentifierClause>(clauses.Length);
            for (int i = 0; i < clauses.Length; i++)
            {
                Add(clauses[i]);
            }
        }

        public SecurityKeyIdentifierClause this[int index]
        {
            get { return _clauses[index]; }
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
            get { return _clauses.Count; }
        }

        public bool IsReadOnly { get; private set; }

        public void Add(SecurityKeyIdentifierClause clause)
        {
            if (IsReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
            if (clause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(clause)));
            }
            _clauses.Add(clause);
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
            if (!TryFind<TClause>(out TClause clause))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.NoKeyIdentifierClauseFound, typeof(TClause)), "TClause"));
            }
            return clause;
        }

        public IEnumerator<SecurityKeyIdentifierClause> GetEnumerator()
        {
            return _clauses.GetEnumerator();
        }

        public void MakeReadOnly()
        {
            IsReadOnly = true;
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
            for (int i = 0; i < _clauses.Count; i++)
            {
                if (_clauses[i] is TClause c)
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
