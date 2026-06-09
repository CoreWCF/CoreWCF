// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CoreWCF.Channels
{
    // Track channels and (optionally) associated state
    internal class ChannelTracker<TChannel, TState> : CommunicationObject where TChannel : IChannel where TState : class
    {
        private readonly Dictionary<TChannel, TState> _receivers;
        private readonly EventHandler _onInnerChannelClosed;
        private readonly EventHandler _onInnerChannelFaulted;

        public ChannelTracker()
        {
            _receivers = new Dictionary<TChannel, TState>();
            _onInnerChannelClosed = new EventHandler(OnInnerChannelClosed);
            _onInnerChannelFaulted = new EventHandler(OnInnerChannelFaulted);
        }

        public void Add(TChannel channel, TState channelReceiver)
        {
            bool abortChannel = false;
            lock (_receivers)
            {
                if (State != CommunicationState.Opened)
                {
                    abortChannel = true;
                }
                else
                {
                    _receivers.Add(channel, channelReceiver);
                }
            }

            if (abortChannel)
            {
                channel.Abort();
            }
        }

        public void PrepareChannel(TChannel channel)
        {
            channel.Faulted += _onInnerChannelFaulted;
            channel.Closed += _onInnerChannelClosed;
        }

        private void OnInnerChannelFaulted(object sender, EventArgs e)
        {
            ((TChannel)sender).Abort();
        }

        private void OnInnerChannelClosed(object sender, EventArgs e)
        {
            // remove the channel from our tracking dictionary
            TChannel channel = (TChannel)sender;
            Remove(channel);
            channel.Faulted -= _onInnerChannelFaulted;
            channel.Closed -= _onInnerChannelClosed;
        }

        public bool Remove(TChannel channel)
        {
            lock (_receivers)
            {
                return _receivers.Remove(channel);
            }
        }

        private TChannel[] GetChannels()
        {
            lock (_receivers)
            {
                TChannel[] channels = new TChannel[_receivers.Keys.Count];
                _receivers.Keys.CopyTo(channels, 0);
                _receivers.Clear();
                return channels;
            }
        }

        protected override void OnAbort()
        {
            TChannel[] channels = GetChannels();
            for (int i = 0; i < channels.Length; i++)
            {
                channels[i].Abort();
            }
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            TChannel[] channels = GetChannels();
            for (int i = 0; i < channels.Length; i++)
            {
                bool success = false;
                try
                {
                    await channels[i].CloseAsync(token);
                    success = true;
                }
                catch (CommunicationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (OperationCanceledException e)
                {
                    //if (TD.CloseTimeoutIsEnabled())
                    //{
                    //    TD.CloseTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                catch (TimeoutException e)
                {
                    //if (TD.CloseTimeoutIsEnabled())
                    //{
                    //    TD.CloseTimeout(e.Message);
                    //}
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                finally
                {
                    if (!success)
                    {
                        channels[i].Abort();
                    }
                }
            }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return ServiceDefaults.CloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return ServiceDefaults.OpenTimeout; }
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
