// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TODO: Help

using System.Threading.Tasks;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal class HelpOperationInvoker : IOperationInvoker
    {
        private readonly HelpPage _helpPage;

        public const string OperationName = "HelpPageInvoke";

        public HelpOperationInvoker(HelpPage helpPage)
        {
            _helpPage = helpPage;
        }

        public object[] AllocateInputs()
        {
            return new object[] { null };
        }

        public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
        {
            UriTemplateMatch match = (UriTemplateMatch)OperationContext.Current.IncomingMessageProperties[IncomingWebRequestContext.UriTemplateMatchResultsPropertyName];
            return new ValueTask<(object, object[])>((_helpPage.Invoke(match), null));
        }

        public bool IsSynchronous => true;
    }
}
