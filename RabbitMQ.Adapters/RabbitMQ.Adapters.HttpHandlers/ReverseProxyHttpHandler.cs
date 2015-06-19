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
using RabbitMQ.Adapters.Routes;
using log4net;

namespace RabbitMQ.Adapters.HttpHandlers {

    class QueueTimeoutException : Exception { }

    public class ReverseProxyHttpHandler : IHttpHandler {

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        bool IHttpHandler.IsReusable {
            get { return true; }
        }

        void IHttpHandler.ProcessRequest(HttpContext context) {
            // TODO: log something more useful
            //System.Diagnostics.EventLog.WriteEntry("ASP.NET 4.0.30319.0", String.Format("Redirect {0} to {1}{2}", context.Request.Url, url, context.Request.IsAuthenticated ? " with authentication" : ""));

            try {
                try {
                    if (context.Request.IsAuthenticated) {
                        using (var impersonation = context.Request.LogonUserIdentity.Impersonate()) {
                            GetResponse(context.Request, context.Response);
                        }
                    } else {
                        GetResponse(context.Request, context.Response);
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

        private void GetResponse(HttpRequest request, HttpResponse response) {
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
            return new Uri(Api.GetApi.GetRoute(targetProxyPath).Destination);
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
                var document = new XmlDocument();
                document.LoadXml(body);
                if (document.IsWsdl()) {
                    var nsmgr = new XmlNamespaceManager(new NameTable());
                    nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
                    nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/wsdl/soap/");
                    nsmgr.AddNamespace("soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
                    var nodes = document.DocumentElement.SelectNodes("/wsdl:definitions/wsdl:service/wsdl:port/soap:address", nsmgr);
                    var nodes12 = document.DocumentElement.SelectNodes("/wsdl:definitions/wsdl:service/wsdl:port/soap12:address", nsmgr);
                    foreach (var node in nodes.Cast<XmlElement>().Concat(nodes12.Cast<XmlElement>())) {
                        var attr = node.Attributes.GetNamedItem("location");
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

                    nsmgr.AddNamespace("s", "http://www.w3.org/2001/XMLSchema");
                    var includes = document.DocumentElement.SelectNodes("/wsdl:definitions/s:schema/s:include", nsmgr);
                    var imports = document.DocumentElement.SelectNodes("/wsdl:definitions/s:schema/s:import", nsmgr);
                    foreach (var i in includes.Cast<XmlElement>().Concat(imports.Cast<XmlElement>())) {
                        var attr = i.Attributes.GetNamedItem("schemaLocation");
                        attr.Value = attr.Value.Replace(destinationUrl.ToString(), proxyTargetUrl.ToString());
                    }
                } else if (document.IsSoapMessage()) {
                    //TODO: process soap messages. Remove soap envelope?
                }
            } catch (XmlException ex) {
                logger.Info("Response will not be processed (it's not xml).", ex);
            } catch (Exception ex) {
                logger.Error("Exception thrown while processing message to replace body URLs.", ex);
            }
        }

        private IBasicProperties HttpRequestToRabbitMQBasicProperties(HttpRequest request) {
            return CreateRequestBasicProperties(request.HttpMethod, request.Url, GetDestinationURL(GetProxyTargetPath(request)), ExtracthttpRequestHeaders(request), request.IsAuthenticated);
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
            using (var channel = Global.Connection.CreateModel()) {
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