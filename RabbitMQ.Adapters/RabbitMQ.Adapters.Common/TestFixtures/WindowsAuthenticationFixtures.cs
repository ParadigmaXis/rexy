using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Common.TestFixtures {
    [TestFixture]
    public class WindowsAuthenticationFixtures: AssertionHelper {
        [Test]
        public void HowToAuthenticate() {
            var clientContext = new Microsoft.Samples.Security.SSPI.ClientContext(new Microsoft.Samples.Security.SSPI.ClientCredential(Microsoft.Samples.Security.SSPI.Credential.Package.Negotiate), "", Microsoft.Samples.Security.SSPI.ClientContext.ContextAttributeFlags.Delegate);
            var clientToken = clientContext.Token;
            // client.Send(clientContext.ContinueProcessing, clientContext.Token)
            var serverContext = new Microsoft.Samples.Security.SSPI.ServerContext(new Microsoft.Samples.Security.SSPI.ServerCredential(Microsoft.Samples.Security.SSPI.Credential.Package.Negotiate), clientToken);
            var serverToken = serverContext.Token;
            // if (msg.ContinueProcessing) server.Send(serverContext.Token)
            while (clientContext.ContinueProcessing) {
                clientContext.Initialize(serverToken);
                if (clientContext.ContinueProcessing) { 
                    clientToken = clientContext.Token;
                    // client.Send(clientContext.ContinueProcessing, clientContext.Token)
                    serverContext.Accept(clientToken);
                    serverToken = serverContext.Token;
                    // if (msg.ContinueProcessing) server.Send(serverContext.Token)
                } else {
                    try {
                        serverContext.ImpersonateClient();
                    } finally {
                        serverContext.RevertImpersonation();
                    }
                }
            }
        }
        // This test may fail because the number of roundtrips is not fixed.
        [Test]
        public void AuthenticateWithHelperClasses() {
            var providerQueue = new List<Client.Events.BasicDeliverEventArgs>();
            var receiverQueue = new List<Client.Events.BasicDeliverEventArgs>();
            var authenticated = false;
            var provider = new WindowsAuthenticationProvider((basicProperties, body) => { receiverQueue.Add(new Client.Events.BasicDeliverEventArgs("", 0, false, "", "", basicProperties, body)); });
            var receiver = new WindowsAuthenticationReceiver((basicProperties, body) => { providerQueue.Add(new Client.Events.BasicDeliverEventArgs("", 0, false, "", "", basicProperties, body)); }, serverContext => { authenticated = true; });

            receiver.RequestAuthentication("amq.gen-receiver");
            Expect(authenticated, Is.False);

            int roundtrips = 0;
            while (roundtrips < 100 && !authenticated) {
                Expect(authenticated, Is.False);

                Expect(provider.IsAuthenticationMessage(providerQueue.Last()), Is.True);
                provider.HandleAuthenticationMessage("amq.gen-provider", providerQueue.Last());

                Expect(receiverQueue.Last().BasicProperties.CorrelationId, Is.EqualTo(providerQueue.Last().BasicProperties.CorrelationId));

                Expect(receiverQueue.Last().BasicProperties.ContentType, Is.EqualTo(Constants.ContentTypeOctetStream));
                Expect(providerQueue.Last().BasicProperties.ContentType, Is.EqualTo(Constants.ContentTypeOctetStream));
                Expect(receiverQueue.Last().BasicProperties.Type, Is.EqualTo(Constants.SoapAuthMessagetype));
                Expect(providerQueue.Last().BasicProperties.Type, Is.EqualTo(Constants.SoapAuthMessagetype));
                Expect(receiverQueue.Last().BasicProperties.ReplyTo, Is.EqualTo("amq.gen-provider"));
                Expect(providerQueue.Last().BasicProperties.ReplyTo, Is.EqualTo("amq.gen-receiver"));

                Expect(receiver.IsAuthenticationMessage(receiverQueue.Last()), Is.True);
                receiver.HandleAuthenticationMessage("amq.gen-receiver", receiverQueue.Last());
            }
            Expect(authenticated, Is.True);
        }
    }
}
