using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Runtime
{
    enum AsyncCompletionResult
    {
        /// <summary>
        /// Inidicates that the operation has been queued for completion.
        /// </summary>
        Queued,

        /// <summary>
        /// Indicates the operation has completed.
        /// </summary>
        Completed,
    }
}
