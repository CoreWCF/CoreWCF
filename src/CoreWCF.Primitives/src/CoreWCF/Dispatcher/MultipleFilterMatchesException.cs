// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;

namespace CoreWCF.Dispatcher
{
    //[Serializable]
    internal class MultipleFilterMatchesException : Exception //SystemException
    {

        //protected MultipleFilterMatchesException(SerializationInfo info, StreamingContext context)
        //    : base(info, context)
        //{
        //    this.filters = null;
        //}

        public MultipleFilterMatchesException()
            : this(SR.FilterMultipleMatches)
        {
        }

        public MultipleFilterMatchesException(string message)
            : this(message, null, null)
        {
        }

        public MultipleFilterMatchesException(string message, Exception innerException)
            : this(message, innerException, null)
        {
        }

        public MultipleFilterMatchesException(string message, Collection<MessageFilter> filters)
            : this(message, null, filters)
        {
        }

        public MultipleFilterMatchesException(string message, Exception innerException, Collection<MessageFilter> filters)
            : base(message, innerException)
        {
            Filters = filters;
        }

        public Collection<MessageFilter> Filters { get; }
    }

}