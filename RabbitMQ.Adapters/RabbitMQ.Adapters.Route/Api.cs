using RabbitMQ.Adapters.Routes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Routes {
    public class Api {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Api));
        private static string ROUTES_FILE_PATH = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase) + "\\routes.xml";
        private IEnumerable<Route> routes;

        private static Api _api;
        public static Api GetApi {
            get {
                if (_api == null)
                    _api = new Api();
                return _api;
            }
        }

        private Api() {
            var parser = new ParseRoutesFile();
            routes = parser.XmlToRoutes(parser.LoadRawRoutes(ROUTES_FILE_PATH));
            foreach (var route in routes) {
                logger.DebugFormat("Configured route {0}: {1} -> {2}", route.Name, route.Path, route.Destination);
            }
            
        }

        public Route GetRoute(string origin) {
            return routes.SingleOrDefault(r => r.Path.Equals(origin));
        }
    }
}
