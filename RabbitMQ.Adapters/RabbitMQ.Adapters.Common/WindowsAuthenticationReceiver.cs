using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Adapters.Common {
    public class WindowsAuthenticationReceiver: WindowsAuthenticationProtocol {
        private readonly Action<Microsoft.Samples.Security.SSPI.ServerContext> Authenticated;
        private Microsoft.Samples.Security.SSPI.ServerContext serverContext = null;
        public WindowsAuthenticationReceiver(Action<RabbitMQ.Client.IBasicProperties, byte[]> sendMessage, Action<Microsoft.Samples.Security.SSPI.ServerContext> authenticated): base(sendMessage) {
            Authenticated = authenticated;
        }
        public void RequestAuthentication(string queueName) {
            var basicProperties = new RabbitMQ.Client.Framing.BasicProperties() {
                Headers = new Dictionary<string, object>()
            };
            basicProperties.CorrelationId = Guid.NewGuid().ToString();
            basicProperties.ReplyTo = queueName;
            basicProperties.ContentType = Constants.ContentTypeOctetStream;
            basicProperties.Type = Constants.SoapAuthMessagetype;
            SendMessage(basicProperties, new byte[0]);
        }
        public override void HandleAuthenticationMessage(string queueName, BasicDeliverEventArgs e) {
            if (!bool.Parse(Constants.GetUTF8String(e.BasicProperties.Headers["SSPI-ContinueProcessing"]))) {
                Authenticated(serverContext);
            } else {
                if (serverContext == null) {
                    serverContext = new Microsoft.Samples.Security.SSPI.ServerContext(new Microsoft.Samples.Security.SSPI.ServerCredential(Microsoft.Samples.Security.SSPI.Credential.Package.Negotiate), e.Body);
                } else {
                    serverContext.Accept(e.Body);
                }
                var basicProperties = new RabbitMQ.Client.Framing.BasicProperties() {
                    Headers = new Dictionary<string, object>()
                };
                basicProperties.CorrelationId = Guid.NewGuid().ToString();
                basicProperties.ReplyTo = queueName;
                basicProperties.ContentType = Constants.ContentTypeOctetStream;
                basicProperties.Type = Constants.SoapAuthMessagetype;
                var token = serverContext.Token;
                SendMessage(basicProperties, serverContext.Token);
            }
        }
    }
}
