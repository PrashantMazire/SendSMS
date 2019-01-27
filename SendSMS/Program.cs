using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SendSMS
{

    class Program
    {
        public static readonly object syncLock = new object();
        static Mutex gM1;


        class SingleGlobalInstance : IDisposable
        {
            public bool _hasHandle = false;
            Mutex _mutex;

            private void InitMutex()
            {
                string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
                string mutexId = string.Format("Global\\{{{0}}}", appGuid);
                _mutex = new Mutex(false, mutexId);

                var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
                var securitySettings = new MutexSecurity();
                securitySettings.AddAccessRule(allowEveryoneRule);
                _mutex.SetAccessControl(securitySettings);
            }

            public SingleGlobalInstance(int timeOut)
            {
                InitMutex();
                try
                {
                    if (timeOut < 0)
                        _hasHandle = _mutex.WaitOne(Timeout.Infinite, false);
                    else
                        _hasHandle = _mutex.WaitOne(timeOut, false);

                    if (_hasHandle == false)
                        throw new TimeoutException("Timeout waiting for exclusive access on SingleInstance");
                }
                catch (AbandonedMutexException)
                {
                    _hasHandle = true;
                }
            }


            public void Dispose()
            {
                if (_mutex != null)
                {
                    if (_hasHandle)
                        _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                using (new SingleGlobalInstance(300000)) //5min timeout on global lock
                {
                    ThreadSafeMethod(args);
                }
            }
            catch(Exception ex)
            {
                Debug.Assert(false);
            }
        }

        public static LinkedList<string[]> msgList = new LinkedList<string[]>();
        public static void ThreadSafeMethod(string[] args)
        {
            try
            {
                //lock (syncLock)
                {
                    msgList.AddLast(args);
                    string message = string.Empty;

                    GSMCommunication gSMCommunication = new GSMCommunication();
                    if (args.Length < 2) return; //return if having less arguments
                    for (int i = 2; i < args.Length; i++)
                    {
                        message += args[i] + " ";
                    }
                    message.TrimEnd();
                    gSMCommunication.AppendLogFile("Thread == ", args[1] + " " + message);
                    SerialPort serialPort = gSMCommunication.OpenPort(args[0], 9600, 8, 300, 300);
                    if (serialPort != null)
                    {
                        gSMCommunication.sendMsg(serialPort, args[1], message);
                    }
                    gSMCommunication.ClosePort(serialPort);
                    return;
                    /* critical code */
                }
            }
            catch { }
        }

    }

}
