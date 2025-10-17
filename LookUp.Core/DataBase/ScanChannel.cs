using LookUp.Core.Models;
using System.Threading.Channels;

namespace LookUp.Core.DataBase
{
    public class ScanChannel
    {
        public Channel<MessageModel> ChannelToDB { get; }

        public ScanChannel()
        {
            ChannelToDB = Channel.CreateUnbounded<MessageModel>(
                new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
        }
    }
}
