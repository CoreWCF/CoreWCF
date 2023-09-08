// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Claims
{
    public static class ClaimTypes
    {
        private const string claimTypeNamespace = XsiConstants.Namespace + "/claims";
        private const string anonymous = claimTypeNamespace + "/anonymous";
        private const string dns = claimTypeNamespace + "/dns";
        private const string email = claimTypeNamespace + "/emailaddress";
        private const string hash = claimTypeNamespace + "/hash";
        private const string name = claimTypeNamespace + "/name";
        private const string rsa = claimTypeNamespace + "/rsa";
        private const string sid = claimTypeNamespace + "/sid";
        private const string denyOnlySid = claimTypeNamespace + "/denyonlysid";
        private const string spn = claimTypeNamespace + "/spn";
        private const string system = claimTypeNamespace + "/system";
        private const string thumbprint = claimTypeNamespace + "/thumbprint";
        private const string upn = claimTypeNamespace + "/upn";
        private const string uri = claimTypeNamespace + "/uri";
        private const string x500DistinguishedName = claimTypeNamespace + "/x500distinguishedname";
        private const string givenname = claimTypeNamespace + "/givenname";
        private const string surname = claimTypeNamespace + "/surname";
        private const string streetaddress = claimTypeNamespace + "/streetaddress";
        private const string locality = claimTypeNamespace + "/locality";
        private const string stateorprovince = claimTypeNamespace + "/stateorprovince";
        private const string postalcode = claimTypeNamespace + "/postalcode";
        private const string country = claimTypeNamespace + "/country";
        private const string homephone = claimTypeNamespace + "/homephone";
        private const string otherphone = claimTypeNamespace + "/otherphone";
        private const string mobilephone = claimTypeNamespace + "/mobilephone";
        private const string dateofbirth = claimTypeNamespace + "/dateofbirth";
        private const string gender = claimTypeNamespace + "/gender";
        private const string ppid = claimTypeNamespace + "/privatepersonalidentifier";
        private const string webpage = claimTypeNamespace + "/webpage";
        private const string nameidentifier = claimTypeNamespace + "/nameidentifier";
        private const string authentication = claimTypeNamespace + "/authentication";
        private const string authorizationdecision = claimTypeNamespace + "/authorizationdecision";
        private const string role = claimTypeNamespace + "/role";
        private const string posixGroupId = claimTypeNamespace + "/posixgroupid";
        private const string posixGroupName = claimTypeNamespace + "/posixgroupname";
        private const string processId = claimTypeNamespace + "/processid";

        public static string Anonymous { get { return anonymous; } }
        public static string DenyOnlySid { get { return denyOnlySid; } }
        public static string Dns { get { return dns; } }
        public static string Email { get { return email; } }
        public static string Hash { get { return hash; } }
        public static string Name { get { return name; } }
        public static string Rsa { get { return rsa; } }
        public static string Sid { get { return sid; } }
        public static string Spn { get { return spn; } }
        public static string System { get { return system; } }
        public static string Thumbprint { get { return thumbprint; } }
        public static string Upn { get { return upn; } }
        public static string Uri { get { return uri; } }
        public static string X500DistinguishedName { get { return x500DistinguishedName; } }
        public static string NameIdentifier { get { return nameidentifier; } }
        public static string Authentication { get { return authentication; } }
        public static string AuthorizationDecision { get { return authorizationdecision; } }
        public static string Role { get { return role; } }
        public static string PosixGroupId { get { return posixGroupId; } }
        public static string PosixGroupName { get { return posixGroupName; } }
        public static string ProcessId { get { return processId; } }

        // used in info card 
        public static string GivenName { get { return givenname; } }
        public static string Surname { get { return surname; } }
        public static string StreetAddress { get { return streetaddress; } }
        public static string Locality { get { return locality; } }
        public static string StateOrProvince { get { return stateorprovince; } }
        public static string PostalCode { get { return postalcode; } }
        public static string Country { get { return country; } }
        public static string HomePhone { get { return homephone; } }
        public static string OtherPhone { get { return otherphone; } }
        public static string MobilePhone { get { return mobilephone; } }
        public static string DateOfBirth { get { return dateofbirth; } }
        public static string Gender { get { return gender; } }
        public static string PPID { get { return ppid; } }
        public static string Webpage { get { return webpage; } }
    }
}
