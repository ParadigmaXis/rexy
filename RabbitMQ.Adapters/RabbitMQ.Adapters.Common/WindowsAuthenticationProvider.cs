using System;
using System.Collections.Generic;
using NSspi;
using NSspi.Contexts;
using NSspi.Credentials;

namespace RabbitMQ.Adapters.Common {
    public class WindowsAuthenticationProvider : WindowsAuthenticationProtocol {
        private readonly ClientCredential clientCredential;
        private readonly ClientContext clientContext;
        public SecurityStatus Status { get; private set; }

        public WindowsAuthenticationProvider(Action<RabbitMQ.Client.IBasicProperties, byte[]> sendMessage) : base(sendMessage) {
            clientCredential = new ClientCredential(PackageNames.Kerberos);
            clientContext = new ClientContext(clientCredential, "amqp/dc.px.local",
                ContextAttrib.MutualAuth |
                ContextAttrib.UseSessionKey |
                ContextAttrib.Confidentiality |
                ContextAttrib.ReplayDetect |
                ContextAttrib.SequenceDetect |
                ContextAttrib.Connection |
                ContextAttrib.Delegate);
            Status = SecurityStatus.ContinueNeeded;
        }


        public override void HandleAuthenticationMessage(string queueName, RabbitMQ.Client.Events.BasicDeliverEventArgs e) {
            if (Status == SecurityStatus.ContinueNeeded) {
                byte[] outBytes;
                if (e.Body.Length == 0) {
                    Status = clientContext.Init(null, out outBytes);
                } else {
                    Status = clientContext.Init(e.Body, out outBytes);
                }

                if (Status == SecurityStatus.ContinueNeeded || Status == SecurityStatus.OK) {
                    if (outBytes != null) {
                        var basicProperties = new RabbitMQ.Client.Framing.BasicProperties() {
                            Headers = new Dictionary<string, object>()
                        };
                        basicProperties.CorrelationId = e.BasicProperties.CorrelationId;
                        basicProperties.ReplyTo = queueName;
                        basicProperties.ContentType = Constants.ContentTypeOctetStream;
                        basicProperties.Type = Constants.SoapAuthMessagetype;
                        SendMessage(basicProperties, outBytes);
                    }
                }
            }
            //if (!clientContext.ContinueProcessing) {
            //    Authenticated(clientContext)
            //}
        }
    }
}
