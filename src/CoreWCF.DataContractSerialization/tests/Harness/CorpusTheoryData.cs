// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.DataContractSerialization.TestCorpus;
using Xunit;

namespace CoreWCF.DataContractSerialization.Tests.Harness
{
    /// <summary>
    /// Theory data for the golden-record tests.
    /// </summary>
    /// <remarks>
    /// Yields the case <em>id</em> rather than the <see cref="CorpusCase"/> itself. xUnit needs
    /// theory data to be serializable so individual cases can be re-run from a test explorer, and a
    /// CorpusCase carries a Func&lt;object&gt;. Keying on a string also keeps test identities stable
    /// when the catalog is reordered.
    /// </remarks>
    public static class CorpusTheoryData
    {
        public static TheoryData<string> CaseIds
        {
            get
            {
                TheoryData<string> data = new TheoryData<string>();
                foreach (string id in CorpusCatalog.Ids)
                {
                    data.Add(id);
                }

                return data;
            }
        }
    }
}
