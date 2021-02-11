// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Collections.Generic;

namespace CoreWCF.Channels
{
    // Some binding elements can sometimes consume extra information when building factories.
    // BindingParameterCollection is a collection of objects with this extra information.
    // See comments in SecurityBindingElement and TransactionFlowBindingElement for examples
    // of binding elements that go looking for certain data in this collection.
    public class BindingParameterCollection : KeyedByTypeCollection<object>
    {
        public BindingParameterCollection() { }

        internal BindingParameterCollection(params object[] parameters)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                Add(parameters[i]);
            }
        }

        internal BindingParameterCollection(BindingParameterCollection parameters)
        {
            if (parameters == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parameters));
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                Add(parameters[i]);
            }
        }
    }
}