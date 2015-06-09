using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.WebServiceCaller
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
#if (DEBUG)
            var service = new WebServiceCallerService();
            //service.Process();
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new WebServiceCallerService()
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}
