using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Common {
    public abstract class WindowsAuthenticationProtocol {
        protected readonly Action<RabbitMQ.Client.IBasicProperties, byte[]> SendMessage;
        public WindowsAuthenticationProtocol(Action<RabbitMQ.Client.IBasicProperties, byte[]> sendMessage) {
            SendMessage = sendMessage;
        }
        public bool IsAuthenticationMessage(RabbitMQ.Client.Events.BasicDeliverEventArgs e) {
            return e.BasicProperties.ContentType == Constants.ContentTypeOctetStream && e.BasicProperties.Type == Constants.SoapAuthMessagetype;
        }
        public abstract void HandleAuthenticationMessage(string queueName, RabbitMQ.Client.Events.BasicDeliverEventArgs e);
    }
}
