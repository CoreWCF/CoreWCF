// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Description
{
    //internal static class NamingHelper
    //{
    //    internal static string TypeName(Type t)
    //    {
    //        if (t.IsGenericType || t.ContainsGenericParameters)
    //        {
    //            Type[] args = t.GetGenericArguments();
    //            int nameEnd = t.Name.IndexOf('`');
    //            string result = nameEnd > 0 ? t.Name.Substring(0, nameEnd) : t.Name;
    //            result += "Of";
    //            for (int i = 0; i < args.Length; ++i)
    //            {
    //                result = result + "_" + TypeName(args[i]);
    //            }
    //            return result;
    //        }
    //        else if (t.IsArray)
    //        {
    //            return "ArrayOf" + TypeName(t.GetElementType());
    //        }
    //        else
    //        {
    //            return t.Name;
    //        }
    //    }

    //    // Converts names that contain characters that are not permitted in XML names to valid names.
    //    internal static string XmlName(string name)
    //    {
    //        if (string.IsNullOrEmpty(name))
    //        {
    //            return name;
    //        }

    //        if (IsAsciiLocalName(name))
    //        {
    //            return name;
    //        }

    //        if (IsValidNCName(name))
    //        {
    //            return name;
    //        }

    //        return XmlConvert.EncodeLocalName(name);
    //    }

    //    private static bool IsAlpha(char ch)
    //    {
    //        return (ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z');
    //    }

    //    private static bool IsDigit(char ch)
    //    {
    //        return (ch >= '0' && ch <= '9');
    //    }

    //    private static bool IsAsciiLocalName(string localName)
    //    {
    //        Fx.Assert(null != localName, "");
    //        if (!IsAlpha(localName[0]))
    //        {
    //            return false;
    //        }

    //        for (int i = 1; i < localName.Length; i++)
    //        {
    //            char ch = localName[i];
    //            if (!IsAlpha(ch) && !IsDigit(ch))
    //            {
    //                return false;
    //            }
    //        }
    //        return true;
    //    }

    //    internal static bool IsValidNCName(string name)
    //    {
    //        try
    //        {
    //            XmlConvert.VerifyNCName(name);
    //            return true;
    //        }
    //        catch (XmlException)
    //        {
    //            return false;
    //        }
    //    }
    //}
}
