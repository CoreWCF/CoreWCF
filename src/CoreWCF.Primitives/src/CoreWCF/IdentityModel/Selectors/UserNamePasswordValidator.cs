// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Selectors
{
    // TODO: Either consider moving this back to System.IdentityModel.Selectors and/or move to ServiceModel and make async
    public abstract class UserNamePasswordValidator
    {
        private static UserNamePasswordValidator none;

        public static UserNamePasswordValidator None
        {
            get
            {
                if (none == null)
                {
                    none = new NoneUserNamePasswordValidator();
                }

                return none;
            }
        }

        public abstract void Validate(string userName, string password);

        private class NoneUserNamePasswordValidator : UserNamePasswordValidator
        {
            public override void Validate(string userName, string password)
            {
            }
        }
    }
}
