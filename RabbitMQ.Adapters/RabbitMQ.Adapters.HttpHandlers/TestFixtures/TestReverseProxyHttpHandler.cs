using NUnit.Framework;
using RabbitMQ.Adapters.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace RabbitMQ.Adapters.HttpHandlers.TestFixtures {
    [TestFixture]
    public class TestReverseProxyHttpHandler: AssertionHelper {
        private string requestMethod;
        private Uri requestGatewayUrl;
        private Uri requestDestinationUrl;
        private Dictionary<String, String> requestHeaders;
        private byte[] requestBody;
        private bool requestIsAuthenticated;
        private WindowsIdentity requestLogonUserIdentity;

        [SetUp]
        public void SetUp()
        {
            this.requestMethod = "POST";
            this.requestGatewayUrl = new Uri("http://localhost:8888/adapter/helloworld/HelloWorld.asmx");
            this.requestDestinationUrl = new Uri("http://localhost:8888/helloworld/HelloWorld.asmx");
            this.requestHeaders = new Dictionary<String, String>() {
                { "Host", "localhost" },
                { "Content-Type", "text/xml; charset=utf-8" },
                { "Content-Length", "0" },
                { "SOAPAction", "http://www.paradigmaxis.pt/isa/2015/06/08/hello-world/HelloWorld" }
            };
            this.requestBody = System.Text.Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "  <soap:Body>" +
                "    <HelloWorld xmlns=\"http://www.paradigmaxis.pt/isa/2015/06/08/hello-world/\">" +
                "      <message>string</message>" +
                "    </HelloWorld>" +
                "  </soap:Body>" +
                "</soap:Envelope>");
            this.requestIsAuthenticated = false;
            this.requestLogonUserIdentity = (WindowsIdentity)null;
        }

        [Test]
        public void CreateBasicProperties()
        {
            var basicProperties = new ReverseProxyHttpHandler().CreateRequestBasicProperties(this.requestMethod, this.requestGatewayUrl, this.requestDestinationUrl, this.requestHeaders, this.requestIsAuthenticated, this.requestLogonUserIdentity);
            Expect(basicProperties, Is.Not.Null);
            Expect(basicProperties.Headers, Is.Not.Null);
            Expect(basicProperties.Headers.Count, Is.EqualTo(4 + this.requestHeaders.Count));
            Expect(basicProperties.Headers.Keys, Is.EquivalentTo(this.requestHeaders.Keys.Select(k => "http-" + k).Concat(new String[] { Constants.RequestMethod, Constants.RequestGatewayUrl, Constants.RequestDestinationUrl, Constants.RequestIsAuthenticated })));
        }

        [Test]
        public void ExtractHttpHeaders()
        {
            var basicProperties = new ReverseProxyHttpHandler().CreateRequestBasicProperties(this.requestMethod, this.requestGatewayUrl, this.requestDestinationUrl, this.requestHeaders, this.requestIsAuthenticated, this.requestLogonUserIdentity);

            var headers = basicProperties.GetHttpHeaders();
            Expect(headers.Keys, Is.EquivalentTo(requestHeaders.Keys));
        }
    }
}
