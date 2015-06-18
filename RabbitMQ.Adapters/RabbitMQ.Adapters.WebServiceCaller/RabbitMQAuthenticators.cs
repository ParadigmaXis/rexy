using System;
using System.Net;
using RabbitMQ.Client;
using RabbitMQ.Adapters.Common;

namespace RabbitMQ.Adapters.WebServiceCaller {
    internal interface IRabbitMQAuthenticator : IDisposable {
        void Authenticate(HttpWebRequest request);
    }

    internal class RabbitMQAnonimousAuthenticator : IRabbitMQAuthenticator {
        public void Authenticate(HttpWebRequest request) { }
        public void Dispose() { }
    }

    internal class RabbitMQWindowsAuthenticator : IRabbitMQAuthenticator {
        IModel channel = null;
        QueueDeclareOk authQueue = null;
        string replyTo = null;
        Microsoft.Samples.Security.SSPI.ServerContext serverContext = null;

        public RabbitMQWindowsAuthenticator(IModel channel, string replyTo) {
            this.channel = channel;
            this.authQueue = this.channel.QueueDeclare();
            this.replyTo = replyTo;
        }

        public void Authenticate(HttpWebRequest request) {
            var consumer = new QueueingBasicConsumer(this.channel);
            this.channel.BasicConsume(this.authQueue.QueueName, true, consumer);
            var receiver = new WindowsAuthenticationReceiver(
                (basicProperties, body) => { this.channel.BasicPublish("", this.replyTo, basicProperties, body); },
                (arg0) => { this.serverContext = arg0; }
                );
            receiver.RequestAuthentication(this.authQueue.QueueName);
            while (this.serverContext == null) {
                Client.Events.BasicDeliverEventArgs authReply;
                if (consumer.Queue.Dequeue(600000, out authReply)) {
                    if (receiver.IsAuthenticationMessage(authReply)) {
                        receiver.HandleAuthenticationMessage(this.authQueue.QueueName, authReply);
                    } else {
                        throw new Exception();
                    }
                } else {
                    throw new Exception();
                }
            }
            this.serverContext.ImpersonateClient();
            request.Credentials = CredentialCache.DefaultNetworkCredentials;
        }

        public void Dispose() {
            if (this.serverContext != null) {
                this.serverContext.RevertImpersonation();
                this.serverContext.Dispose();
            }

            if (this.channel != null && channel.IsOpen && authQueue != null) {
                this.channel.QueueDelete(authQueue.QueueName);
            }
        }
    }
}