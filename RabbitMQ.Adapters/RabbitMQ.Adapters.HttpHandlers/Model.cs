using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RabbitMQ.Adapters.HttpHandlers
{
    public class RabbitMQMessage
    {
        public RabbitMQMessage() { }
        public RabbitMQMessage(IBasicProperties basicProperties, byte[] body)
        {
            this.BasicProperties = basicProperties;
            this.Body = body;
        }
        public IBasicProperties BasicProperties { get; set; }
        public byte[] Body { get; set; }
    }
}