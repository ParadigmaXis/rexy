using RabbitMQ.Adapters.Common;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.WebServiceCaller {
    public partial class WebServiceCallerService : ServiceBase {
        public WebServiceCallerService() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {
            //new System.Threading.Thread(Main);
        }

        protected override void OnStop() {
            // AskToStop(thread);
            //thread.Join();
        }

        public void Main() {
            var factory = new ConnectionFactory { HostName = "AURA", VirtualHost = "/", UserName = "isa-web-service-caller", Password = "isa-web-service-caller" };
            using (var connection = factory.CreateConnection()) {
                using (var channel = connection.CreateModel()) {
                    channel.BasicAcks += (sender, e) => Debug.WriteLine(string.Format("WSCS::ACK {0} {1}", e.DeliveryTag, e.Multiple));
                    channel.BasicNacks += (sender, e) => Debug.WriteLine(string.Format("WSCS::NACK {0} {1} {2}", e.DeliveryTag, e.Multiple, e.Requeue));
                    channel.BasicRecoverOk += (sender, e) => Debug.WriteLine(string.Format("WSCS::RECOVER_OK"));
                    channel.BasicReturn += (sender, e) => Debug.WriteLine(string.Format("WSCS::RETURN ..."));
                    channel.CallbackException += (sender, e) => Debug.WriteLine(string.Format("WSCS::CALLBACK_EXCEPTION {0}", e.Exception.Message));
                    channel.ModelShutdown += (sender, e) => Debug.WriteLine(string.Format("WSCS::MODEL_SHUTDOWN ..."));

                    var queue = channel.QueueDeclare();
                    channel.QueueBind(queue.QueueName, Constants.WebServiceAdapterExchange, "", new Dictionary<String, Object>());
                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(queue.QueueName, false, consumer);
                    while (true) {
                        var msg = consumer.Queue.Dequeue();
                        var requestMsg = new RabbitMQMessage(msg.BasicProperties, msg.Body);
                        var request = RabbitMQMessageToHttpWebRequest(requestMsg);

                        // forward
                        try {
                            var response = CallWebService(request, channel, msg);
                            var basicproperties = CreateResponseBasicProperties(200, "OK", response.Headers.AllKeys.ToDictionary(k => k, k => response.Headers[k]));
                            basicproperties.CorrelationId = msg.BasicProperties.CorrelationId;
                            var buffer = new byte[response.ContentLength];
                            if (response.ContentLength > 0) {
                                var responseStream = response.GetResponseStream();
                                responseStream.Read(buffer, 0, buffer.Length);
                                responseStream.Close();
                            }
                            channel.BasicPublish("", msg.BasicProperties.ReplyTo, basicproperties, buffer);
                        } catch (WebException ex) {
                            IBasicProperties basicProperties;
                            byte[] body = null;
                            if (ex.Response != null) {
                                basicProperties = CreateResponseBasicProperties((int)(ex.Response as HttpWebResponse).StatusCode, (ex.Response as HttpWebResponse).StatusDescription, ex.Response.Headers.AllKeys.ToDictionary(k => k, k => ex.Response.Headers[k]));
                                body = new byte[ex.Response.ContentLength];
                                ex.Response.GetResponseStream().Read(body, 0, (int)ex.Response.ContentLength);
                            } else {
                                basicProperties = CreateResponseBasicProperties((int)HttpStatusCode.ServiceUnavailable, "Service Unavailable", new Dictionary<string, string>());
                            }
                            basicProperties.CorrelationId = msg.BasicProperties.CorrelationId;
                            channel.BasicPublish("", msg.BasicProperties.ReplyTo, basicProperties, body ?? new byte[0]);
                        }
                    }
                }
            }
        }
        private static WebResponse CallWebService(HttpWebRequest request, IModel channel, Client.Events.BasicDeliverEventArgs msg) {
            if ((bool)msg.BasicProperties.Headers[Constants.RequestIsAuthenticated]) {
                var authQueue = channel.QueueDeclare();
                try {
                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(authQueue.QueueName, true, consumer);
                    Microsoft.Samples.Security.SSPI.ServerContext serverContext = null;
                    var receiver = new WindowsAuthenticationReceiver(
                        (basicProperties, body) => { channel.BasicPublish("", msg.BasicProperties.ReplyTo, basicProperties, body); },
                        (arg0) => { serverContext = arg0; }
                        );
                    receiver.RequestAuthentication(authQueue.QueueName);
                    while (serverContext == null) {
                        Client.Events.BasicDeliverEventArgs authReply;
                        if (consumer.Queue.Dequeue(600000, out authReply)) {
                            if (receiver.IsAuthenticationMessage(authReply)) {
                                receiver.HandleAuthenticationMessage(authQueue.QueueName, authReply);
                            } else {
                                throw new Exception();
                            }
                        } else {
                            throw new Exception();
                        }
                    }
                    try {
                        serverContext.ImpersonateClient();
                        request.Credentials = CredentialCache.DefaultNetworkCredentials;
                        return request.GetResponse();
                    } finally {
                        serverContext.RevertImpersonation();
                        serverContext.Dispose();
                    }
                } finally {
                    channel.QueueDelete(authQueue.QueueName);
                }
            }
            return request.GetResponse();
        }
        private static HttpWebRequest RabbitMQMessageToHttpWebRequest(RabbitMQMessage msg)
        {
            //var gatewayUrl = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestGatewayUrl]);
            var destinationUrl = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestDestinationUrl]);
            var request = (HttpWebRequest)WebRequest.Create(destinationUrl);

            request.Method = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestMethod]);
            RecoverHttpHeadersToRequest(msg.BasicProperties.GetHttpHeaders(), request);
            request.ContentLength = msg.Body.Length;
            if (msg.Body.Length > 0)
            {
                var requestStream = request.GetRequestStream();
                requestStream.Write(msg.Body, 0, msg.Body.Length);
                requestStream.Close();
            }
            return request;
        }

        private static void RecoverHttpHeadersToRequest(IDictionary<string, string> httpHeaders, HttpWebRequest request)
        {
            foreach (var kvp in httpHeaders)
            {
                if (Constants.HttpRestrictedHeaders.Contains(kvp.Key))
                {
                    continue;
                }
                else if (Constants.HttpRestrictedHeadersViaProperty.Contains(kvp.Key))
                {
                    if ("Accept".Equals(kvp.Key))
                    {
                        request.Accept = kvp.Value;
                    }
                    else if ("Content-Type".Equals(kvp.Key))
                    {
                        request.ContentType = kvp.Value;
                    }
                }
                else
                {
                    request.Headers.Add(kvp.Key, kvp.Value);
                }
            }
        }

        internal IBasicProperties CreateResponseBasicProperties(int responseStatusCode, string responseStatusDescription, Dictionary<string, string> responseHeaders) {
            var result = new RabbitMQ.Client.Framing.BasicProperties() {
                Headers = new Dictionary<String, object>()
            };
            result.Headers.Add(Constants.ResponseStatusCode, responseStatusCode);
            result.Headers.Add(Constants.ResponseStatusDescription, responseStatusDescription);
            responseHeaders.ToList().ForEach(kvp => result.Headers.Add(Constants.HttpHeaderPrefix + kvp.Key, kvp.Value));
            return result;
        }
    }
}
