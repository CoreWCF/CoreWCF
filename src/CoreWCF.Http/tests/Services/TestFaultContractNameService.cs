using CoreWCF;

namespace Services
{
    public class TestFaultContractNameService : ServiceContract.ITestFaultContractName
    {
     
        #region TwoWay_Methods
        public string Method1(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method2(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method3(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method4(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method5(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method6(string s)
        {
                if (s.Length == 0)
                {
                    string faultToThrow = "Test fault thrown from a service";
                    throw new FaultException<string>(faultToThrow);
                }

                return s;
            }

        public string Method7(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method8(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method9(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method10(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method11(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method12(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method13(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method14(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method15(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method16(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method17(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method18(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method19(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method20(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }

        public string Method21(string s)
        {
            if (s.Length == 0)
            {
                string faultToThrow = "Test fault thrown from a service";
                throw new FaultException<string>(faultToThrow);
            }

            return s;
        }
        #endregion
    }
}

