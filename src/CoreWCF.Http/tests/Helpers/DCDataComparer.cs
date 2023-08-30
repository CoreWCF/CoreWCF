// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;

namespace Helpers
{
    public class DCDataComparer
    {
        //using hashtable to keep track of circular refence 
        private Hashtable lhsObjectHashTable = new Hashtable();

        private Hashtable rhsObjectHashTable = new Hashtable();

        public static bool Compare(object lhObj, object rhObj)
        {
            return new DCDataComparer().CompareObjects(lhObj, rhObj);
        }

        private bool CompareObjects(object lhObj, object rhObj)
        {
            if (lhObj == null && rhObj == null) return true;

            if (lhObj == null || rhObj == null) return false;

            Type lhObjType = lhObj.GetType();
            Type rhObjType = rhObj.GetType();

            //both objects have same circular reference 
            if (lhsObjectHashTable[lhObj] != null && rhsObjectHashTable[rhObj] != null)
            {
                return true;
            }
            // one object has circular ref, so not equal
            else if (lhsObjectHashTable[lhObj] != null || rhsObjectHashTable[rhObj] != null)
            {
                return false;
            }

            DataContract lhDataContract = DataContract.GetDataContract(lhObjType);
            DataContract rhDataContract = DataContract.GetDataContract(rhObjType);

            //if either type is not serializable or DC, then above GetDataContract would throw
            //so both DataContracts are not null now
            if (!lhDataContract.Equals(rhDataContract)) return false;

            //lhDataContract and rhDataContract have same qname now (that's the Equals compare
            //enum, primitive, string and decimal can be compared by .Equals, note decimal is not a primitive 
            //in this case, lhObjType should be equal to rhObjType
            if (lhObjType.IsEnum || lhObjType == typeof(Decimal) || lhObjType.IsPrimitive || lhObj is string)
            {
                System.Diagnostics.Debug.Assert(lhObjType == rhObjType, "types must be same for the compared objects when they are enum/primitive/string/decimal", string.Format("left type is {0}, right type is {1}", lhObjType, rhObjType));
                return lhObj.Equals(rhObj);
            }
            else if (lhObjType.IsArray && rhObjType.IsArray)
            {
                Array lhArray = (Array)lhObj;
                Array rhArray = (Array)rhObj;

                if (lhArray.Length != rhArray.Length)
                {
                    return false;
                }

                //indeces and bounds of the array to be compared
                //indeces is the current position
                Array indeces = Array.CreateInstance(typeof(int), lhArray.Rank);

                //bounds is the upper bounds of the array
                Array bounds = Array.CreateInstance(typeof(int), lhArray.Rank);
                int arrayTotalLength = 0;

                for (int i = 0; i < indeces.Length; i++)
                {
                    indeces.SetValue(0, i);
                    bounds.SetValue(lhArray.GetLength(i), i);
                    if (i == 0)
                        arrayTotalLength = lhArray.GetLength(0);
                    else
                        arrayTotalLength *= (int)lhArray.GetLength(i);
                }

                int index = 0;
                bool notEnd = true;

                //UNDONE: only need one condition, use both to double check here
                while (index < arrayTotalLength && notEnd)
                {
                    if (!this.CompareObjects(lhArray.GetValue((int[])indeces), rhArray.GetValue((int[])indeces))) return false;

                    //increase array indeces to the next element in the array
                    //always increment from the highest dimension
                    notEnd = this.IncIndex((int[])indeces, (int[])bounds, indeces.Length - 1);
                    index++;
                }

                return true;
            }
            else
            {
                lhsObjectHashTable.Add(lhObj, lhObj);
                rhsObjectHashTable.Add(rhObj, rhObj);

                ClassDataContract lhClassDataContract = lhDataContract as ClassDataContract;
                ClassDataContract rhClassDataContract = rhDataContract as ClassDataContract;

                if (rhClassDataContract == null || lhClassDataContract == null)
                    throw new Exception("type mismatch! not both are class/struct");

                return CompareDM(lhObj, lhClassDataContract, rhObj, rhClassDataContract);
            }
        }

        private bool IncIndex(int[] indeces, int[] bounds, int dimension)
        {
            indeces[dimension]++;
            if (indeces[dimension] >= bounds[dimension])
            {
                //have exhausted the array
                if (dimension == 0)
                {
                    System.Diagnostics.Trace.WriteLine("dimesion is " + dimension);
                    return false;
                }

                indeces[dimension] = 0;
                return IncIndex(indeces, bounds, dimension - 1);
            }

            return true;
        }

        private bool CompareDM(object lhObj, ClassDataContract lhDataContract, object rhObj, ClassDataContract rhDataContract)
        {
            bool result = true;

            //DM differences must be whole version, no partial version data
            int lhVEnd = 0;
            int rhVEnd = 0;
            DataMember lhDataMember;
            object lhMemberValue;
            DataMember rhDataMember;
            object rhMemberValue;

            while (lhVEnd < lhDataContract.Members.Count && rhVEnd < rhDataContract.Members.Count)
            {
                //Members of the data contract has been sorted alphabetic then VersionAdded
                lhDataMember = lhDataContract.Members[lhVEnd++];
                rhDataMember = rhDataContract.Members[rhVEnd++];
                if (!lhDataMember.Equals(rhDataMember)) return false;

                lhMemberValue = lhDataMember.GetMemberValue(lhObj);
                rhMemberValue = rhDataMember.GetMemberValue(rhObj);
                if (lhMemberValue != null && rhMemberValue != null)
                {
                    result = this.CompareObjects(lhMemberValue, rhMemberValue);
                    if (!result) return false;
                }
                else if (lhMemberValue == null && rhMemberValue == null) continue;
                else
                    return false;
            }

            //For different version, remaining DM's versionadded need to be greater then tested
            if (lhVEnd < lhDataContract.Members.Count && rhVEnd == rhDataContract.Members.Count && rhDataContract.Members.Count > 0)
            {
                result &= lhDataContract.Members[lhVEnd].VersionAdded > rhDataContract.Members[rhVEnd - 1].VersionAdded;
            }
            else if (rhVEnd < rhDataContract.Members.Count && lhVEnd == lhDataContract.Members.Count && lhDataContract.Members.Count > 0)
            {
                result &= rhDataContract.Members[rhVEnd].VersionAdded > lhDataContract.Members[lhVEnd - 1].VersionAdded;
            }

            if (!result) return false;

            if (lhDataContract.BaseContract != null && rhDataContract.BaseContract != null)
                return CompareDM(lhObj, lhDataContract.BaseContract, rhObj, rhDataContract.BaseContract);
            else if (lhDataContract.BaseContract == null && rhDataContract.BaseContract == null)
                return true;
            else
                return false;
        }
    }
}