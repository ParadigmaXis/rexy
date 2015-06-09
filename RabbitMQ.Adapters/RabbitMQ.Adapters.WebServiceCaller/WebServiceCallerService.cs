using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.WebServiceCaller
{
    public partial class WebServiceCallerService : ServiceBase
    {
        public WebServiceCallerService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            //new System.Threading.Thread(Main);
        }

        protected override void OnStop()
        {
            // AskToStop(thread);
            //thread.Join();
        }

        public void Main() {
        }
    }
}
