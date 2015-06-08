using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace RabbitMQ.Adapters.HttpHandlers {
    public class ReverseProxyHttpHandler: IHttpHandler/*, IHttpHandlerFactory*/ {
        bool IHttpHandler.IsReusable {
            get { return true; }
        }
        private static String[] RestrictedHeaders { get { return new String[] { "Connection", "Content-Length", "Date", "Expect", "Host", "If-Modified-Since", "Range", "Transfer-Encoding", "Proxy-Connection" }; } }
        private static String[] RestrictedHeadersViaProperty { get { return new String[] { "Accept", "Content-Type", "Referer", "User-Agent" }; } }

        void IHttpHandler.ProcessRequest(HttpContext context) {
            const string IN_URL = "http://aura/rabbitmq-adapters/helloworld/HelloWorldService.asmx";
            const string OUT_URL = "http://127.0.0.1/helloworld/HelloWorldService.asmx";
            var url = new UriBuilder(OUT_URL);
            if (!String.IsNullOrEmpty(context.Request.Url.Query)) {
                url.Query = context.Request.Url.Query.Substring(1);
            }
            System.Diagnostics.EventLog.WriteEntry("ASP.NET 4.0.30319.0", String.Format("Redirect {0} to {1}{2}", context.Request.Url, url, context.Request.IsAuthenticated ? " with authentication" : ""));
            
            try {
                // prepare request forwarding
                var request = (HttpWebRequest)WebRequest.Create(url.Uri);

                request.Method = context.Request.HttpMethod;
                if (context.Request.AcceptTypes != null) {
                    request.Accept = String.Join(", ", context.Request.AcceptTypes);
                }
                request.ContentType = context.Request.ContentType;
                request.UserAgent = context.Request.UserAgent;
                request.ContentLength = context.Request.ContentLength;

                foreach (var key in request.Headers.AllKeys.Except(RestrictedHeaders).Except(RestrictedHeadersViaProperty)) {
                    request.Headers.Add(key, context.Request.Headers[key]);
                }
                if (context.Request.ContentLength > 0) {
                    var inStream = context.Request.GetBufferedInputStream();
                    var buffer = new byte[context.Request.ContentLength];
                    inStream.Read(buffer, 0, context.Request.ContentLength);

                    var outStream = request.GetRequestStream();
                    outStream.Write(buffer, 0, context.Request.ContentLength);
                    outStream.Close();
                }
                Func<WebResponse> CallWebService = () => {
                    if (context.Request.IsAuthenticated) {
                        using (var impersonationContext = context.Request.LogonUserIdentity.Impersonate()) {
                            request.Credentials = CredentialCache.DefaultNetworkCredentials;
                            return request.GetResponse();
                        }
                    }
                    return request.GetResponse();
                };
                // forward
                var response = CallWebService();

                //foreach (var key in response.Headers.AllKeys.Except(RestrictedHeaders).Except(RestrictedHeadersViaProperty)) {
                //    System.Diagnostics.EventLog.WriteEntry("ASP.NET 4.0.30319.0", String.Format("Setting Header[{0}] to {1}", key, response.Headers[key]));
                //    context.Response.Headers.Add(key, response.Headers[key]);
                //}
                context.Response.ContentType = response.ContentType;
                if (response.ContentLength > 0) {
                    var inStream = response.GetResponseStream();
                    var buffer = new byte[response.ContentLength];
                    inStream.Read(buffer, 0, (int)response.ContentLength);
                    var document = new XmlDocument();
                    document.Load(new System.IO.MemoryStream(buffer, 0, buffer.Length));
                    if (document.DocumentElement.NamespaceURI == "http://schemas.xmlsoap.org/wsdl/" && document.DocumentElement.LocalName == "definitions") {
                        var nsmgr = new XmlNamespaceManager(new NameTable());
                        nsmgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
                        nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/wsdl/soap/");
                        nsmgr.AddNamespace("soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
                        var nodes = document.DocumentElement.SelectNodes("/wsdl:definitions/wsdl:service/wsdl:port/soap:address", nsmgr);
                        var nodes12 = document.DocumentElement.SelectNodes("/wsdl:definitions/wsdl:service/wsdl:port/soap12:address", nsmgr);
                        foreach (var node in nodes.Cast<XmlElement>().Concat(nodes12.Cast<XmlElement>())) {
                            var attr = node.Attributes.GetNamedItem("location");
                            attr.Value = attr.Value.Replace(OUT_URL, IN_URL);
                        }
                        using (var ms = new System.IO.MemoryStream()) {
                            using (var writer = new System.IO.StreamWriter(ms, System.Text.Encoding.UTF8)) {
                                document.Save(writer);
                            }
                            buffer = ms.ToArray();
                        }
                        nsmgr.AddNamespace("s", "http://www.w3.org/2001/XMLSchema");
                        var includes = document.DocumentElement.SelectNodes("/wsdl:definitions/s:schema/s:include", nsmgr);
                        var imports = document.DocumentElement.SelectNodes("/wsdl:definitions/s:schema/s:import", nsmgr);
                        foreach (var i in includes.Cast<XmlElement>().Concat(imports.Cast<XmlElement>())) {
                            var attr = i.Attributes.GetNamedItem("schemaLocation");
                            attr.Value = attr.Value.Replace(OUT_URL, IN_URL);
                        }
                    }
                    var outStream = context.Response.OutputStream;
                    outStream.Write(buffer, 0, buffer.Length);
                    outStream.Close();
                }
            } catch (WebException ex) {
                if (ex.Response != null) {
                    context.Response.StatusCode = (int)(ex.Response as HttpWebResponse).StatusCode;
                    context.Response.StatusDescription = (ex.Response as HttpWebResponse).StatusDescription;
                }
                else {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
                context.Response.End();
                return;
            }
        }

        //IHttpHandler IHttpHandlerFactory.GetHandler(HttpContext context, string requestType, string url, string pathTranslated) {
        //    return new ReverseProxyHttpHandler();
        //}

        //void IHttpHandlerFactory.ReleaseHandler(IHttpHandler handler) {
            
        //}
    }
}