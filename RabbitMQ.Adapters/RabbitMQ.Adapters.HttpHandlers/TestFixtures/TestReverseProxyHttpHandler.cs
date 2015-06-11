using NUnit.Framework;
using RabbitMQ.Adapters.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace RabbitMQ.Adapters.HttpHandlers.TestFixtures {
    [TestFixture]
    public class TestReverseProxyHttpHandler: AssertionHelper {
        [Test]
        public void RunMe() {
            var requestMethod = "POST";
            var requestGatewayUrl = new Uri("http://localhost:8888/adapter/helloworld/HelloWorld.asmx");
            var requestDestinationUrl = new Uri("http://localhost:8888/helloworld/HelloWorld.asmx");
            var requestHeaders = new Dictionary<String, String>() {
                { "Host", "localhost" },
                { "Content-Type", "text/xml; charset=utf-8" },
                { "Content-Length", "0" },
                { "SOAPAction", "http://www.paradigmaxis.pt/isa/2015/06/08/hello-world/HelloWorld" }
            };
            var requestBody =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "  <soap:Body>" +
                "    <HelloWorld xmlns=\"http://www.paradigmaxis.pt/isa/2015/06/08/hello-world/\">" +
                "      <message>string</message>" +
                "    </HelloWorld>" +
                "  </soap:Body>" +
                "</soap:Envelope>";
            var requestIsAuthenticated= false;
            var requestLogonUserIdentity = (WindowsIdentity)null;

            var basicProperties = new ReverseProxyHttpHandler().CreateBasicProperties(requestMethod, requestGatewayUrl, requestDestinationUrl, requestHeaders, requestIsAuthenticated);
            Expect(basicProperties, Is.Not.Null);
            Expect(basicProperties.Headers, Is.Not.Null);
            Expect(basicProperties.Headers.Count, Is.EqualTo(4 + requestHeaders.Count));
            Expect(basicProperties.Headers.Keys, Is.EquivalentTo(requestHeaders.Keys.Select(k => "http-" + k).Concat(new String[] { Constants.RequestMethod, Constants.RequestGatewayUrl, Constants.RequestDestinationUrl, Constants.RequestIsAuthenticated })));
            
            // Operation

            var responseStatusCode = Int32.MinValue;
            var responseStatusDescription = String.Empty;
            var responseHeaders = new Dictionary<String, String>();
            var responseBody = new byte[0];
        }
    }
}
