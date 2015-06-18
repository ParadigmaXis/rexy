using RabbitMQ.Adapters.Common;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.WebServiceCaller {

    public partial class WebServiceCallerService : ServiceBase {
        private System.Threading.Thread serviceThread;
        private System.Threading.ManualResetEvent serviceStopEvent = new System.Threading.ManualResetEvent(false);
        public WebServiceCallerService() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {
            serviceThread = new System.Threading.Thread(Main);
            serviceThread.Start();
        }

        protected override void OnStop() {
            serviceStopEvent.Set();
            serviceThread.Join();
            serviceStopEvent.Reset();
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
                    var tasks = new List<Task>();
                    const int WAIT_TIMEOUT_MILLISECOND = 500;
                    while (!serviceStopEvent.WaitOne(0)) {
                        if (tasks.Count >= Environment.ProcessorCount * 4) {
                            var finished = Task.WaitAny(tasks.ToArray(), WAIT_TIMEOUT_MILLISECOND);
                            if (finished >= 0) {
                                tasks.RemoveAt(finished);
                            } else {
                                Console.WriteLine("Waiting for: tasks to finish; exit signal.");
                            }
                        } else {
                            Client.Events.BasicDeliverEventArgs msg;
                            if (consumer.Queue.Dequeue(WAIT_TIMEOUT_MILLISECOND, out msg)) {
                                tasks.Add(Task.Factory.StartNew(() => { HandleRabbitMQRequestMessage(msg, connection.CreateModel()); }, TaskCreationOptions.LongRunning));
                                channel.BasicAck(msg.DeliveryTag, false);
                            } else {
                                Console.WriteLine("Waiting for: messages to arrive; exit signal.");
                            }
                        }
                    }
                }
            }
        }

        private void HandleRabbitMQRequestMessage(Client.Events.BasicDeliverEventArgs msg, IModel channel) {
            var requestMsg = new RabbitMQMessage(msg.BasicProperties, msg.Body);
            var request = RabbitMQMessageToHttpRequest(requestMsg);

            var requestIsAuthenticated = (bool)msg.BasicProperties.Headers[Constants.RequestIsAuthenticated];
            var routingKey = msg.BasicProperties.ReplyTo;
            var correlationId = msg.BasicProperties.CorrelationId;

            var response = CallWebservice(request, () => {
                if (requestIsAuthenticated) {
                    return new RabbitMQWindowsAuthenticator(channel, routingKey);
                }else {
                    return new RabbitMQAnonimousAuthenticator();
                }
            });
            var responseMsg = HttpResponseToRabbitMQMessage(response);
            responseMsg.BasicProperties.CorrelationId = correlationId;
            channel.BasicPublish("", routingKey, responseMsg.BasicProperties, responseMsg.Body);
        }

        private HttpWebResponse CallWebservice(HttpWebRequest request, Func<IRabbitMQAuthenticator> GetAuthenticator) {
            WebResponse response = null;
            try {
                using (var authenticator = GetAuthenticator()) {
                    authenticator.Authenticate(request);
                    response = request.GetResponse();
                }
            } catch (WebException ex) {
                if (ex.Response != null) {
                    response = ex.Response;
                } else {
                    response = null;
                }
            }
            return (HttpWebResponse)response;
        }

        private RabbitMQMessage HttpResponseToRabbitMQMessage(HttpWebResponse response) {
            var responseMsg = new RabbitMQMessage();
            if (response != null) { 
                responseMsg.BasicProperties = CreateResponseBasicProperties((int)(response).StatusCode, response.StatusDescription, response.Headers.AllKeys.ToDictionary(k => k, k => response.Headers[k]));
                responseMsg.Body = response.GetResponseBytes();
            } else {
                responseMsg.BasicProperties = CreateResponseBasicProperties((int)HttpStatusCode.ServiceUnavailable, "Service Unavailable", new Dictionary<string, string>());
                responseMsg.Body = new byte[0];
            }
            return responseMsg;
        }

        private static HttpWebRequest RabbitMQMessageToHttpRequest(RabbitMQMessage msg) {
            //var gatewayUrl = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestGatewayUrl]);
            var destinationUrl = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestDestinationUrl]);
            var request = (HttpWebRequest)WebRequest.Create(destinationUrl);

            request.Method = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestMethod]);
            RecoverHttpHeadersToRequest(msg.BasicProperties.GetHttpHeaders(), request);
            request.ContentLength = msg.Body.Length;
            if (msg.Body.Length > 0) {
                var requestStream = request.GetRequestStream();
                requestStream.Write(msg.Body, 0, msg.Body.Length);
                requestStream.Close();
            }
            return request;
        }

        private static void RecoverHttpHeadersToRequest(IDictionary<string, string> httpHeaders, HttpWebRequest request) {
            foreach (var kvp in httpHeaders) {
                if (kvp.Key == "Authorization") {
                    continue;
                }
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