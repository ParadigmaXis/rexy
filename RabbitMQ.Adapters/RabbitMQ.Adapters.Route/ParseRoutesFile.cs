using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RabbitMQ.Adapters.Routes {
    public class ParseRoutesFile {
        public ParseRoutesFile() { }

        internal XElement LoadRawRoutes(string filePath) {
            return XElement.Load(filePath);
        }

        internal IEnumerable<Route> XmlToRoutes(XElement rawRoutes) {
            var routes = new List<Route>();
            Uri uri;
            rawRoutes.Descendants("route").ToList().ForEach(r => {
                if (ValidateDestinationUrl(r.Element("destination").Value, out uri))
                    routes.Add(new Route(r.Attribute("name").Value, r.Element("path").Value, uri));
            });
            return routes;
        }

        private static bool ValidateDestinationUrl(string url, out Uri uri) {
            return Uri.TryCreate(url, UriKind.Absolute, out uri);
        }
    }
}
