using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ETSRobot_v2.service
{
    public static class TimeSync
    {
        [DllImport("coredll.dll")]
        private extern static void GetSystemTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("coredll.dll")]
        private extern static uint SetSystemTime(ref SYSTEMTIME lpSystemTime);


        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }


        private static void GetTime()
        {
            // Call the native GetSystemTime method
            // with the defined structure.
            SYSTEMTIME stime = new SYSTEMTIME();
            GetSystemTime(ref stime);

            // Show the current time.           
            //MessageBox.Show("Current Time: " +
            //    stime.wHour.ToString() + ":"
            //    + stime.wMinute.ToString());
        }


        public static void SetTime(int timeDeltaInSec)
        {
            // Call the native GetSystemTime method
            // with the defined structure.
            SYSTEMTIME systime = new SYSTEMTIME();
            GetSystemTime(ref systime);

            // Set the system clock ahead one hour.
            systime.wHour = (ushort)(systime.wSecond + timeDeltaInSec % 60);
            SetSystemTime(ref systime);
            //MessageBox.Show("New time: " + systime.wHour.ToString() + ":"
            //    + systime.wMinute.ToString());
        }
    }
}
