using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using NUnit.Framework;
using System.Xml;

namespace RabbitMQ.Adapters.Routes.TestFixtures {
    [TestFixture]
    public class RoutesTest {
        XElement raw_routes;
        [SetUp]
        public void Init() {
            raw_routes =
                new XElement("routes",
                    new XElement("route",
                        new XAttribute("name", "webservice1"),
                        new XElement("path", "helloworld/HelloWorldService.asmx"),
                        new XElement("destination", "http://aura:8888/helloworld/HelloWorldService.asmx")),
                    new XElement("route",
                        new XAttribute("name", "webservice2"),
                        new XElement("path", "test/TestService.asmx"),
                        new XElement("destination", "amqp://service1")
                    )
                );
        }

        [Test]
        public void ReadRouteXMLTest() {
            var parser = new ParseRoutesFile();
            var routes = parser.XmlToRoutes(raw_routes);

            CollectionAssert.AllItemsAreInstancesOfType(routes, typeof(Route));
            Assert.AreEqual(2, routes.Count());
            Assert.AreEqual("webservice1", routes.ElementAt(0).Name);
            Assert.AreEqual("helloworld/HelloWorldService.asmx", routes.ElementAt(0).Path);
            Assert.AreEqual("http://aura:8888/helloworld/HelloWorldService.asmx", routes.ElementAt(0).Destination);
        }

        [Test]
        public void ApiTest() {
            var r = Api.GetApi.GetRoute("helloworld/HelloWorldService.asmx");

            Assert.IsNotNull(r);
            Assert.AreEqual("http://localhost:8888/helloworld/HelloWorldService.asmx", r.Destination);
        }
    }
}
