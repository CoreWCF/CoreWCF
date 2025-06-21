// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF;
using CoreWCF.Channels;

namespace ServiceContract
{
    [ServiceContract(SessionMode = SessionMode.Required)]
    public interface IVerifyWebSockets
    {
        // This operation will only get called when using WebSockets and CreateNotificationOnConnection is set to true on the binding WebSocketSettings
        [OperationContract(Action = WebSocketTransportSettings.ConnectionOpenedAction, IsOneWay = true, IsInitiating = true)]
        void ForceWebSocketsUse();
        [OperationContract()]
        bool ValidateWebSocketsUsed();
    }
}
