using RabbitMQ.Adapters.Common;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;

namespace RabbitMQ.Adapters.HttpHandlers {
    public class Global : System.Web.HttpApplication {
        private static IConnection connection;
        public static IConnection Connection {
            get {
                lock (typeof(Global)) {
                    var factory = new ConnectionFactory { HostName = "AURA", VirtualHost = "/", UserName = "isa-http-handler", Password = "isa-http-handler" };
                    if (connection == null) {
                        connection = factory.CreateConnection();
                        connection.AutoClose = false;
                        connection.ConnectionShutdown += (sender, e) => connection = null;
                    }
                }
                return connection;
            }
        }
        protected void Application_Start(object sender, EventArgs e) {
            using (var channel = Connection.CreateModel()) {
                channel.TxSelect();
                channel.ExchangeDeclare(Constants.WebServiceAdapterExchange, ExchangeType.Headers, true, false, null);
                channel.TxCommit();
            }
        }

        protected void Session_Start(object sender, EventArgs e) {}

        protected void Application_BeginRequest(object sender, EventArgs e) {}

        protected void Application_AuthenticateRequest(object sender, EventArgs e) {}

        protected void Application_Error(object sender, EventArgs e) {}

        protected void Session_End(object sender, EventArgs e) {}

        protected void Application_End(object sender, EventArgs e) {}
    }
}