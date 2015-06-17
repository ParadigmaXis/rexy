using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Adapters.WebServiceCaller {
    static class Program {
        public static readonly String SERVICE_NAME = "WebServiceCallerService";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args) {
            var actions = new Dictionary<string, Action>();
            actions.Add("-c", RunConsole);
            actions.Add("-i", Install);
            actions.Add("-u", Uninstall);
            if (args.Length == 1) {
                Action action;
                if (actions.TryGetValue(args[0], out action)) {
                    action();
                } else {
                    Console.WriteLine("Usage: {0} [flag]", typeof(Program).Assembly.FullName);
                    Console.WriteLine("\t-c\t Run as console");
                    Console.WriteLine("\t-i\t Install Windows Service");
                    Console.WriteLine("\t-u\t Uninstall Windows Service");
                }
            } else {
                RunService();
            }
        }

        static void RunConsole() {
            new WebServiceCallerService().Main();
        }

        static void RunService() {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new WebServiceCallerService()
            };
            ServiceBase.Run(ServicesToRun);
        }

        static void Install() {
            try {
                InstallService();
                StartService();
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }

        static void Uninstall() {
            StopService();
            UninstallService();
        }

        private static bool IsInstalled() {
            using (ServiceController controller =
                new ServiceController(SERVICE_NAME)) {
                try {
                    ServiceControllerStatus status = controller.Status;
                } catch {
                    return false;
                }
                return true;
            }
        }

        private static bool IsRunning() {
            using (ServiceController controller =
                new ServiceController(SERVICE_NAME)) {
                if (!IsInstalled()) return false;
                return (controller.Status == ServiceControllerStatus.Running);
            }
        }

        private static AssemblyInstaller GetInstaller() {
            AssemblyInstaller installer = new AssemblyInstaller(
                typeof(Program).Assembly, null);
            installer.UseNewContext = true;
            return installer;
        }

        private static void InstallService() {
            if (IsInstalled()) return;

            try {
                using (AssemblyInstaller installer = GetInstaller()) {
                    System.Collections.IDictionary state = new System.Collections.Hashtable();
                    try {
                        installer.Install(state);
                        installer.Commit(state);
                    } catch {
                        try {
                            installer.Rollback(state);
                        } catch { }
                        throw;
                    }
                }
            } catch {
                throw;
            }
        }

        private static void UninstallService() {
            if (!IsInstalled()) return;
            try {
                using (AssemblyInstaller installer = GetInstaller()) {
                    System.Collections.IDictionary state = new System.Collections.Hashtable();
                    try {
                        installer.Uninstall(state);
                    } catch {
                        throw;
                    }
                }
            } catch {
                throw;
            }
        }

        private static void StartService() {
            if (!IsInstalled()) return;

            using (ServiceController controller =
                new ServiceController(SERVICE_NAME)) {
                try {
                    if (controller.Status != ServiceControllerStatus.Running) {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running,
                            TimeSpan.FromSeconds(10));
                    }
                } catch {
                    throw;
                }
            }
        }

        private static void StopService() {
            if (!IsInstalled()) return;
            using (ServiceController controller =
                new ServiceController(SERVICE_NAME)) {
                try {
                    if (controller.Status != ServiceControllerStatus.Stopped) {
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped,
                             TimeSpan.FromSeconds(10));
                    }
                } catch {
                    throw;
                }
            }
        }
    }
}
