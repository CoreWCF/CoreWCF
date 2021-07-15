using System.ServiceModel;

namespace DesktopServer
{
    internal class CustomUserNamePasswordValidator : System.IdentityModel.Selectors.UserNamePasswordValidator
    {
        public override void Validate(string userName, string password)
        {
            bool valid = userName.ToLowerInvariant().EndsWith("valid")
                && password.ToLowerInvariant().EndsWith("valid");
            if (!valid)
            {
                throw new FaultException("Unknown Username or Incorrect Password");
            }
        }

        public static void AddToHost(ServiceHostBase host)
        {
            host.Credentials.UserNameAuthentication.UserNamePasswordValidationMode
                = System.ServiceModel.Security.UserNamePasswordValidationMode.Custom;
            host.Credentials.UserNameAuthentication.CustomUserNamePasswordValidator
                = new CustomUserNamePasswordValidator();
        }

    }
}
