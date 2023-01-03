using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NetLearnService
{
    public partial class Service1 : ServiceBase
    {
        public static System.IO.StreamWriter log;
        public static List<Server> List_Server = new List<Server>();
        public static List<Service> List_Service = new List<Service>();
        public static List<String> List_ConvKey = new List<String>();
        public static List<int> List_Receive = new List<int>();
        public static List<int> List_Send = new List<int>();
        public static string myversion = "1.01";
        public static IPAddress mylocalip;
        public static int Verbose = 0;
        public static int Duration = 0;
        public static int hDuration = 0;
        public static int mDuration = 0;
        public static int sDuration = 0;
        public static bool VerboseInput = false;
        public static bool IpInput = false;
        public static bool DurationInput = false;
        public static TraceEventSession LearnSession;
        public static DateTime dtend;
        public Thread CaptureThread;

        public class Server
        {
            public int Port;
            public string ProcessName;
            public long OutPackets;
            public long InPackets;
            public IPAddress ClientIP;
            public Server(int port, string processName, long outPackets, long inPackets, IPAddress clientIP)
            {
                Port = port;
                ProcessName = processName;
                OutPackets = outPackets;
                InPackets = inPackets;
                ClientIP = clientIP;
            }
        }
        public class Service
        {
            public int ProcessId;
            public string ServiceName;
            public Service(int processId, string serviceName)
            {
                ProcessId = processId;
                ServiceName = serviceName;
            }
        }
        public Service1()
        {
            InitializeComponent();
        }
        public static Service GetServiceName(int pid)
        {
            return List_Service.Find(x => (x.ProcessId == pid));

        }
        public static void CaptureEvents()
        {
             
            LearnSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);

            LearnSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
            LearnSession.Source.Kernel.TcpIpAccept += Kernel_TcpIpAccept;
            LearnSession.Source.Kernel.TcpIpConnect += Kernel_TcpIpConnect;
            LearnSession.Source.Kernel.TcpIpRecv += Kernel_TcpIpRecv;
            LearnSession.Source.Kernel.TcpIpSend += Kernel_TcpIpSend;

            dtend = DateTime.Now.AddSeconds(Duration);

            Thread backgroundThread = new Thread(new ThreadStart(ThreadTask));
            backgroundThread.IsBackground = true;
            backgroundThread.Start();
            LearnSession.Source.Process();
        }
        public static void AddServices()
        {
            ServiceController[] scServices;
            scServices = ServiceController.GetServices();

            foreach (ServiceController Service in scServices)
            {
                if (Service.Status == ServiceControllerStatus.Running)
                {
                    try
                    {
                        Service s = new Service(0, Service.ServiceName);
                        ManagementObject wmiServiceObj;
                        wmiServiceObj = new ManagementObject("Win32_Service.Name='" + Service.ServiceName + "'");
                        wmiServiceObj.Get();
                        s.ProcessId = int.Parse(wmiServiceObj["ProcessId"].ToString());
                        List_Service.Add(s);
                    }
                    catch (Exception e)
                    {

                    }
                }
            }
        }
        public static void AddListeners()
        {
            
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            var endpointlist = properties.GetActiveTcpListeners();
            foreach (IPEndPoint p in endpointlist)
            {
                if (!p.Address.ToString().Contains("127.0.0.1"))
                {
                    Server s = new Server(p.Port, "NA", 0, 0, IPAddress.Parse("0.0.0.0"));
                    List_Server.Add(s);
                }
            }
        }
        public static void UpdateServeurWithActiveTcpConnections()
        {
            Console.WriteLine("Active TCP Connections");
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();

            foreach (TcpConnectionInformation c in connections)
            {
                Console.WriteLine("{0} <==> {1}",
                    c.LocalEndPoint.ToString(),
                    c.RemoteEndPoint.ToString());
            }
        }
        public static bool GetLocalIPAddress(IPAddress myip)
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                IPAddress Ip = IPAddress.Parse(ip.ToString());
                if ((ip.AddressFamily == AddressFamily.InterNetwork) &&
                    myip.Equals(Ip))
                {
                    return true;
                }
            }
            return false;
        }
         static void  WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\NetLearn_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {

                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
        }
        static void logit(int level, string msg)
        {
            DateTime dateTime = DateTime.Now;
            if (Verbose <= level)
            {
            Service1.WriteToFile(dateTime.ToString() + " " + msg);

            }
        }
        static bool ParseArgs(String[] args)
        {
            int i = 0;

            foreach (string arg in args)
            {
                
                if (arg.Equals("-i"))
                {
                    try
                    {
                        mylocalip = IPAddress.Parse(args[i + 1]);
                        IpInput = true;
                    }
                    catch (Exception e)
                    {
                        logit(0, "Specify a valid IP address");
                    }

                }
                if (arg.Equals("-v"))
                {
                    try
                    {
                        Verbose = int.Parse(args[i + 1]);
                        if (Verbose > 4)
                        {
                            logit(0, "Specify verbositylevel  0-4");
                            break;
                        }
                        else
                        {
                            VerboseInput = true;
                        }
                    }
                    catch (Exception e)
                    {
                        logit(0, "Specify a valid verbosity (0-4 ) level or default is 0 ");
                        break;
                    }
                }

                if (arg.Equals("-t"))
                {
                    DurationInput = true;
                    string DurationPattern = @"^([0-1]?\d|2[0-3])(?::([0-5]?\d))?(?::([0-5]?\d))?$";
                    Regex rg = new Regex(DurationPattern);

                    if (rg.IsMatch(args[i + 1]))
                    {

                        var spl = args[i + 1].Split(':');
                        hDuration = int.Parse(spl[0]);
                        Duration = hDuration * 3600;
                        mDuration = int.Parse(spl[1]);
                        Duration = Duration + (mDuration * 60);
                        sDuration = int.Parse(spl[2]);
                        Duration = Duration + sDuration;

                    }
                    else
                    {
                        return false;
                    }
                }


                i++;
            }
            if (!DurationInput)
                Duration = 60;

            if (!VerboseInput)
                Verbose = 0;

            if (!IpInput)
                return false;

            return true;
        }
       
        private static void ThreadTask()
        {
            int stp;
            int newval;
            Random rnd = new Random();

            while (true)
            {
                if (DateTime.Now > dtend)
                {
                    
                   // LearnSession.Stop();
                    printconv();
                    break;
                }
                Thread.Sleep(5000);
            }
        }

        protected override void OnStart(string[] args)
        {
            WriteToFile("Service is started at " + DateTime.Now);
            if (!ParseArgs(args))
            {
                logit(0, "Use Netlearn -i mylocalip -v verbosity -t hh:mm:ss");
                this.Stop();
                return;
            }
            logit(0, String.Format("Netlearn {0} is starting with ip : {1}  verbose level {2} and for {3} seconds\n", myversion, mylocalip.ToString(), Verbose, Duration));

            if (!GetLocalIPAddress(mylocalip))
            {
                logit(0, "No IP address found");
                this.Stop();
            }

            logit(0, String.Format("using {0} to learn TCP traffic ", mylocalip.ToString()));

            AddListeners();

            if (!(TraceEventSession.IsElevated() ?? false))
            {
                logit(0, "Please run Netlearn with elevated permission");

            }
            AddServices();
            CaptureThread  = new Thread(new ThreadStart(CaptureEvents));
            CaptureThread.IsBackground = true;
            CaptureThread.Start();
            //   CaptureEvents();

            return;

            logit(0, "NetLearn ended");
            int idx = 0;

            foreach (string s in List_ConvKey)
            {
             //   Console.WriteLine(s + ";" + List_Send[idx] + ";" + List_Receive[idx]);
                logit(0,s + ";" + List_Send[idx] + ";" + List_Receive[idx]);
                idx++;
            }
            this.Stop();
        }
        public static void printconv()
        {
            int idx = 0;
           // WriteToFile("Service is stopped at " + DateTime.Now);
            foreach (string s in List_ConvKey)
            {
                //   Console.WriteLine(s + ";" + List_Send[idx] + ";" + List_Receive[idx]);
                logit(0, s + ";" + List_Send[idx] + ";" + List_Receive[idx]);
                idx++;
            }
        }

        protected override void OnStop()

        {
            int idx = 0;
            WriteToFile("Service is stopped at " + DateTime.Now);
            LearnSession.Stop();
            foreach (string s in List_ConvKey)
            {
                //   Console.WriteLine(s + ";" + List_Send[idx] + ";" + List_Receive[idx]);
                logit(0, s + ";" + List_Send[idx] + ";" + List_Receive[idx]);
                idx++;
            }
        }
        private static void Kernel_TcpIpSend(Microsoft.Diagnostics.Tracing.Parsers.Kernel.TcpIpSendTraceData obj)
        {
            if (!obj.saddr.Equals(mylocalip))
                return;
            String DIR = "";
            String key = "";
            String Pro = GetProcessName(obj.ProcessID, obj.ProcessName);
            int idx = 0;

            if (List_Server.Find(x => (x.Port == obj.sport)) == null)
            {
                DIR = "CLIENT";
                key = DIR + ";" + obj.saddr.ToString() + ";" + obj.daddr.ToString() + "-" + obj.dport + ";" + Pro;
            }
            else
            {
                DIR = "SERVER";
                key = DIR + ";" + obj.saddr.ToString() + "-" + obj.sport + ";" + obj.daddr.ToString() + ";" + Pro;
            }

            idx = List_ConvKey.IndexOf(key);
            if (idx == -1)
            {
                List_ConvKey.Add(key);
                List_Send.Add(0);
                List_Receive.Add(0);

            }
            else
            {
                List_Send[idx]++;
            }

        }
        public static String GetProcessName(int pid, string procname)
        {
            String Pro = "";
            Service Srv = GetServiceName(pid);
            if (Srv == null)
            {
                Pro = procname;
            }
            else
            {
                Pro = procname + " " + Srv.ServiceName;
            }
            return Pro;
        }
        private static void Kernel_TcpIpRecv(Microsoft.Diagnostics.Tracing.Parsers.Kernel.TcpIpTraceData obj)
        {
            if (!obj.saddr.Equals(mylocalip))
                return;
            String key = "";
            String DIR = "";
            String Pro = GetProcessName(obj.ProcessID, obj.ProcessName);
            int idx = 0;
            if (List_Server.Find(x => (x.Port == obj.sport)) == null)
            {
                DIR = "CLIENT";
                key = DIR + ";" + obj.saddr.ToString() + ";" + obj.daddr.ToString() + "-" + obj.dport + ";" + Pro;
            }
            else
            {
                DIR = "SERVER";
                key = DIR + ";" + obj.saddr.ToString() + "-" + obj.sport + ";" + obj.daddr.ToString() + ";" + Pro;
            }

            idx = List_ConvKey.IndexOf(key);
            if (idx == -1)
            {
                List_ConvKey.Add(key);
                List_Receive.Add(0);
                List_Send.Add(0);
            }
            else
            {
                List_Receive[idx]++;
            }


        }

        private static void Kernel_TcpIpConnect(Microsoft.Diagnostics.Tracing.Parsers.Kernel.TcpIpConnectTraceData obj)
        {

            int idx = 0;
            String key = "";

            if (obj.saddr.Equals(mylocalip))
            {
                String Pro = GetProcessName(obj.ProcessID, obj.ProcessName);
                key = "CLIENT;" + obj.saddr.ToString() + ";" + obj.daddr.ToString() + "-" + obj.dport + ";" + Pro;
                idx = List_ConvKey.IndexOf(key);
                if (idx == -1)
                {
                    List_ConvKey.Add(key);
                    List_Send.Add(0);
                    List_Receive.Add(0);

                }
                else
                {
                    List_Send[idx]++;
                }



            }




        }

        private static void Kernel_TcpIpAccept(Microsoft.Diagnostics.Tracing.Parsers.Kernel.TcpIpConnectTraceData obj)
        {
            String key = "";
            String DIR = "SERVER";
            int idx = 0;
            if (!obj.saddr.Equals(mylocalip))
                return;
            String Pro = GetProcessName(obj.ProcessID, obj.ProcessName);

            if (List_Server.Find(x => (x.Port == obj.sport)) == null)
            {
                Server s = new Server(obj.sport, "NA", 0, 0, IPAddress.Parse("0.0.0.0"));
                Console.WriteLine("Add Server port " + obj.sport);
                List_Server.Add(s);
            }
            key = DIR + ";" + obj.saddr.ToString() + "-" + obj.sport + ";" + obj.daddr.ToString() + ";" + Pro;

            idx = List_ConvKey.IndexOf(key);
            if (idx == -1)
            {
                Console.WriteLine("add key : " + key);
                List_ConvKey.Add(key);
                List_Receive.Add(0);
                List_Send.Add(0);
            }
            else
            {
                List_Receive[idx]++;
            }



        }
        
    }
}
