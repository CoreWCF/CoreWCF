using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.WebSockets;
using System.Security.Principal;

namespace CoreWCF.Channels
{
    internal class ServiceWebSocketContext : WebSocketContext
    {
        WebSocketContext _context;
        IPrincipal _user;

        public ServiceWebSocketContext(WebSocketContext context, IPrincipal user)
        {
            Fx.Assert(context != null, "context should not be null.");
            _context = context;
            _user = user;
        }

        public override CookieCollection CookieCollection
        {
            get { return _context.CookieCollection; }
        }

        public override NameValueCollection Headers
        {
            get { return _context.Headers; }
        }

        public override bool IsAuthenticated
        {
            get { return _user != null ? _user.Identity != null && _user.Identity.IsAuthenticated : _context.IsAuthenticated; }
        }

        public override bool IsLocal
        {
            get { return _context.IsLocal; }
        }

        public override bool IsSecureConnection
        {
            get { return _context.IsSecureConnection; }
        }

        public override Uri RequestUri
        {
            get { return _context.RequestUri; }
        }

        public override string SecWebSocketKey
        {
            get { return _context.SecWebSocketKey; }
        }

        public override string Origin
        {
            get { return _context.Origin; }
        }

        public override IEnumerable<string> SecWebSocketProtocols
        {
            get { return _context.SecWebSocketProtocols; }
        }

        public override string SecWebSocketVersion
        {
            get { return _context.SecWebSocketVersion; }
        }

        public override IPrincipal User
        {
            get { return _user != null ? _user : _context.User; }
        }

        public override WebSocket WebSocket
        {
            get { throw Fx.Exception.AsError(new InvalidOperationException(SR.WebSocketContextWebSocketCannotBeAccessedError)); }
        }
    }
}