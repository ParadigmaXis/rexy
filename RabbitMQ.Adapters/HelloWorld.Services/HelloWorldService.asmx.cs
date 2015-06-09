﻿using System;
using System.Collections.Generic;
using System.Linq;
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

        [WebMethod]
        public string HelloWorld(string message) {
            string userName = Context.Request.IsAuthenticated ? Context.Request.LogonUserIdentity.Name : "<anonymous>";
            if (message == null) {
                return "Hello to " + userName + " with null argument!";
            }
            return "Hello to " + userName + " with argument: " + message;
        }
    }
}