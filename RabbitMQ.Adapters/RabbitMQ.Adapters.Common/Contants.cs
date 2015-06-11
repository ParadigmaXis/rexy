using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Common
{
    public static class Constants
    {
        public static String[] HttpRestrictedHeaders { get { return new String[] { "Connection", "Content-Length", "Date", "Expect", "Host", "If-Modified-Since", "Range", "Transfer-Encoding", "Proxy-Connection" }; } }
        public static String[] HttpRestrictedHeadersViaProperty { get { return new String[] { "Accept", "Content-Type", "Referer", "User-Agent" }; } }
        public static IEnumerable<String> ExceptHttpRestrictedHeaders(this IEnumerable<String> self) {
            return self.Except(HttpRestrictedHeaders).Except(HttpRestrictedHeadersViaProperty);
        }
        public static IDictionary<String, String> GetHttpHeaders(this IBasicProperties basicProperties) {
            return basicProperties.Headers.Where(kvp => kvp.Key.StartsWith(HttpHeaderPrefix)).ToDictionary(kvp => kvp.Key.Substring(HttpHeaderPrefix.Length), kvp => GetUTF8String(kvp.Value));
        }

        public const String HttpHeaderPrefix = "http-";

        public const String RequestGatewayUrl = "request-GatewayUrl";
        public const String RequestDestinationUrl = "request-DestinationUrl";
        public const String RequestMethod = "request-Method";
        public const String RequestIsAuthenticated = "request-Authorization";

        public const String ResponseStatusCode = "response-StatusCode";
        public const String ResponseStatusDescription = "response-StatusDescription";

        public const String SoapAuthMessagetype = "SOAP-Auth";
        public const String WebServiceAdapterExchange = "isa.web-service-adapter";

        /// <summary>
        /// This method decodes byte[] as UTF8 Strings.
        /// </summary>
        /// <remarks>This is necessary because the RabbitMQ Client (and maybe AMQP?) does not dictate an encoding for strings used in header values.</remarks>
        /// <param name="header"></param>
        /// <returns></returns>
        public static String GetUTF8String(object header) {
            if (header is string) return (string)header;
            return System.Text.Encoding.UTF8.GetString((byte[])header);
        }

    }
}
