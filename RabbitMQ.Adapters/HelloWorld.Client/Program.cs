using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelloWorld.Client {
    class Program {
        static void Main(string[] args) {
            var start = DateTime.UtcNow;
            var tasks = new List<Task>();
            for (int i = 0; i < 1; i++) {
                RunIt();
                //tasks.Add(Task.Factory.StartNew(RunIt));
            }
            Console.WriteLine("All Running {0}", DateTime.UtcNow - start);
            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("All DONE {0}", DateTime.UtcNow - start);
            Console.ReadKey();
        }
        static void RunIt() {
            var start = DateTime.UtcNow;
            var client = new ServiceReferences.HelloWorldServiceSoapClient("HelloWorldServiceSoap");
            client.ClientCredentials.UseIdentityConfiguration = true;
            client.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
            client.ClientCredentials.Windows.ClientCredential = System.Net.CredentialCache.DefaultNetworkCredentials;
            var txt = client.HelloWorld("Hello??");
            Console.WriteLine("DONE {0}: {1} {2}", txt, start, DateTime.UtcNow - start);
        }
    }
}
