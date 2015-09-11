using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace RabbitMQ.Adapters.Common {
    public static class Constants {
        public static String[] HttpRestrictedHeaders { get { return new String[] { "Connection", "Content-Length", "Date", "Expect", "Host", "If-Modified-Since", "Range", "Transfer-Encoding", "Proxy-Connection" }; } }
        public static String[] HttpRestrictedHeadersViaProperty { get { return new String[] { "Accept", "Content-Type", "Referer", "User-Agent" }; } }
        public static IEnumerable<String> ExceptHttpRestrictedHeaders(this IEnumerable<String> self) {
            return self.Except(HttpRestrictedHeaders).Except(HttpRestrictedHeadersViaProperty);
        }

        public static IDictionary<String, String> GetHttpHeaders(this IBasicProperties basicProperties) {
            return basicProperties.Headers.Where(kvp => kvp.Key.StartsWith(HttpHeaderPrefix)).ToDictionary(kvp => kvp.Key.Substring(HttpHeaderPrefix.Length), kvp => GetUTF8String(kvp.Value));
        }

        public static byte[] GetResponseBytes(this WebResponse response) {
            var buffer = new byte[response.ContentLength];
            if (response.ContentLength > 0) {
                var output = new MemoryStream(buffer);
                using (var responseStream = response.GetResponseStream()) {
                    responseStream.CopyTo(output);
                }
            }
            return buffer;
        }

        public static byte[] GetRequestBytes(this HttpRequest request) {
            var buffer = new byte[request.ContentLength];
            if (request.ContentLength > 0) {
                var inStream = request.GetBufferedInputStream();
                inStream.Read(buffer, 0, request.ContentLength);
                inStream.Close();
            }
            return buffer;
        }

        public const String HttpHeaderPrefix = "http-";

        public const String RequestGatewayUrl = "request-GatewayUrl";
        public const String RequestDestinationUrl = "request-DestinationUrl";
        public const String RequestMethod = "request-Method";
        public const String RequestIsAuthenticated = "request-Authorization";
        public const String UserPrincipalName = "request-UserPrincipalName";

        public const String ResponseStatusCode = "response-StatusCode";
        public const String ResponseStatusDescription = "response-StatusDescription";

        public const String SoapAuthMessagetype = "SOAP-Auth";
        public const String WebServiceAdapterExchange = "isa.web-service-adapter";

        public const String ContentTypeOctetStream = "application/octet-stream";
        /// <summary>
        /// This method decodes byte[] as UTF8 Strings.
        /// </summary>
        /// <remarks>This is necessary because the RabbitMQ Client (and maybe AMQP?) does not dictate an encoding for strings used in header values.</remarks>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetUTF8String(object value) {
            if (value is string) return (string)value;
            return System.Text.Encoding.UTF8.GetString((byte[])value);
        }

        public static string GetZippedUTF8String(byte[] value) {
            string result;
            using (GZipStream stream = new GZipStream(new MemoryStream(value), CompressionMode.Decompress)) {
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                result = reader.ReadToEnd();
            }
            return result;
        }

    }
}
