using RabbitMQ.Adapters.Route;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Route {
    public class Api {
        private static string ROUTES_FILE_PATH = AppDomain.CurrentDomain.BaseDirectory + "\\routes.xml";
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
        }

        public Route GetRoute(string origin) {
            return routes.SingleOrDefault(r => r.Path.Equals(origin));
        }
    }
}
