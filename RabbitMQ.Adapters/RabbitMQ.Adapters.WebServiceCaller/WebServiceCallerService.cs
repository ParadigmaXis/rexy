using log4net;
using RabbitMQ.Adapters.Common;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.WebServiceCaller {

    public partial class WebServiceCallerService : ServiceBase {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private System.Threading.Thread serviceThread;
        private System.Threading.ManualResetEvent serviceStopEvent = new System.Threading.ManualResetEvent(false);
        public WebServiceCallerService() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {
            logger.Info("Starting WebService Caller Service...");
            serviceThread = new System.Threading.Thread(Main);
            serviceThread.Start();
            logger.Info("WebService Caller Service started.");
        }

        protected override void OnStop() {
            serviceStopEvent.Set();
            serviceThread.Join();
            serviceStopEvent.Reset();
        }

        public void Main() {
            try { 
                var factory = new ConnectionFactory {
                    HostName = ConfigurationManager.AppSettings["HostName"],
                    VirtualHost = ConfigurationManager.AppSettings["VirtualHost"],
                    UserName = ConfigurationManager.AppSettings["UserName"],
                    Password = ConfigurationManager.AppSettings["Password"]
                };
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
                                    logger.Debug("Request message processed.");
                                } else {
                                    logger.Debug("Waiting for: tasks to finish; exit signal.");
                                }
                            } else {
                                Client.Events.BasicDeliverEventArgs msg;
                                if (consumer.Queue.Dequeue(WAIT_TIMEOUT_MILLISECOND, out msg)) {
                                    logger.Debug("Request message arrived...");
                                    tasks.Add(Task.Factory.StartNew(() => { HandleRabbitMQRequestMessage(msg, connection.CreateModel()); }, TaskCreationOptions.LongRunning));
                                    channel.BasicAck(msg.DeliveryTag, false);
                                } else {
                                    //logger.Debug("Waiting for: messages to arrive; exit signal.");
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                logger.Error(e.Message);
            }
        }

        private void HandleRabbitMQRequestMessage(Client.Events.BasicDeliverEventArgs msg, IModel channel) {
            logger.Debug("Handling Request Message...");

            var userPrincipalName = NormalizeUserPrincipalName(Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.UserPrincipalName]));
            msg.BasicProperties.Headers.Remove(Constants.UserPrincipalName);

            WindowsIdentity identity = null;
            try {
                identity = new WindowsIdentity(userPrincipalName);
            } catch(Exception e) {
                logger.Error(e);
            }

            if (identity != null) {
                using (identity.Impersonate()) {
                    logger.DebugFormat("Now I'm {0}.", WindowsIdentity.GetCurrent().Name);
                    logger.DebugFormat("Impersonation level: {0}.", identity.ImpersonationLevel);

                    CallWebService(msg, channel);
                }
            } else {
                CallWebService(msg, channel);
            }

            logger.Debug("Request Message Handle.");
        }

        private void CallWebService(Client.Events.BasicDeliverEventArgs msg, IModel channel) {
            var requestMsg = new RabbitMQMessage(msg.BasicProperties, msg.Body);

            var request = RabbitMQMessageToHttpRequest(requestMsg);
            var requestIsAuthenticated = (bool)msg.BasicProperties.Headers[Constants.RequestIsAuthenticated];
            var routingKey = msg.BasicProperties.ReplyTo;
            var correlationId = msg.BasicProperties.CorrelationId;

            var responseMsg = new RabbitMQMessage();

            try {
                logger.Debug("Calling WebService...");
                var response = (HttpWebResponse)request.GetResponse();
                logger.Debug("WebService Called.");
                logger.DebugFormat("Content length: {0}.", response.ContentLength);
                responseMsg = HttpResponseToRabbitMQMessage(response);
                logger.DebugFormat("Response received: {0}", Constants.GetUTF8String(responseMsg.Body));
            } catch (WebException ex) {
                logger.ErrorFormat("Error calling service: {0}", ex.Message);
                var exx = ex.InnerException;
                while (exx != null) {
                    logger.ErrorFormat("\t{0}", exx.Message);
                    exx = exx.InnerException;
                }
                if (ex.Response != null) {
                    responseMsg = HttpResponseToRabbitMQMessage((HttpWebResponse)ex.Response);
                } else {
                    responseMsg = HttpResponseToRabbitMQMessage(null);
                }
            }

            responseMsg.BasicProperties.CorrelationId = correlationId;
            channel.BasicPublish("", routingKey, responseMsg.BasicProperties, responseMsg.Body);
        }

        private string NormalizeUserPrincipalName(string userPrincipalName) {
            var ret = userPrincipalName;

            logger.DebugFormat("Normalizing UserPrincipalName {0}...", ret);
            
            if (userPrincipalName.Contains('\\')) {
                var domain = userPrincipalName.Split('\\')[0];
                var username = userPrincipalName.Split('\\')[1];
                ret = string.Format("{0}@{1}", username, domain);
            }

            logger.DebugFormat("Normalized UserPrincipalName: {0}", ret);

            return ret;
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
            logger.Debug("Converting AMQP Message to Http Request...");
            
            var destinationUrl = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.RequestDestinationUrl]);

            logger.DebugFormat("Destination URL: {0}", destinationUrl);

            var request = (HttpWebRequest)WebRequest.Create(destinationUrl);
            request.Credentials = CredentialCache.DefaultNetworkCredentials;
            request.Method = Constants.GetUTF8String((byte[])msg.BasicProperties.Headers[Constants.RequestMethod]);
            
            logger.DebugFormat("Method: {0}", request.Method);
            logger.DebugFormat("ContentLength: {0}", msg.Body.Length);
            logger.DebugFormat("Content: {0}", Constants.GetUTF8String(msg.Body));

            RecoverHttpHeadersToRequest(msg.BasicProperties.GetHttpHeaders(), request);
            request.ContentLength = msg.Body.Length;
            if (msg.Body.Length > 0) {
                var requestStream = request.GetRequestStream();
                requestStream.Write(msg.Body, 0, msg.Body.Length);
                requestStream.Close();
            }

            logger.Debug("AMQP Message converted to Http Request.");

            return request;
        }

        private static void RecoverHttpHeadersToRequest(IDictionary<string, string> httpHeaders, HttpWebRequest request) {
            foreach (var kvp in httpHeaders) {
                logger.DebugFormat("Header {0} - {1}", kvp.Key, kvp.Value);

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