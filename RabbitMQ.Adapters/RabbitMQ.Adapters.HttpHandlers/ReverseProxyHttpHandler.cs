using RabbitMQ.Adapters.Common;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Diagnostics;

namespace RabbitMQ.Adapters.HttpHandlers {

    class QueueTimeoutException : Exception { }

    public class ReverseProxyHttpHandler : IHttpHandler {
        bool IHttpHandler.IsReusable {
            get { return true; }
        }

        void IHttpHandler.ProcessRequest(HttpContext context) {
            //const string IN_URL = "http://aura/rabbitmq-adapters/helloworld/HelloWorldService.asmx";
            const string OUT_URL = "http://127.0.0.1/helloworld/HelloWorldService.asmx";
            var url = new UriBuilder(OUT_URL);
            if (!String.IsNullOrEmpty(context.Request.Url.Query)) {
                url.Query = context.Request.Url.Query.Substring(1);
            }
            System.Diagnostics.EventLog.WriteEntry("ASP.NET 4.0.30319.0", String.Format("Redirect {0} to {1}{2}", context.Request.Url, url, context.Request.IsAuthenticated ? " with authentication" : ""));

            try {
                //// prepare request forwarding
                //var request = (HttpWebRequest)WebRequest.Create(url.Uri);

                //request.Method = context.Request.HttpMethod;
                //if (context.Request.AcceptTypes != null) {
                //    request.Accept = String.Join(", ", context.Request.AcceptTypes);
                //}
                //request.ContentType = context.Request.ContentType;
                //request.UserAgent = context.Request.UserAgent;
                //request.ContentLength = context.Request.ContentLength;

                //foreach (var key in context.Request.Headers.AllKeys.ExceptHttpRestrictedHeaders()) {
                //    request.Headers.Add(key, context.Request.Headers[key]);
                //}
                //if (context.Request.ContentLength > 0) {
                //    var buffer = GetRequestBuffer(context);

                //    var outStream = request.GetRequestStream();
                //    outStream.Write(buffer, 0, context.Request.ContentLength);
                //    outStream.Close();
                //}
                //Func<WebResponse> CallWebService = () => {
                //    if (context.Request.IsAuthenticated) {
                //        using (var impersonationContext = context.Request.LogonUserIdentity.Impersonate()) {
                //            request.Credentials = CredentialCache.DefaultNetworkCredentials;
                //            return request.GetResponse();
                //        }
                //    }
                //    return request.GetResponse();
                //};
                //// forward
                //var response = CallWebService();

                ////foreach (var key in response.Headers.AllKeys.ExceptHttpRestrictedHeaders()) {
                ////    System.Diagnostics.EventLog.WriteEntry("ASP.NET 4.0.30319.0", String.Format("Setting Header[{0}] to {1}", key, response.Headers[key]));
                ////    context.Response.Headers.Add(key, response.Headers[key]);
                ////}
                //context.Response.ContentType = response.ContentType;
                //if (response.ContentLength > 0) {
                //    var inStream = response.GetResponseStream();
                //    var buffer = new byte[response.ContentLength];
                //    inStream.Read(buffer, 0, (int)response.ContentLength);
                //    try {
                //        var document = new XmlDocument();
                //        document.Load(new System.IO.MemoryStream(buffer, 0, buffer.Length));
                //        if (document.DocumentElement.NamespaceURI == "http://schemas.xmlsoap.org/wsdl/" && document.DocumentElement.LocalName == "definitions") {
                //            var nsmgr = new XmlNamespaceManager(new NameTable());
                //            nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
                //            nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/wsdl/soap/");
                //            nsmgr.AddNamespace("soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
                //            var nodes = document.DocumentElement.SelectNodes("/wsdl:definitions/wsdl:service/wsdl:port/soap:address", nsmgr);
                //            var nodes12 = document.DocumentElement.SelectNodes("/wsdl:definitions/wsdl:service/wsdl:port/soap12:address", nsmgr);
                //            foreach (var node in nodes.Cast<XmlElement>().Concat(nodes12.Cast<XmlElement>())) {
                //                var attr = node.Attributes.GetNamedItem("location");
                //                attr.Value = attr.Value.Replace(OUT_URL, IN_URL);
                //            }
                //            using (var ms = new System.IO.MemoryStream()) {
                //                using (var writer = new System.IO.StreamWriter(ms, System.Text.Encoding.UTF8)) {
                //                    document.Save(writer);
                //                }
                //                buffer = ms.ToArray();
                //            }
                //            nsmgr.AddNamespace("s", "http://www.w3.org/2001/XMLSchema");
                //            var includes = document.DocumentElement.SelectNodes("/wsdl:definitions/s:schema/s:include", nsmgr);
                //            var imports = document.DocumentElement.SelectNodes("/wsdl:definitions/s:schema/s:import", nsmgr);
                //            foreach (var i in includes.Cast<XmlElement>().Concat(imports.Cast<XmlElement>())) {
                //                var attr = i.Attributes.GetNamedItem("schemaLocation");
                //                attr.Value = attr.Value.Replace(OUT_URL, IN_URL);
                //            }
                //        }
                //    } catch (Exception ex) {
                //        // FIXME: log the exception somewhere
                //        // response is not XML, so don't process it
                //    }
                //    var outStream = context.Response.OutputStream;
                //    outStream.Write(buffer, 0, buffer.Length);
                //    outStream.Close();
                //}

                var basicProperties = HttpRequestToRabbitMQBasicProperties(context.Request);
                var body = context.Request.GetRequestBytes();
                var requestMsg = new RabbitMQMessage(basicProperties, body);
                try {
                    if (context.Request.IsAuthenticated) {
                        using (var impersonation = context.Request.LogonUserIdentity.Impersonate()) {
                            var responseMsg = PostAndWait(requestMsg);
                            RabbitMQMessageToHttpResponse(responseMsg, context.Response);
                        }
                    } else {
                        var responseMsg = PostAndWait(requestMsg);
                        RabbitMQMessageToHttpResponse(responseMsg, context.Response);
                    }
                } catch (QueueTimeoutException ex) {
                    throw new HttpException(504, "Gateway Timeout");
                }
            } catch (WebException ex) {
                if (ex.Response != null) {
                    context.Response.StatusCode = (int)(ex.Response as HttpWebResponse).StatusCode;
                    context.Response.StatusDescription = (ex.Response as HttpWebResponse).StatusDescription;
                } else {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
                context.Response.End();
                return;
            }
        }

        private IBasicProperties HttpRequestToRabbitMQBasicProperties(HttpRequest request) {
            return CreateRequestBasicProperties(request.HttpMethod, request.Url, new Uri("http://localhost:8888/helloworld/HelloWorldService.asmx"), ExtracthttpRequestHeaders(request), request.IsAuthenticated);
        }

        internal IBasicProperties CreateRequestBasicProperties(string requestMethod, Uri requestGatewayUrl, Uri requestDestinationUrl, Dictionary<string, string> requestHeaders, bool requestIsAuthenticated) {
            var result = new RabbitMQ.Client.Framing.BasicProperties() {
                Headers = new Dictionary<string, object>()
            };
            var requestDestinationUrlWithQuery = new UriBuilder(requestDestinationUrl) { Query = requestGatewayUrl.Query.StartsWith("?") ? requestGatewayUrl.Query.Substring(1) : "" }.Uri;
            result.Headers.Add(Constants.RequestMethod, requestMethod);
            result.Headers.Add(Constants.RequestGatewayUrl, requestGatewayUrl.ToString());
            result.Headers.Add(Constants.RequestDestinationUrl, requestDestinationUrlWithQuery.ToString());
            result.Headers.Add(Constants.RequestIsAuthenticated, requestIsAuthenticated);
            requestHeaders.ToList().ForEach(kvp => result.Headers.Add(Constants.HttpHeaderPrefix + kvp.Key, kvp.Value));
            return result;
        }

        private Dictionary<string, string> ExtracthttpRequestHeaders(HttpRequest request) {
            return request.Headers.AllKeys.ToDictionary(k => k, k => request.Headers[k]);
        }

        private void RabbitMQMessageToHttpResponse(RabbitMQMessage msg, HttpResponse response) {
            foreach (var kvp in msg.BasicProperties.GetHttpHeaders()) {
                if ("Content-Type".Equals(kvp.Key)) {
                    response.ContentType = kvp.Value;
                } else {
                    response.Headers.Add(kvp.Key, kvp.Value);
                }
            }
            if (msg.BasicProperties.Headers.ContainsKey(Constants.ResponseStatusCode)) {
                response.StatusCode = (int)msg.BasicProperties.Headers[Constants.ResponseStatusCode];
                response.StatusDescription = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.ResponseStatusDescription]);
            }
            if (msg.Body.Length > 0) {
                var outStream = response.OutputStream;
                outStream.Write(msg.Body, 0, msg.Body.Length);
                outStream.Close();
            }
            return;
        }

