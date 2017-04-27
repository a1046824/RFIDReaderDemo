using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RFIDReader
{
    public class Device
    {
        public string ID { get; set; }
        public string cmd { get; set; }
        public Boolean IsConnected { get; set; }

        #region Declarations
        [DllImport("AtUsbHid.dll")]
        public static extern Boolean findHidDevice(uint VendorID, uint ProductID);

        [DllImport("AtUsbHid.dll")]
        public static extern void closeDevice();

        [DllImport("AtUsbHid.dll")]
        private static extern Boolean writeData(byte[] buf);

        [DllImport("AtUsbHid.dll")]
        private static extern Boolean readData(byte[] buffer);

        //[DllImport("AtUsbHid.dll")]
        //private static extern int hidRegisterDeviceNotification(IntPtr hWnd);

        //[DllImport("AtUsbHid.dll")]
        //private static extern void hidUnregisterDeviceNotification(IntPtr hWnd);

        //[DllImport("AtUsbHid.dll")]
        //private static extern int isMyDeviceNotification(uint dwData);

        //[DllImport("AtUsbHid.dll")]
        //private static extern Boolean setFeature(byte type, byte direction, uint length);
        
        private const int RECBUFMAX = 255;
        private byte[] receiveBuffer = new byte[RECBUFMAX];
        private int receiveIndex;
        //public event EventHandler DeviceDisconnected;

        #endregion

        public Device()
        {
            if (!ConnectDevice())
            {
                this.IsConnected = false;
                //if (DeviceDisconnected != null)
                //    DeviceDisconnected(this, null);
            }
            this.TriggerDevice();
            Read(this.cmd);
        }
        
        #region Functional
        /// <summary>
        /// Check connection to device
        /// </summary>
        /// <returns>True if device connected</returns>
        private bool ConnectDevice()
        {
            //  Don't know what the parameters mean
            this.IsConnected = findHidDevice(0x03EB, 0x2013);
            return this.IsConnected;
        }

        private void Read(string cmd)
        {
            //  Create the command to send to the HID
            string sendData = cmd;

            //Send Command
            int index;
            byte[] xmitbuffer = new byte[50];
            byte[] sbuffer = new byte[100];
            byte[] packet = new byte[60];
            byte[] buf = new byte[16];
            string strSend = "";
            receiveIndex = 0;

            //  If HID not connected
            if (ConnectDevice() == false)
            {
                this.IsConnected = false;
            }

            memcpy(ref sbuffer, sendData, sendData.Length);

            index = ConvertAsciiHexToBin(ref xmitbuffer, ref sbuffer, sendData.Length);

            int length = CreatePacket(ref packet, ref xmitbuffer, index);
            index = 0;
            int org_length = length;
            while (length != 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    int idx = i + index;
                    buf[i] = (idx < org_length) ? packet[idx] : (byte)0;
                    strSend += string.Format("{0:x} ", buf[i]);
                }
                writeData(buf);
                if (length > 16)
                {
                    index += 16;
                    length -= 16;
                }
                else
                {
                    length = 0;
                }
            }

            for(int x=0; x < 5; x++)
            {
                this.ReadTimer_Tick();
                if(String.IsNullOrWhiteSpace(this.ID))
                {
                    Thread.Sleep(100);
                } else
                {
                    break;
                }
            }
            
            this.ID = FormatOutput();

            // Set everything to null and close connection to device to avoid memory leaks
            sbuffer = null;
            sendData = null;
            xmitbuffer = null;
            packet = null;
            buf = null;
            closeDevice();
        }

        private void ReadTimer_Tick()
        {
            byte[] sbuffer = new byte[255];
            int sindex = 0;
            int count = 16;
            
            if (readData(sbuffer))
            {
                while ((count != 0) && (receiveIndex < (RECBUFMAX - 5)))
                {
                    receiveBuffer[receiveIndex++] = sbuffer[sindex++];
                    count--;
                }
                if ((receiveIndex > 3) &&
                    (receiveBuffer[0] == 0x01) &&
                    (receiveBuffer[2] == 0) &&
                    (receiveBuffer[1] <= receiveIndex))
                {
                    //TestBed.ID = FormatOutput();
                    // if (IDFound != null)
                    //   IDFound(this, null);
                    //_ReadTimer.Stop();
                }
            }
        }

        private string FormatOutput()
        {
            string output = "";
            byte lucCmd, lucErrorCode;
            byte index;
            byte lucPktLen;

            index = 0;

            lucErrorCode = receiveBuffer[4];
            lucCmd = receiveBuffer[5];
            lucPktLen = receiveBuffer[1];

            if (lucErrorCode == 0)
            {
                index = 7;
                string _address = string.Empty;

                for (int i = (index + 8) - 1; i != index; i--)
                {
                    _address += FormatOutput(receiveBuffer[i]);
                }
                output += string.Format("\r\n\tUID:\t\t{0}", _address);
                index += 8;
            }

            // The reading is back to front. e.g. 	"E0 02 21 2C 33 18 43" instead of "43:18:33:2C:21:02:E0"
            receiveBuffer = null;
            return output;
        }

        private void TriggerDevice()
        {
            //  Create the command to send to the HID
            string sendData = string.Empty;
            string _flag = "83";
            string _cmd = string.Empty;
            
            // Removed the other options - set read tag info as default
            _cmd = "2B";
            sendData = _flag + _cmd;
            this.cmd = sendData;
        }

        #endregion

        #region Conversions
        private string FormatOutput(byte _in)
        {
            string _out = string.Empty;

            if (_in < 0x10)
                _out += string.Format("0{0:X} ", _in);
            else
                _out += string.Format("{0:X} ", _in);

            return _out;
        }

        private void memcpy(ref byte[] sbuffer, string strAscii, int length)
        {
            for (int i = 0; i < length; i++)
            {
                char ch = strAscii[i];
                sbuffer[i] = (byte)ch;
            }
        }

        private int ConvertAsciiHexToBin(ref byte[] ptrOut, ref byte[] ptrIn, int length)
        {
            int rtnLength = 0;
            byte upper_nibble, lower_nibble, total_nibble;
            int inputIndex = 0;

            while (length != 0)
            {
                length--;
                upper_nibble = ConvertAsciiToHex(ptrIn[inputIndex++]);
                if (length != 0)
                    length--;
                lower_nibble = ConvertAsciiToHex(ptrIn[inputIndex++]);
                total_nibble = upper_nibble;
                total_nibble <<= 4;
                total_nibble &= 0xF0;
                total_nibble |= lower_nibble;
                ptrOut[rtnLength++] = total_nibble;
            }
            return rtnLength;
        }

        private byte ConvertAsciiToHex(byte lucInput)
        {
            if ('0' <= lucInput && lucInput <= '9')
                return (byte)(lucInput - '0');

            if ('a' <= lucInput && lucInput <= 'f')
                return (byte)((lucInput - 'a') + 10);

            if ('A' <= lucInput && lucInput <= 'F')
                return (byte)((lucInput - 'A') + 10);

            return (byte)0;
        }

        private int CreatePacket(ref byte[] pktBuf, ref byte[] tempBuf, int pktLen)
        {
            System.UInt16 current_crc_value;
            byte final_crc_msb, final_crc_lsb;

            pktBuf[0] = 0x01;//SOF
            pktBuf[2] = 0;//len, msb
            pktBuf[3] = 0x10;//device type
            //copy the flag, command and data to pktBuf

            for (int i = 0; i < pktLen; i++)
            {
                pktBuf[i + 4] = tempBuf[i];
            }

            pktLen += 4;
            pktBuf[1] = (byte)(pktLen + 2);
            current_crc_value = lrc(ref pktBuf, pktLen);
            final_crc_msb = (byte)((0xFF00 & current_crc_value) >> 8);
            final_crc_lsb = (byte)(0x00FF & current_crc_value);
            pktBuf[pktLen++] = final_crc_lsb;
            pktBuf[pktLen++] = final_crc_msb;

            return pktLen;
        }

        System.UInt16 lrc(ref byte[] message, int length)
        {
            int i;
            System.UInt16 temp1 = 0, temp2 = 0;

            for (i = 0; i < length; i++)
            {
                temp2 ^= message[i];
            }
            temp1 = (System.UInt16)(temp2 ^ 0xff);

            return ((System.UInt16)((temp1 << 8) | temp2));
        }
        #endregion


    }
    class Win32
    {
        public const int
        WM_DEVICECHANGE = 0x0219;
        public const int
        DBT_DEVICEARRIVAL = 0x8000,
        DBT_DEVICEREMOVECOMPLETE = 0x8004;
        public const int
        DEVICE_NOTIFY_WINDOW_HANDLE = 0,
        DEVICE_NOTIFY_SERVICE_HANDLE = 1;
        public const int
        DBT_DEVTYP_DEVICEINTERFACE = 5;
        public static Guid
        GUID_DEVINTERFACE_HID = new
        Guid("4D1E55B2-F16F-11CF-88CB-001111000030");

        [StructLayout(LayoutKind.Sequential)]
        public class DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            public Guid dbcc_classguid;
            public short dbcc_name;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterDeviceNotification(
        IntPtr hRecipient,
        IntPtr NotificationFilter,
        Int32 Flags);

        [DllImport("kernel32.dll")]
        public static extern int GetLastError();
    }
}
