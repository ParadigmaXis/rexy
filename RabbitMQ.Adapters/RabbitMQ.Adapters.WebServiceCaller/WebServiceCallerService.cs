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
                    if (msg.BasicProperties.Headers.ContainsKey(Constants.RequestAccept)) {
                        request.Accept = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestAccept]);
                    }
                    request.ContentType = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestContentType]);
                    request.ContentLength = msg.Body.Length;
                    foreach (var key in msg.BasicProperties.Headers.Keys) {
                        if (key.StartsWith("http-")) {
                            var httpKey = key.Substring("http-".Length);
                            if (Constants.HttpRestrictedHeaders.Contains(httpKey) || Constants.HttpRestrictedHeadersViaProperty.Contains(httpKey)) {
                                continue;
                            }
                            request.Headers.Add(httpKey, Constants.GetUTF8String(msg.BasicProperties.Headers[key]));
                        }
                    }
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
                        var basicproperties = channel.CreateBasicProperties();
                        basicproperties.Headers = new Dictionary<String, Object>();
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
                        var basicproperties = channel.CreateBasicProperties();
                        basicproperties.Headers = new Dictionary<String, Object>();
                        if (ex.Response != null) {
                            basicproperties.Headers.Add(Constants.ResponseStatusCode, (int)(ex.Response as HttpWebResponse).StatusCode);
                            basicproperties.Headers.Add(Constants.ResponseStatusDescription, (ex.Response as HttpWebResponse).StatusDescription);
                        } else {
                            basicproperties.Headers.Add(Constants.ResponseStatusCode, (int)HttpStatusCode.InternalServerError);
                        }
                        basicproperties.CorrelationId = msg.BasicProperties.CorrelationId;
                        channel.BasicPublish("", msg.BasicProperties.ReplyTo, basicproperties, new byte[0]);
                    }

                }
            }
        }
    }
}
