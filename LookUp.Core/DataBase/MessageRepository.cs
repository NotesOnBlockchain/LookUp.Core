using LookUp.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LookUp.Core.DataBase
{
    public class MessageRepository
    {
        private readonly MessageDatabaseContext _dBContext;

        private object _dBContextLock = new();

        public MessageRepository(MessageDatabaseContext databaseContext)
        {
            _dBContext = databaseContext;
        }

        public List<MessageModel> GetMessages()
        {
            lock (_dBContextLock)
            {
                return _dBContext.Messages.ToList();
            }
        }

        public void AddMessage(MessageModel message)
        {
            lock(_dBContextLock)
            {
                _dBContext.Messages.Add(message);
                _dBContext.SaveChanges();
            }
        }

        public void RemoveMessage(MessageModel message) 
        {
            lock (_dBContextLock) 
            {
                _dBContext.Messages.Remove(message);
                _dBContext.SaveChanges();
            }
        }
        public void Clear() 
        {
            lock (_dBContextLock) 
            {
                var messages = GetMessages();
                foreach (var message in messages) 
                {
                    _dBContext.Messages.Remove(message);
                }

                _dBContext.SaveChanges();
            }
        }
    }
}
