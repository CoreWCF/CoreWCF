using System;
using System.Collections.Generic;
using System.Text;

using System.Threading.Tasks;

namespace CoreWCF.Security
{
    internal interface ISecurityCommunicationObject
    {
        TimeSpan DefaultOpenTimeout { get; }
        TimeSpan DefaultCloseTimeout { get; }
        void OnAbort();
        Task OnCloseAsync(TimeSpan timeout);
        void OnClosed();
        void OnClosing();
        void OnFaulted();
        Task OnOpenAsync(TimeSpan timeout);
        void OnOpened();
        void OnOpening();
    }
}