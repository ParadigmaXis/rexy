using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Routes {
    [Serializable]
    public class RouteNotFoundException : Exception {
        public string Route { get; private set; }
        public RouteNotFoundException(string route) { Route = route; }
        public RouteNotFoundException(string route, string message) : base(message) { Route = route; }
        public RouteNotFoundException(string route, string message, Exception inner) : base(message, inner) { Route = route; }
        protected RouteNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