        private RabbitMQMessage PostAndWait(RabbitMQMessage requestMsg) {
            var factory = new ConnectionFactory { HostName = "AURA", VirtualHost = "/", UserName = "isa-http-handler", Password = "isa-http-handler" };
            using (var connection = factory.CreateConnection()) {
                using (var channel = connection.CreateModel()) {
                    channel.BasicAcks += (sender, e) => Debug.WriteLine(string.Format("RPHH::ACK {0} {1}", e.DeliveryTag, e.Multiple));
                    channel.BasicNacks += (sender, e) => Debug.WriteLine(string.Format("RPHH::NACK {0} {1} {2}", e.DeliveryTag, e.Multiple, e.Requeue));
                    channel.BasicRecoverOk += (sender, e) => Debug.WriteLine(string.Format("RPHH::RECOVER_OK"));
                    channel.BasicReturn += (sender, e) => Debug.WriteLine(string.Format("RPHH::RETURN ..."));
                    channel.CallbackException += (sender, e) => Debug.WriteLine(string.Format("RPHH::CALLBACK_EXCEPTION {0}", e.Exception.Message));
                    channel.ModelShutdown += (sender, e) => Debug.WriteLine(string.Format("RPHH::MODEL_SHUTDOWN ..."));

                    var privateQueue = channel.QueueDeclare();
                    var consumer = new QueueingBasicConsumer(channel);
                    channel.BasicConsume(privateQueue.QueueName, false, consumer);

                    requestMsg.BasicProperties.CorrelationId = Guid.NewGuid().ToString();
                    requestMsg.BasicProperties.ReplyTo = privateQueue.QueueName;
                    channel.BasicPublish(new PublicationAddress(ExchangeType.Headers, Constants.WebServiceAdapterExchange, ""), requestMsg.BasicProperties, requestMsg.Body);

                    BasicDeliverEventArgs msg = null;
                    var provider = new WindowsAuthenticationProvider(
                        (basicProperties, body) => { channel.BasicPublish("", msg.BasicProperties.ReplyTo, basicProperties, body); }
                        );
                    while (consumer.Queue.Dequeue(600000, out msg)) {
                        if (provider.IsAuthenticationMessage(msg)) {
                            provider.HandleAuthenticationMessage(privateQueue.QueueName, msg);
                        } else {
                            //assert msg.BasicProperties.CorrelationId == basicProperties.CorrelationId
                            Debug.WriteLine("Got a reply!");
                            return new RabbitMQMessage {
                                BasicProperties = msg.BasicProperties,
                                Body = msg.Body
                            };
                        }
                    }
                    Debug.WriteLine("Timeout");
                    // TODO: log timeout
                    throw new QueueTimeoutException();
                }
            }
        }
    }
}