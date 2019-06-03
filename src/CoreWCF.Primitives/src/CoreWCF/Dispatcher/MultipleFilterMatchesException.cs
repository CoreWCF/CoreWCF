using System;
using System.Collections.ObjectModel;

namespace CoreWCF.Dispatcher
{
    //[Serializable]
    internal class MultipleFilterMatchesException : Exception //SystemException
    {
        //[NonSerialized]
        Collection<MessageFilter> filters;

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
            this.filters = filters;
        }

        public Collection<MessageFilter> Filters
        {
            get
            {
                return filters;
            }
        }
    }

}