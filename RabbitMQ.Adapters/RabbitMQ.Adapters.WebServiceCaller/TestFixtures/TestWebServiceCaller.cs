using NUnit.Framework;
using RabbitMQ.Adapters.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.WebServiceCaller {
    [TestFixture]
    public class TestWebServiceCaller : AssertionHelper {
        private int responseStatusCode;
        private String responseStatusDescription;
        private Dictionary<String, String> responseHeaders;
        private byte[] responseBody;

        [SetUp]
        public void SetUp() {
            this.responseStatusCode = 200;
            this.responseStatusDescription = "OK";
            this.responseHeaders = new Dictionary<string, string>() {
                { "Content-Type", "text/xml; charset=utf-8" },
                { "Content-Length", "0" }
            };
            this.responseBody = System.Text.Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "  <soap:Body>" +
                "    <HelloWorldResponse xmlns=\"http://www.paradigmaxis.pt/isa/2015/06/08/hello-world/\">" +
                "      <HelloWorldResult>string</HelloWorldResult>" +
                "    </HelloWorldResponse>" +
                "  </soap:Body>" +
                "</soap:Envelope>");
        }

        [Test]
        public void CreateBasicProperties() {
            var basicProperties = new WebServiceCallerService().CreateResponseBasicProperties(responseStatusCode, responseStatusDescription, responseHeaders);
            Expect(basicProperties, Is.Not.Null);
            Expect(basicProperties.Headers, Is.Not.Null);
            Expect(basicProperties.Headers.Count, Is.EqualTo(2 + responseHeaders.Count));
            Expect(basicProperties.Headers.Keys, Is.EquivalentTo(responseHeaders.Keys.Select(k => "http-" + k).Concat(new string[] { Constants.ResponseStatusCode, Constants.ResponseStatusDescription })));
        }

        [Test]
        public void ExtractHttpHeaders() {
            var basicProperties = new WebServiceCallerService().CreateResponseBasicProperties(responseStatusCode, responseStatusDescription, responseHeaders);

            var headers = basicProperties.GetHttpHeaders();
            Expect(headers.Keys, Is.EquivalentTo(responseHeaders.Keys));
        }
    }
}