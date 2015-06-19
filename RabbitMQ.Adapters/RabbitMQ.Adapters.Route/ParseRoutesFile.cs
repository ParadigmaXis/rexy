using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RabbitMQ.Adapters.Route {
    public class ParseRoutesFile {
        public ParseRoutesFile() { }

        internal XElement LoadRawRoutes(string filePath) {
            return XElement.Load(filePath);
        }

        internal IEnumerable<Route> XmlToRoutes(XElement routes) {
            return routes.Descendants("route").Select(r => new Route(r.Attribute("name").Value, r.Element("path").Value, r.Element("destination").Value));
        }
    }
}
