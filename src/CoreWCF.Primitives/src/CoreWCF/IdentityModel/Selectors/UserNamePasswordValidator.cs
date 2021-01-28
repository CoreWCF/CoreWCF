// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public abstract void Validate(string userName, string password);

        private class NoneUserNamePasswordValidator : UserNamePasswordValidator
        {
            public override void Validate(string userName, string password)
            {
            }
        }
    }
}
