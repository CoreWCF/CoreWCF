// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;

namespace Helpers
{
    public class CommonUtility
    {
        public static string CreateInterestingString(int length)
        {
            char[] chars = new char[length];
            int index = 0;

            // Arrays of odd length will start with a single char.
            // The rest of the entries will be surrogate pairs.
            if (length % 2 == 1)
            {
                chars[index] = 'a';
                index++;
            }

            // Fill remaining entries with surrogate pairs
            int seed = DateTime.Now.Millisecond;
            Random rand = new Random(seed);
            char highSurrogate;
            char lowSurrogate;

            while (index < length)
            {
                highSurrogate = Convert.ToChar(rand.Next(0xD800, 0xDC00));
                lowSurrogate = Convert.ToChar(rand.Next(0xDC00, 0xE000));

                chars[index] = highSurrogate;
                ++index;
                chars[index] = lowSurrogate;
                ++index;
            }

            return new string(chars, 0, chars.Length);
        }
    }

    public static class CommonUtilities
    {
        public static void AddSchema(System.Xml.Schema.XmlSchemaSet schemas, System.Xml.XmlQualifiedName name)
        {
            // nothing to do.
        }

        public enum Test
        {
            Basic,
            NestedBasic
        }

        public static void WriteXml(Test test, XmlWriter writer)
        {
            switch (test)
            {
                case Test.Basic:
                    {
                        // <MyXmlSerialzable><root something="MyValue" /></MyXmlSerialzable>
                        writer.WriteStartElement("root");
                        writer.WriteAttributeString("something", "MyValue");
                        writer.WriteEndElement();
                    }
                    break;
                case Test.NestedBasic:
                    {
                        writer.WriteStartElement(NestedNodeNames[0]);
                        // writer.WriteEndAttribute();
                        writer.WriteStartElement(NestedNodeNames[1], "http://somenamepsace");
                        writer.WriteElementString(NestedNodeNames[2], "can you see me???");
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                    break;
                default:
                    throw new Exception("I don't know what to do with test " + test);
            }
        }

        private static readonly string[] NestedNodeNames = new string[] { "root", "inside", "inner" };

        public static void ReadXml(Test test, XmlReader reader)
        {
            switch (test)
            {
                case Test.Basic:
                    {
                        // <MyXmlSerialzable><root something="MyValue" /></MyXmlSerialzable>
                        if (!reader.Read())
                        {
                            throw new Exception("reader.Read() returned false!");
                        }
                        if (reader.Name == null || reader.Name.Equals("root") == false)
                        {
                            throw new Exception("Bad Name = " + reader.Name);
                        }
                        if (reader.HasAttributes == false)
                        {
                            throw new Exception("I have not attribute, but I expected one");
                        }
                        string attriString = reader.GetAttribute("something");
                        if (attriString == null || attriString.Equals("MyValue") == false)
                        {
                            throw new Exception("Badd atribute Value = " + attriString);
                        }
                        reader.Read(); // end root
                                       //Console.WriteLine("after a final read Name = {0}", reader.Name);
                        reader.ReadEndElement(); // MYContainer
                    }
                    break;
                case Test.NestedBasic:
                    {
                        if (!reader.Read())
                        {
                            throw new Exception("reader.Read() returned false!");
                        }
                        if (reader.Name == null || reader.Name.Equals(NestedNodeNames[0]) == false)
                        {
                            throw new Exception("Bad Name = " + reader.Name);
                        }
                        reader.Read();
                        if (reader.Name == null || reader.Name.Equals(NestedNodeNames[1]) == false)
                        {
                            throw new Exception("Bad Name = " + reader.Name);
                        }
                        reader.Read();
                        if (reader.Name == null || reader.Name.Equals(NestedNodeNames[2]) == false)
                        {
                            throw new Exception("Bad Name = " + reader.Name);
                        }
                        string str = reader.ReadString();
                        if (str == null || str.Equals("can you see me???") == false)
                        {
                            throw new Exception("could not find string <can you see me???>");
                        }
                        int i = 0;
                        while (reader.Read())
                        { // read inside
                          //Console.WriteLine("after a {0} read Name = {1}", i++, reader.Name);
                        }
                        if (i != 3) // run out the clock
                        {
                            throw new Exception("Error i = " + i);
                        }
                    }
                    break;
                default:
                    throw new Exception("I don't know what to do with test " + test);
            }
        }

        static readonly char[] _XmlCharacterArray;

        public static char[] XmlCharacterArray
        {
            get
            {
                return _XmlCharacterArray;
            }
        }

        static CommonUtilities()
        {
            char singleton1 = '\x9', singleton2 = '\xA', singleton3 = '\xD',
               rangleMin1 = '\x20', rangleMax1 = '\xD7FF',
               rangleMin2 = '\xE000', rangleMax2 = '\xFFFD';

            List<char> list = new List<char>(char.MaxValue);
            list.Add(singleton1);
            list.Add(singleton2);
            list.Add(singleton3);
            for (char c = rangleMin1; c <= rangleMax1; c++) list.Add(c);
            for (char c = rangleMin2; c <= rangleMax2; c++) list.Add(c);
            _XmlCharacterArray = list.ToArray();
        }
        public const float FloatValue = 45.3333333f;
        public const decimal DecimalValue = 23.999999m;
        public const double DoubleValue = -88888.98d;
        public const int IntValue = 444444534;
        public const long LongValue = long.MaxValue;
        public const string WhiteSpaceString = "\t\t\t\t\t\r\n   \n\r   ";

        #region XMLSTR
        public const string xmlString =
@"<root>
	<!--Comment-->
	<elem>
		<!-- Comment -->
		<child1 att='1'>
			<child2 xmlns='child2'>
				<child3/>
				blahblahblah<![CDATA[ blah ]]>
				<child4/>
			</child2>
		</child1>
	</elem>
	<elem att='1'>
		<child1 att='1'>
			<child2 xmlns='child2'>
				<child3/>
				blahblahblah
				<child4/>
			</child2>
		</child1>
	</elem>
	<elem xmlns='elem'>
		<child1 att='1'>
			<child2 xmlns='child2'>
				<child3/>
				blahblahblah2
				<child4/>
			</child2>
		</child1>
	</elem>
	<elem xmlns='elem' att='1'>
		<child1 att='1'>
			<child2 xmlns='child2'>
				<child3/>
				blahblahblah2
				<child4/>
			</child2>
		</child1>
	</elem>
	<e:elem xmlns:e='elem2'>
		<e:child1 att='1'>
			<e:child2 xmlns='child2'>
				<e:child3/>
				blahblahblah2
				<e:child4/>
			</e:child2>
		</e:child1>
	</e:elem>
	<e:elem xmlns:e='elem2' att='1'>
		<e:child1 att='1'>
			<e:child2 xmlns='child2'>
				<e:child3/>
				blahblahblah2
				<e:child4/>
			</e:child2>
		</e:child1>
	</e:elem>
</root>";

        #endregion
        public const string XmlStringForAttributes =
            @"<?xml version='1.0'?><attributeHolder a='b' c='d' e='f' />";
        public static readonly TimeSpan TimeSpanValue = new TimeSpan(365, 5, 57, 2);
        public const string SERNS = @"http://schemas.microsoft.com/2003/10/Serialization/";
    }

    public class MyException : Exception
    {
        public MyException(string s) : base(s) { }
        public MyException() : base() { }
    }
}
