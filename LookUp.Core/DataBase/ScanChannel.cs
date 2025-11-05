using LookUp.Models;
using System.Threading.Channels;

namespace LookUp.Scanner.DataBase
{
    public class ScanChannel
    {
        public Channel<MessageModel> MessageChannel { get; }

        public ScanChannel()
        {
            MessageChannel = Channel.CreateUnbounded<MessageModel>(
                new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
        }
    }
}
