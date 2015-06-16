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
        

        protected void Application_Start(object sender, EventArgs e) {
            using (var thisVariableForcesTheLoadingOfTheAssembly = new Microsoft.Samples.Security.SSPI.ClientContext(new Microsoft.Samples.Security.SSPI.ClientCredential(Microsoft.Samples.Security.SSPI.Credential.Package.Negotiate), "", Microsoft.Samples.Security.SSPI.ClientContext.ContextAttributeFlags.Delegate)) {
            }

            var factory = new ConnectionFactory { HostName = "AURA", VirtualHost = "/", UserName = "isa-http-handler", Password = "isa-http-handler" };
            using (var connection = factory.CreateConnection()) {
                using (var channel = connection.CreateModel()) {
                    channel.TxSelect();
                    channel.ExchangeDeclare(Constants.WebServiceAdapterExchange, ExchangeType.Headers, true, false, null);
                    channel.TxCommit();
                }
            }
        }
        protected void Session_Start(object sender, EventArgs e) {

        }

        protected void Application_BeginRequest(object sender, EventArgs e) {

        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e) {

        }

        protected void Application_Error(object sender, EventArgs e) {

        }

        protected void Session_End(object sender, EventArgs e) {

        }

        protected void Application_End(object sender, EventArgs e) {

        }
    }
}