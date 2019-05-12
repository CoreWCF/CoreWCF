using CoreWCF.Runtime;
using CoreWCF;
using System;
using System.Diagnostics;
using System.Threading;

namespace CoreWCF.Channels
{
    delegate void ConnectionModeCallback(ConnectionModeReader connectionModeReader);

    sealed class ConnectionModeReader : InitialServerConnectionReader
    {
        Exception readException;
        ServerModeDecoder decoder;
        byte[] buffer;
        int offset;
        int size;
        ConnectionModeCallback callback;
        static Action<object> readCallback;
        TimeoutHelper receiveTimeoutHelper;

        public ConnectionModeReader(IConnection connection, ConnectionModeCallback callback, ConnectionClosedCallback closedCallback)
            : base(connection, closedCallback)
        {
            this.callback = callback;
        }

        public int BufferOffset
        {
            get { return offset; }
        }

        public int BufferSize
        {
            get { return size; }
        }

        public long StreamPosition
        {
            get { return decoder.StreamPosition; }
        }

        public TimeSpan GetRemainingTimeout()
        {
            return receiveTimeoutHelper.RemainingTime();
        }

        void Complete(Exception e)
        {
            // exception will be logged by the caller
            readException = e;
            Complete();
        }

        void Complete()
        {
            callback(this);
        }

        bool ContinueReading()
        {
            for (;;)
            {
                if (size == 0)
                {
                    if (readCallback == null)
                    {
                        readCallback = ReadCallback;
                    }

                    if (Connection.BeginRead(0, Connection.AsyncReadBufferSize, GetRemainingTimeout(),
                        readCallback, this) == AsyncCompletionResult.Queued)
                    {
                        break;
                    }
                    if (!GetReadResult()) // we're at EOF, bail
                    {
                        return false;
                    }
                }

                for (;;)
                {
                    int bytesDecoded;
                    try
                    {
                        bytesDecoded = decoder.Decode(buffer, offset, size);
                    }
                    catch (CommunicationException e)
                    {
                        // see if we need to send back a framing fault
                        string framingFault;
                        if (FramingEncodingString.TryGetFaultString(e, out framingFault))
                        {
                            byte[] drainBuffer = new byte[128];
                            InitialServerConnectionReader.SendFault(
                                Connection, framingFault, drainBuffer, GetRemainingTimeout(),
                                MaxViaSize + MaxContentTypeSize);
                            base.Close(GetRemainingTimeout());
                        }
                        throw;
                    }

                    if (bytesDecoded > 0)
                    {
                        offset += bytesDecoded;
                        size -= bytesDecoded;
                    }
                    if (decoder.CurrentState == ServerModeDecoder.State.Done)
                    {
                        return true;
                    }
                    if (size == 0)
                    {
                        break;
                    }
                }
            }

            return false;
        }

        static void ReadCallback(object state)
        {
            ConnectionModeReader reader = (ConnectionModeReader)state;
            bool completeSelf = false;
            Exception completionException = null;
            try
            {
                if (reader.GetReadResult())
                {
                    completeSelf = reader.ContinueReading();
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                completeSelf = true;
                completionException = e;
            }

            if (completeSelf)
            {
                reader.Complete(completionException);
            }
        }

        bool GetReadResult()
        {
            offset = 0;
            size = Connection.EndRead();
            if (size == 0)
            {
                if (decoder.StreamPosition == 0) // client timed out a cached connection
                {
                    base.Close(GetRemainingTimeout());
                    return false;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(decoder.CreatePrematureEOFException());
                }
            }

            // restore ExceptionEventType to Error after the initial read for cached connections
            Connection.ExceptionEventType = TraceEventType.Error;

            if (buffer == null)
            {
                buffer = Connection.AsyncReadBuffer;
            }

            return true;
        }

        public FramingMode GetConnectionMode()
        {
            if (readException != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(readException, Connection.ExceptionEventType);
            }

            return decoder.Mode;
        }

        public void StartReading(TimeSpan receiveTimeout, Action connectionDequeuedCallback)
        {
            decoder = new ServerModeDecoder();
            receiveTimeoutHelper = new TimeoutHelper(receiveTimeout);
            ConnectionDequeuedCallback = connectionDequeuedCallback;
            bool completeSelf = false;
            Exception completionException = null;
            try
            {
                completeSelf = ContinueReading();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                completeSelf = true;
                completionException = e;
            }

            if (completeSelf)
            {
                Complete(completionException);
            }
        }
    }

}
