// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;

namespace CoreWCF.IdentityModel.Selectors
{
    // TODO: Either consider moving this back to System.IdentityModel.Selectors and/or move to ServiceModel and make async
    public abstract class UserNamePasswordValidator
    {
        private static UserNamePasswordValidator s_none;

        public static UserNamePasswordValidator None
        {
            get
            {
                if (s_none == null)
                {
                    s_none = new NoneUserNamePasswordValidator();
                }

                return s_none;
            }
        }

        [Obsolete("Implementers should override ValidateAsync.")]
        public virtual void Validate(string userName, string password) => throw new NotImplementedException(SR.SynchronousUserNamePasswordValidationIsDeprecated);

        public virtual Task ValidateAsync(string userName, string password)
        {
            Validate(userName, password);
            return Task.CompletedTask;
        }

        private class NoneUserNamePasswordValidator : UserNamePasswordValidator
        {
            public override Task ValidateAsync(string userName, string password) => Task.CompletedTask;
        }
    }
}
