﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    // TODO: Make internal again
    public interface IChannelBindingProvider
    {
        void EnableChannelBindingSupport();
        bool IsChannelBindingSupportEnabled { get; }
    }
}
