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

        static public string Anonymous { get { return anonymous; } }
        static public string DenyOnlySid { get { return denyOnlySid; } }
        static public string Dns { get { return dns; } }
        static public string Email { get { return email; } }
        static public string Hash { get { return hash; } }
        static public string Name { get { return name; } }
        static public string Rsa { get { return rsa; } }
        static public string Sid { get { return sid; } }
        static public string Spn { get { return spn; } }
        static public string System { get { return system; } }
        static public string Thumbprint { get { return thumbprint; } }
        static public string Upn { get { return upn; } }
        static public string Uri { get { return uri; } }
        static public string X500DistinguishedName { get { return x500DistinguishedName; } }
        static public string NameIdentifier { get { return nameidentifier; } }
        static public string Authentication { get { return authentication; } }
        static public string AuthorizationDecision { get { return authorizationdecision; } }

        // used in info card 
        static public string GivenName { get { return givenname; } }
        static public string Surname { get { return surname; } }
        static public string StreetAddress { get { return streetaddress; } }
        static public string Locality { get { return locality; } }
        static public string StateOrProvince { get { return stateorprovince; } }
        static public string PostalCode { get { return postalcode; } }
        static public string Country { get { return country; } }
        static public string HomePhone { get { return homephone; } }
        static public string OtherPhone { get { return otherphone; } }
        static public string MobilePhone { get { return mobilephone; } }
        static public string DateOfBirth { get { return dateofbirth; } }
        static public string Gender { get { return gender; } }
        static public string PPID { get { return ppid; } }
        static public string Webpage { get { return webpage; } }
    }

}