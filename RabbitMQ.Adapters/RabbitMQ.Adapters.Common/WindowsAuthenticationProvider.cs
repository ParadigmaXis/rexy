using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Common {
    public class WindowsAuthenticationProvider : WindowsAuthenticationProtocol {

        public WindowsAuthenticationProvider(Action<RabbitMQ.Client.IBasicProperties, byte[]> sendMessage) : base(sendMessage) { }

        private Microsoft.Samples.Security.SSPI.ClientContext clientContext;

        public override void HandleAuthenticationMessage(string queueName, RabbitMQ.Client.Events.BasicDeliverEventArgs e) {
            if (clientContext == null) {
                clientContext = new Microsoft.Samples.Security.SSPI.ClientContext(new Microsoft.Samples.Security.SSPI.ClientCredential(Microsoft.Samples.Security.SSPI.Credential.Package.Negotiate), "", Microsoft.Samples.Security.SSPI.ClientContext.ContextAttributeFlags.Delegate);
            } else {
                if (!clientContext.ContinueProcessing) {
                    throw new Exception();
                }
                clientContext.Initialize(e.Body);
            }
            var basicProperties = new RabbitMQ.Client.Framing.BasicProperties() {
                Headers = new Dictionary<string, object>()
            };
            basicProperties.CorrelationId = e.BasicProperties.CorrelationId;
            basicProperties.ReplyTo = queueName;
            basicProperties.ContentType = Constants.ContentTypeOctetStream;
            basicProperties.Type = Constants.SoapAuthMessagetype;
            basicProperties.Headers.Add("SSPI-ContinueProcessing", clientContext.ContinueProcessing.ToString());
            SendMessage(basicProperties, clientContext.Token);
        }
    }
}
