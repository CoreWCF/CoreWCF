using CoreWCF.Channels;
using Xunit;

namespace Services
{
    public class OpActionReplyActionBehaviorService : ServiceContract.IOpActionReplyActionBehavior
    {
        public int TestMethodCheckCustomReplyAction(int id, string name)
        {
            Assert.Equal(1, id);
            Assert.Equal("Custom ReplyAction", name);
            return id + 1;
        }

        public int TestMethodCheckDefaultReplyAction(int id, string name)
        {
            Assert.Equal("Default ReplyAction", name);
            return id + 1;
        }

        public int TestMethodCheckEmptyReplyAction(int id, string name)
        {
            Assert.Equal("Empty ReplyAction", name);
            return id + 1;
        }

        public Message TestMethodCheckUntypedReplyAction()
        {            
            Message serviceMessage = Message.CreateMessage(MessageVersion.Soap11, "myAction");
            return serviceMessage;
        }

        public int TestMethodCheckUriReplyAction(int id, string name)
        {
            Assert.Equal("Uri ReplyAction", name);
            return id + 1;
        }

        public void UnMatchedMessageHandler(Message m)
        {
            bool flag = false;
            // Writing only the action of the message to the output
            for (int i = 0; i < m.Headers.Count && flag == false; i++)
            {
                string name = m.Headers.GetReaderAtHeader(i).Name;
                string[] fields = name.Split(':');

                foreach (string s in fields)
                {
                    if (s.ToLower() == "action")
                    {
                        string action = m.Headers.GetReaderAtHeader(i).ReadInnerXml();
                        if ("" == action)
                        {
                            action = "empty action";
                        }

                        System.IO.File.WriteAllText("resultAction.txt", action);
                        flag = true;
                        break;
                    }
                }
            }
        }
    }
}
