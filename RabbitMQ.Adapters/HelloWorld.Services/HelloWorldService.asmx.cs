using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Services;

namespace HelloWorld.Services {
    /// <summary>
    /// Summary description for HelloWorldService
    /// </summary>
    [WebService(Namespace = "http://www.paradigmaxis.pt/isa/2015/06/08/hello-world/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    public class HelloWorldService: System.Web.Services.WebService {

        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [WebMethod]
        public string HelloWorld(string message) {
            logger.Debug("Handling Hello World Request...");

            if (Context.Request.IsAuthenticated) {
                using (var impersonate = Context.Request.LogonUserIdentity.Impersonate()) {
                    try {
                        var filename = WindowsIdentity.GetCurrent().Name + "footprints.txt";
                        var footprint = DateTime.Now.ToString("O");
                        System.IO.File.AppendAllLines("C:\\ISA\\HelloWorldService\\" + filename, new string[1] { footprint });
                    } catch (Exception ex) {
                        logger.Error(ex.Message);
                    }
                }
            }

            string userName = Context.Request.IsAuthenticated ? Context.Request.LogonUserIdentity.Name : "<anonymous>";
            var ret = "Hello to " + userName;

            if (message == null) {
                ret += " with null argument!";
            } else {
                ret += " with argument: " + message;
            }

            logger.Debug("Hello World Request handled.");

            return ret;
        }
    }
}
