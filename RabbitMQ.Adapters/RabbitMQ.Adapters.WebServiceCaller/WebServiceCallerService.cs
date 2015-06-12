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
            //var factory = new ConnectionFactory { HostName = "AURA", VirtualHost = "/", UserName = "isa-http-handler", Password = "isa-http-handler" };
            using (var connection = factory.CreateConnection()) {
                using (var channel = connection.CreateModel()) {
                    var queue = channel.QueueDeclare();
                    channel.QueueBind(queue.QueueName, Constants.WebServiceAdapterExchange, "", new Dictionary<String, Object>());
                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(queue.QueueName, false, consumer);

                    var msg = consumer.Queue.Dequeue();

                    var gatewayUrl = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestGatewayUrl]);
                    var destinationUrl = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestDestinationUrl]);

                    var request = (HttpWebRequest)WebRequest.Create(destinationUrl);

                    request.Method = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestMethod]);
                    foreach (var kvp in msg.BasicProperties.GetHttpHeaders()) {
                        if (Constants.HttpRestrictedHeaders.Contains(kvp.Key)) {
                            continue;
                        } else if (Constants.HttpRestrictedHeadersViaProperty.Contains(kvp.Key)) {
                            if ("Accept".Equals(kvp.Key)) {
                                request.Accept = kvp.Value;
                            } else if ("Content-Type".Equals(kvp.Key)) {
                                request.ContentType = kvp.Value;
                            }
                        } else {
                            request.Headers.Add(kvp.Key, kvp.Value);
                        }
                    }
                    request.ContentLength = msg.Body.Length;
                    if (msg.Body.Length > 0) {
                        var requestStream = request.GetRequestStream();
                        requestStream.Write(msg.Body, 0, msg.Body.Length);
                        requestStream.Close();
                    }

                    Func<WebResponse> CallWebService = () => {
                        if ((bool)msg.BasicProperties.Headers[Constants.RequestIsAuthenticated]) {
                            // TODO: handshake Authentication
                            //using (var impersonationContext = context.Request.LogonUserIdentity.Impersonate()) {
                            //    request.Credentials = CredentialCache.DefaultNetworkCredentials;
                            //    return request.GetResponse();
                            //}
                        }
                        return request.GetResponse();
                    };
                    // forward
                    try {
                        channel.BasicAck(msg.DeliveryTag, false);
                        var response = CallWebService();
                        var basicproperties = CreateResponseBasicProperties(200, "OK", response.Headers.AllKeys.ToDictionary(k => k, k => response.Headers[k]));
                        basicproperties.CorrelationId = msg.BasicProperties.CorrelationId;
                        var buffer = new byte[response.ContentLength];
                        if (response.ContentLength > 0) {
                            var responseStream = response.GetResponseStream();
                            responseStream.Read(buffer, 0, buffer.Length);
                            responseStream.Close();
                        }
                        channel.TxSelect();
                        channel.BasicPublish("", msg.BasicProperties.ReplyTo, basicproperties, buffer);
                        channel.TxCommit();
                    } catch (WebException ex) {
                        IBasicProperties basicProperties;
                        if (ex.Response != null) {
                            basicProperties = CreateResponseBasicProperties((int)(ex.Response as HttpWebResponse).StatusCode, (ex.Response as HttpWebResponse).StatusDescription, ex.Response.Headers.AllKeys.ToDictionary(k => k, k => ex.Response.Headers[k]));
                        } else {
                            basicProperties = CreateResponseBasicProperties((int)HttpStatusCode.ServiceUnavailable, "Service Unavailable", new Dictionary<string, string>());
                        }
                        basicProperties.CorrelationId = msg.BasicProperties.CorrelationId;
                        channel.BasicPublish("", msg.BasicProperties.ReplyTo, basicProperties, new byte[0]);
                    }

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
