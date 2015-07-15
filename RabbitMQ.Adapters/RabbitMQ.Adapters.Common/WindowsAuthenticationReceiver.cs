using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NSspi;
using NSspi.Contexts;
using NSspi.Credentials;

namespace RabbitMQ.Adapters.Common {
    public class WindowsAuthenticationReceiver : WindowsAuthenticationProtocol {

        private readonly Action<ServerContext> Authenticated;
        private readonly ServerCredential serverCredential;
        private readonly ServerContext serverContext;
        public SecurityStatus Status { get; private set; }

        public WindowsAuthenticationReceiver(Action<IBasicProperties, byte[]> sendMessage, Action<ServerContext> authenticated) : base(sendMessage) {
            Authenticated = authenticated;
            serverCredential = new ServerCredential(PackageNames.Kerberos);
            serverContext = new ServerContext(serverCredential,
                ContextAttrib.MutualAuth |
                ContextAttrib.UseSessionKey |
                ContextAttrib.Confidentiality |
                ContextAttrib.ReplayDetect |
                ContextAttrib.SequenceDetect |
                ContextAttrib.Connection |
                ContextAttrib.Delegate);
            Status = SecurityStatus.ContinueNeeded;
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
            if (Status == SecurityStatus.ContinueNeeded) {
                byte[] nextToken;
                Status = serverContext.AcceptToken(e.Body, out nextToken);
                if (Status == SecurityStatus.ContinueNeeded || Status == SecurityStatus.OK) {
                    if (nextToken != null) {
                        var basicProperties = new RabbitMQ.Client.Framing.BasicProperties() {
                            Headers = new Dictionary<string, object>()
                        };
                        basicProperties.CorrelationId = Guid.NewGuid().ToString();
                        basicProperties.ReplyTo = queueName;
                        basicProperties.ContentType = Constants.ContentTypeOctetStream;
                        basicProperties.Type = Constants.SoapAuthMessagetype;
                        SendMessage(basicProperties, nextToken);
                    }
                }
                if (Status == SecurityStatus.OK) {
                    Authenticated(serverContext);
                }
            }
        }
    }
}
