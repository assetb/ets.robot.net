using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ETSRobot_v2.service
{
    public class AppJournal
    {
        private static List<LogInfo> logInfoList = new List<LogInfo>();
        private static int tCount = 0;


        public static void StartProc()
        {
            Task task = new Task(WaitFor);
            task.Start();
        }


        private static void WaitFor()
        {
            bool cycle = true;

            while (cycle) {
                Thread.Sleep(500);

                if (logInfoList.Count > 0) {
                    foreach (var logInfo in logInfoList) {
                        Write(1, logInfo.sender, logInfo.message, logInfo.withDate);
                    }

                    logInfoList.Clear();
                }
            }
        }


        public static void Write(int type, string sender, string message, bool withDateTime = true)
        {
            try {
                string fileName = "etsLogs_" + DateTime.Now.ToShortDateString().Replace(".", "_") + ".log";

                File.AppendAllLines(fileName, new string[1] { (withDateTime ? DateTime.Now.ToString() + "| " : "") + sender + ": " + message });
            } catch {
            }
        }


        public static void WriteServerLogs(string sender, string message)
        {
            try {
                string fileName = "etsServerLogs_" + DateTime.Now.ToShortDateString().Replace(".", "_") + ".log";

                File.AppendAllLines(fileName, new string[1] { sender + ": " + message });
            } catch {
            }
        }


        public static void WriteLotLog(string lotName, string sender, string message)
        {
            try {
                string fileName = "etsServerLot_" + lotName + " " + DateTime.Now.ToShortDateString().Replace(".", "_") + ".log";

                File.AppendAllLines(fileName, new string[1] { sender + ": " + message });
            } catch {
            }
        }


        public static void Write(string sender, string message, bool withDateTime = true)
        {
            try {
                logInfoList.Add(new LogInfo() {
                    sender = sender,
                    message = message,
                    withDate = withDateTime
                });
            } catch {
                /*logInfoList.Add(new LogInfo()
                {
                    sender = sender,
                    message = message,
                    withDate = withDateTime
                });*/
            }
        }


        public class LogInfo
        {
            public string sender { get; set; }
            public string message { get; set; }
            public bool withDate { get; set; }
        }
    }
}


