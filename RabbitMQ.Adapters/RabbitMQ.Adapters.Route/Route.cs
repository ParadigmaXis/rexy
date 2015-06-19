using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.Routes {
    public class Route {

        public string Name { get; }
        public string Path { get; }
        public string Destination { get; }

        public Route (string name, string origin, string destination) {
            Name = name;
            Path = origin;
            Destination = destination;
        }
    }
}
