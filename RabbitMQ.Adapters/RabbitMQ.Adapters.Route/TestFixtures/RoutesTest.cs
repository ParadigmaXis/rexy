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
        XElement rawRoutes;
        [SetUp]
        public void Init() {
            rawRoutes =
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
            var routes = parser.XmlToRoutes(rawRoutes);

            CollectionAssert.AllItemsAreInstancesOfType(routes, typeof(Route));
            Assert.AreEqual(2, routes.Count());
            Assert.AreEqual("webservice1", routes.ElementAt(0).Name);
            Assert.AreEqual("helloworld/HelloWorldService.asmx", routes.ElementAt(0).Path);
            Assert.AreEqual("http://aura:8888/helloworld/HelloWorldService.asmx", routes.ElementAt(0).Destination.ToString());
        }

        [Test]
        public void InvalideDestinationUriTest() {
            var rawRoute =
                new XElement("routes",
                    new XElement("route",
                        new XAttribute("name", "webservice1"),
                        new XElement("path", "helloworld/HelloWorldService.asmx"),
                        new XElement("destination", "invalid url")
                    )
                );
            var parser = new ParseRoutesFile();
            var routes = parser.XmlToRoutes(rawRoute);

            Assert.AreEqual(0, routes.Count());
        }
    }
}
