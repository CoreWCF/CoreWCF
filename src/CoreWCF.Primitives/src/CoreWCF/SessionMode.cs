using System;

namespace CoreWCF
{
    public enum SessionMode
    {
        Allowed,
        Required,
        NotAllowed,
    }

    static class SessionModeHelper
    {
        public static bool IsDefined(SessionMode sessionMode)
        {
            return (sessionMode == SessionMode.NotAllowed ||
                    sessionMode == SessionMode.Allowed ||
                    sessionMode == SessionMode.Required);
        }
    }
}