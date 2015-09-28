using RabbitMQ.Adapters.Common;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using RabbitMQ.Adapters.Routes;
using log4net;
using System.Security.Principal;

namespace RabbitMQ.Adapters.HttpHandlers {

    class QueueTimeoutException : Exception { }

    public class ReverseProxyHttpHandler : IHttpHandler {

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        bool IHttpHandler.IsReusable {
            get { return true; }
        }

        void IHttpHandler.ProcessRequest(HttpContext context) {
            logger.Debug("Processing request...");

            try {
                try {
                    if (context.Request.IsAuthenticated) {
                        logger.InfoFormat("Authenticated {0} using {1} at level {2}", context.Request.LogonUserIdentity.Name, context.Request.LogonUserIdentity.AuthenticationType, context.Request.LogonUserIdentity.ImpersonationLevel);

                        using (var impersonation = context.Request.LogonUserIdentity.Impersonate()) {
                            try {
                                GetResponse(context.Request, context.Response);
                            } catch (Exception ex) {
                                logger.Error(ex.Message, ex);
                                throw;
                            }
                        }
                    } else {
                        logger.Info("Unauthenticated");
                        GetResponse(context.Request, context.Response);
                    }
                } catch (QueueTimeoutException) {
                    throw new HttpException(504, "Gateway Timeout");
                }
            } catch (WebException ex) {
                logger.ErrorFormat("WebException: {0}", ex.Message);

                if (ex.Response != null) {
                    context.Response.StatusCode = (int)(ex.Response as HttpWebResponse).StatusCode;
                    context.Response.StatusDescription = (ex.Response as HttpWebResponse).StatusDescription;
                } else {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
                context.Response.End();
                return;
            } catch (RouteNotFoundException ex) {
                logger.ErrorFormat("RouteNotFoundException: {0} {1}", ex.Message, ex.Route);
                context.Response.StatusCode = 404;
                context.Response.StatusDescription = "Not found.";
                context.Response.Write("404 Route not found.");
                context.Response.End();
            }

            logger.Debug("Request processed.");
        }

        private void GetResponse(HttpRequest request, HttpResponse response) {
            logger.DebugFormat("Request URL: {0}", request.Url);
            var proxyTargetPath = GetProxyTargetPath(request);
            var basicProperties = HttpRequestToRabbitMQBasicProperties(request);
            var body = request.GetRequestBytes();
            var requestMsg = new RabbitMQMessage(basicProperties, body);
            
            var responseMsg = PostAndWait(requestMsg);
            ReplaceBodyURLs(responseMsg,
                GetDestinationURL(proxyTargetPath),
                GetProxyTargetURL(request, proxyTargetPath));
            RabbitMQMessageToHttpResponse(responseMsg, response);
        }

        private string GetProxyTargetPath(HttpRequest request) {
            return request.Url.AbsolutePath.Substring(GetProxyTargetURL(request).AbsolutePath.Length);
        }

        private Uri GetProxyTargetURL(HttpRequest request, string relativePath = "") {
            return new Uri(new Uri(new Uri(request.Url.GetLeftPart(UriPartial.Authority)), request.ApplicationPath.TrimEnd('/') + "/"), relativePath);
        }

        private Uri GetDestinationURL(string targetProxyPath) {
            logger.DebugFormat("GetDestinationURL({0})", targetProxyPath);
            var route = Api.GetApi.GetRoute(targetProxyPath);
            if (route == null) {
                throw new RouteNotFoundException(targetProxyPath);
            }
            return route.Destination;
        }

        private void ReplaceBodyURLs(RabbitMQMessage responseMsg, Uri destinationUrl, Uri proxyTargetUrl) {
            var isGzipCompressed =
                responseMsg.BasicProperties.Headers.ContainsKey("http-Content-Encoding") &&
                Constants.GetUTF8String((byte[])responseMsg.BasicProperties.Headers["http-Content-Encoding"]) == "gzip";
            string body;
            if (isGzipCompressed) {
                body = Constants.GetZippedUTF8String(responseMsg.Body);
            } else {
                body = Constants.GetUTF8String(responseMsg.Body);
            }

            try {
                var document = XDocument.Parse(body);
                if (document.IsWsdl()) {
                    logger.Debug("Is WSDL Content.");
                    var ports = document.Root
                        .Descendants(XName.Get("service", "http://schemas.xmlsoap.org/wsdl/"))
                        .SelectMany(d => d.Descendants(XName.Get("port", "http://schemas.xmlsoap.org/wsdl/")))
                        .ToList();
                    var nodes = ports.SelectMany(d => d.Descendants(XName.Get("address", "http://schemas.xmlsoap.org/wsdl/soap/")));
                    var nodes12 = ports.SelectMany(d => d.Descendants(XName.Get("address", "http://schemas.xmlsoap.org/wsdl/soap12/")));
                    foreach (var node in nodes.Concat(nodes12)) {
                        var attr = node.Attribute(XName.Get("location"));
                        attr.Value = attr.Value.Replace(destinationUrl.ToString(), proxyTargetUrl.ToString());
                    }

                    var schemas = document.Root
                        .Descendants(XName.Get("schema", "http://www.w3.org/2001/XMLSchema"))
                        .ToList();

                    var includes = schemas.SelectMany(d => d.Descendants(XName.Get("include", "http://www.w3.org/2001/XMLSchema")));
                    var imports = schemas.SelectMany(d => d.Descendants(XName.Get("import", "http://www.w3.org/2001/XMLSchema")));
                    foreach (var i in includes.Concat(imports)) {
                        var attr = i.Attribute(XName.Get("schemaLocation"));
                        attr.Value = attr.Value.Replace(destinationUrl.ToString(), proxyTargetUrl.ToString());
                    }

                    using (var outms = new MemoryStream()) {
                        if (isGzipCompressed) {
                            using (var zipStream = new GZipStream(outms, CompressionMode.Compress, false)) {
                                document.Save(zipStream);
                            }
                        } else {
                            document.Save(outms);
                        }
                        responseMsg.Body = outms.ToArray();
                        responseMsg.BasicProperties.Headers["http-Content-Length"] = Encoding.UTF8.GetBytes(responseMsg.Body.Length.ToString());
                    }
                } else if (document.IsSoapEnvelope()) {
                    logger.Debug("Is SOAP Envelope Content.");
                    //TODO: process soap messages. Remove soap envelope?
                } else {
                    logger.Debug("Is unknown Content.");
                }
            } catch (XmlException ex) {
                logger.Info("Response will not be processed (it's not xml).", ex);
            } catch (Exception ex) {
                logger.Error("Exception thrown while processing message to replace body URLs.", ex);
            }
        }

        private IBasicProperties HttpRequestToRabbitMQBasicProperties(HttpRequest request) {
            logger.Debug("HttpRequestToRabbitMQBasicProperties...");

            var method = request.HttpMethod;
            var gatewayUrl = request.Url;
            logger.Debug("requestUrl begin");
            var requestUrl = GetDestinationURL(GetProxyTargetPath(request));
            logger.Debug("requestUrl");
            var isAuthenticated = request.IsAuthenticated;
            var identity = request.LogonUserIdentity;

            var properties = CreateRequestBasicProperties(method, gatewayUrl, requestUrl, ExtracthttpRequestHeaders(request), isAuthenticated, identity);

            logger.Debug("HttpRequestToRabbitMQBasicProperties done.");

            return properties;
        }

        internal IBasicProperties CreateRequestBasicProperties(string requestMethod, Uri requestGatewayUrl, Uri requestDestinationUrl, Dictionary<string, string> requestHeaders, bool requestIsAuthenticated, WindowsIdentity identity) {
            logger.Debug("Creating Basic Properties...");

            var result = new RabbitMQ.Client.Framing.BasicProperties() {
                Headers = new Dictionary<string, object>()
            };
            var requestDestinationUrlWithQuery = new UriBuilder(requestDestinationUrl) { Query = requestGatewayUrl.Query.StartsWith("?") ? requestGatewayUrl.Query.Substring(1) : "" }.Uri;
            result.Headers.Add(Constants.RequestMethod, requestMethod);
            result.Headers.Add(Constants.RequestGatewayUrl, requestGatewayUrl.ToString());
            result.Headers.Add(Constants.RequestDestinationUrl, requestDestinationUrlWithQuery.ToString());
            result.Headers.Add(Constants.RequestIsAuthenticated, requestIsAuthenticated);
            if (requestIsAuthenticated) {
                result.Headers.Add(Constants.UserPrincipalName, identity.Name);
            }
            requestHeaders.ToList().ForEach(kvp => result.Headers.Add(Constants.HttpHeaderPrefix + kvp.Key, kvp.Value));

            logger.Debug("Basic Properties Created.");
            return result;
        }

        private Dictionary<string, string> ExtracthttpRequestHeaders(HttpRequest request) {
            return request.Headers.AllKeys.ToDictionary(k => k, k => request.Headers[k]);
        }

        private void RabbitMQMessageToHttpResponse(RabbitMQMessage msg, HttpResponse response) {
            foreach (var kvp in msg.BasicProperties.GetHttpHeaders()) {
                if ("Content-Type".Equals(kvp.Key)) {
                    logger.DebugFormat("Content-Type: {0}", kvp.Value);
                    response.ContentType = kvp.Value;
                } else {
                    response.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            if (msg.BasicProperties.Headers.ContainsKey(Constants.ResponseStatusCode)) {
                response.StatusCode = (int)msg.BasicProperties.Headers[Constants.ResponseStatusCode];
                response.StatusDescription = Constants.GetUTF8String(msg.BasicProperties.Headers[Constants.ResponseStatusDescription]);
            }

            logger.DebugFormat("Writing {0} bytes to response stream...", msg.Body.Length);

            if (msg.Body.Length > 0) {
                var outStream = response.OutputStream;
                outStream.Write(msg.Body, 0, msg.Body.Length);
                outStream.Close();
            }

            logger.Debug("Bytes written.");
        }

        private RabbitMQMessage PostAndWait(RabbitMQMessage requestMsg) {
            logger.Debug("Sending AMQP message...");

            using (var channel = Global.Connection.CreateModel()) {
                channel.BasicAcks += (sender, e) => logger.Debug(string.Format("RPHH::ACK {0} {1}", e.DeliveryTag, e.Multiple));
                channel.BasicNacks += (sender, e) => logger.Debug(string.Format("RPHH::NACK {0} {1} {2}", e.DeliveryTag, e.Multiple, e.Requeue));
                channel.BasicRecoverOk += (sender, e) => logger.Debug(string.Format("RPHH::RECOVER_OK"));
                channel.BasicReturn += (sender, e) => logger.Debug(string.Format("RPHH::RETURN ..."));
                channel.CallbackException += (sender, e) => logger.Debug(string.Format("RPHH::CALLBACK_EXCEPTION {0}", e.Exception.Message));
                channel.ModelShutdown += (sender, e) => logger.Debug(string.Format("RPHH::MODEL_SHUTDOWN ..."));

                var privateQueue = channel.QueueDeclare();
                var consumer = new QueueingBasicConsumer(channel);
                channel.BasicConsume(privateQueue.QueueName, false, consumer);

                requestMsg.BasicProperties.CorrelationId = Guid.NewGuid().ToString();
                requestMsg.BasicProperties.ReplyTo = privateQueue.QueueName;
                channel.BasicPublish(new PublicationAddress(ExchangeType.Headers, Constants.WebServiceAdapterExchange, ""), requestMsg.BasicProperties, requestMsg.Body);

                logger.Debug("AMQP message sent.");

                BasicDeliverEventArgs msg = null;
                var timeout = !consumer.Queue.Dequeue(600000, out msg);

                if (timeout) {
                    logger.Warn("Timeout waiting for response.");
                    throw new QueueTimeoutException();
                }

                logger.DebugFormat("Got a reply: {0}", Constants.GetUTF8String(msg.Body));

                return new RabbitMQMessage {
                    BasicProperties = msg.BasicProperties,
                    Body = msg.Body
                };
            }
        }
    }
}