namespace CoreWCF
{
    public interface ICommunicationObject
    {
        CommunicationState State { get; }
        event System.EventHandler Closed;
        event System.EventHandler Closing;
        event System.EventHandler Faulted;
        event System.EventHandler Opened;
        event System.EventHandler Opening;
        void Abort();
        System.Threading.Tasks.Task CloseAsync();
        System.Threading.Tasks.Task CloseAsync(System.Threading.CancellationToken token);
        System.Threading.Tasks.Task OpenAsync();
        System.Threading.Tasks.Task OpenAsync(System.Threading.CancellationToken token);
        //System.IAsyncResult BeginClose(System.AsyncCallback callback, object state); // No more APM
        //System.IAsyncResult BeginClose(System.TimeSpan timeout, System.AsyncCallback callback, object state);
        //System.IAsyncResult BeginOpen(System.AsyncCallback callback, object state);
        //System.IAsyncResult BeginOpen(System.TimeSpan timeout, System.AsyncCallback callback, object state);

        //void Close(); // No more sync
        //void Close(System.TimeSpan timeout);
        //void EndClose(System.IAsyncResult result); // No more APM
        //void EndOpen(System.IAsyncResult result);
        //void Open(); // No more sync
        //void Open(System.TimeSpan timeout);
    }
}