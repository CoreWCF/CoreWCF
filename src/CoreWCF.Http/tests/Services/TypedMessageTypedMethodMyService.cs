using Helpers;
using ServiceContract;
using System;
using System.Collections;
using System.Globalization;

namespace Services
{
    public class TypedMessageTypedMethodMyService : ITypedMessageTypedMethodMyService
    {
        public void MyOperation(FooMessage1 request)
        {
            ResultHelper.fromMessage = request.foo.FooName;
        }

        public void Method1(byte b)
        {
            ResultHelper.fromMethod = b.ToString();
        }
    }

    public class TypedMessageTypedMethodMyService2 : ITypedMessageTypedMethodMyService2
    {
        public void MyOperation(FooMessage2 request)
        {
            ResultHelper.fromMessage = request.foos[0].FooName;
        }

        public void Method2()
        {
            ResultHelper.fromMethod = TypedMessageTypedMethodConstants.voidvoid;
        }
    }

    public class TypedMessageTypedMethodMyService3 : ITypedMessageTypedMethodMyService3
    {
        public void MyOperation(FooMessage3 request)
        {
            ResultHelper.fromMessage = request.foos[0].FooName;
        }

        public int Method3(float num)
        {

            int result = (int)num;
            ResultHelper.fromMethod = num.ToString(NumberFormatInfo.InvariantInfo) + " " + result.ToString();
            return result;
        }
    }

    public class TypedMessageTypedMethodMyService4 : ITypedMessageTypedMethodMyService4
    {
        public void MyOperation(FooMessage4 request)
        {
            ResultHelper.fromMessage = request.newID.ToString();
        }

        public bool Method4(double dblnum, decimal decnum)
        {
            bool result = dblnum.ToString().Equals(decnum.ToString());
            ResultHelper.fromMethod = dblnum.ToString(NumberFormatInfo.InvariantInfo) + decnum.ToString(NumberFormatInfo.InvariantInfo) + result.ToString();
            return result;
        }
    }

    public class TypedMessageTypedMethodMyService5 : ITypedMessageTypedMethodMyService5
    {
        public void MyOperation(FooMessage5 request)
        {
            ResultHelper.fromMessage = request.foos[0].FooName;
        }

        public char Method5(string str, byte[] arrbyte)
        {
            char ch = str[0];
            ResultHelper.fromMethod = str + arrbyte[4].ToString() + ch.ToString();
            return ch;
        }
    }

    public class TypedMessageTypedMethodMyService6 : ITypedMessageTypedMethodMyService6
    {
        public void MyOperation(FooMessage6 request)
        {
            ResultHelper.fromMessage = request.ID.ToString();
        }

        public DateTime Method6(int num, Foo foo)
        {
            DateTime now = DateTime.Now;
            ResultHelper.fromMethod = now.DayOfWeek.ToString() + num.ToString();
            return now;
        }
    }

    public class TypedMessageTypedMethodMyService7 : ITypedMessageTypedMethodMyService7
    {
        public void MyOperation(Person request)
        {
            ResultHelper.fromMessage = request.address;
        }

        public Foo[] Method7(string str, ArrayList list)
        {
            Foo[] foos = new Foo[1];
            foos[0] = new Foo();
            foos[0].FooName = TypedMessageTypedMethodConstants.foo;
            ResultHelper.fromMethod = foos[0].FooName + str;
            return foos;
        }
    }

    public class TypedMessageTypedMethodMyService8 : ITypedMessageTypedMethodMyService8
    {
        public void MyOperation(Manager request)
        {
            ResultHelper.fromMessage = request.Address.address;
        }

        public decimal Method8(string str, bool b)
        {
            decimal dec = 0;
            ResultHelper.fromMethod = str + b.ToString() + dec.ToString();
            return dec;
        }
    }
}
