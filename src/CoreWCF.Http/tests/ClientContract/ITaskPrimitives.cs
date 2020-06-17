using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace ClientContract
{
    public static class AsyncNetAdoptionConstants
    {
        public static DateTime TestDateTime = new DateTime(2010, 09, 04, new GregorianCalendar(GregorianCalendarTypes.USEnglish));
    }
   
    [ServiceContract(Namespace = "http://microsoft.samples", Name = "ITestPrimitives")]
    public interface ITestPrimitives
    {
        [OperationContract]
        Task<Int32> GetInt();

        [OperationContract]
        Task<Byte> GetByte();

        [OperationContract]
        Task<SByte> GetSByte();

        [OperationContract]
        Task<short> GetShort();

        [OperationContract]
        Task<ushort> GetUShort();

        [OperationContract]
        Task<Double> GetDouble();

        [OperationContract]
        Task<UInt32> GetUInt();

        [OperationContract]
        Task<long> GetLong();

        [OperationContract]
        Task<ulong> GetULong();

        [OperationContract]
        Task<char> GetChar();

        [OperationContract]
        Task<bool> GetBool();

        [OperationContract]
        Task<float> GetFloat();

        [OperationContract]
        Task<decimal> GetDecimal();

        [OperationContract]
        Task<String> GetString();

        [OperationContract]
        Task<DateTime> GetDateTime();

        [OperationContract]
        Task<int[][]> GetintArr2D();

        [OperationContract]
        Task<float[]> GetfloatArr();

        [OperationContract]
        Task<byte[]> GetbyteArr();

        [OperationContract]
        Task<int?> GetnullableInt();

        [OperationContract]
        Task<TimeSpan> GetTimeSpan();

        [OperationContract]
        Task<Guid> GetGuid();

        [OperationContract]
        Task<Color> GetEnum();
    }
    public enum Color
    {
        Red,
        Green,
        Blue
    }
}
