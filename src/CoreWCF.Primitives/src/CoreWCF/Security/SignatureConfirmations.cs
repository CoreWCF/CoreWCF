using System;

namespace CoreWCF.Security
{
    internal class SignatureConfirmations
    {
        private SignatureConfirmation[] confirmations;

        private struct SignatureConfirmation
        {
            public byte[] value;

            public SignatureConfirmation(byte[] value)
            {
                this.value = value;
            }
        }

        public SignatureConfirmations()
        {
            confirmations = new SignatureConfirmation[1];
            Count = 0;
        }

        public int Count { get; private set; }

        public void AddConfirmation(byte[] value, bool encrypted)
        {
            if (confirmations.Length == Count)
            {
                SignatureConfirmation[] newConfirmations = new SignatureConfirmation[Count * 2];
                Array.Copy(confirmations, 0, newConfirmations, 0, Count);
                confirmations = newConfirmations;
            }
            confirmations[Count] = new SignatureConfirmation(value);
            ++Count;
            this.IsMarkedForEncryption |= encrypted;
        }

        public void GetConfirmation(int index, out byte[] value, out bool encrypted)
        {
            if (index < 0 || index >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }

            value = confirmations[index].value;
            encrypted = this.IsMarkedForEncryption;
        }

        public bool IsMarkedForEncryption { get; private set; }
    }
}
