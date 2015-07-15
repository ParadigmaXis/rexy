using System;
using System.Net;
using RabbitMQ.Client;
using RabbitMQ.Adapters.Common;
using NSspi.Contexts;

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
        ServerContext serverContext = null;
        ImpersonationHandle impersonation = null;
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
            Console.WriteLine("+AUTH {0} {1} {2}", serverContext.AuthorityName, serverContext.ContextUserName, serverContext.SupportsImpersonate);
            impersonation = this.serverContext.ImpersonateClient();
            try {
                var ident = System.Threading.Thread.CurrentPrincipal.Identity;
                Console.WriteLine("++AUTH {0}", ident.GetType().FullName);
                System.IO.File.WriteAllLines("c:\\FusionLog\\" + Guid.NewGuid().ToString(), new string[0]);
            } catch (Exception ex) {
                Console.WriteLine("\t!\t{0}\n\t\t{1}", ex.GetType(), ex.Message);
            }
            //request.Credentials = CredentialCache.DefaultNetworkCredentials;
            request.ImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
            request.UseDefaultCredentials = true;
                
        }

        public void Dispose() {
            if (impersonation != null) {
                impersonation.Dispose();
            }
            if (serverContext != null) {
                serverContext.Dispose();
            }

            if (this.channel != null && channel.IsOpen && authQueue != null) {
                this.channel.QueueDelete(authQueue.QueueName);
            }
        }
    }
}