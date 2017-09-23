/*
 * Copyright (C) 2017 FIX94
 *
 * This software may be modified and distributed under the terms
 * of the MIT license.  See the LICENSE file for details.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TobiasErichsen.teVirtualMIDI;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MIDILoopback
{
    public partial class Form1 : Form
    {
        static bool is64BitProcess = (IntPtr.Size == 8);
        private Icon small, large;
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private String pString = "MIDI Loopback";
        private string drvPathNative = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Drivers32";
        private string drvPathWOW64 = "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows NT\\CurrentVersion\\Drivers32";
        private string midi = "midi";
        private string normalDrv = "wdmaud.drv";
        private string ourDrv = "teVirtualMIDI.dll";
        static private TeVirtualMIDI mahPort;
        private Thread pthrough;
        private bool registered = false;
        public Form1()
        {
            InitializeComponent();
            small = GetIcon(ShellIconSize.SmallIcon);
            large = GetIcon(ShellIconSize.LargeIcon);
            //set normal icons
            SendMessage(this.Handle, WM_SETICON, SHGFI_LARGEICON, small.Handle);
            SendMessage(this.Handle, WM_SETICON, SHGFI_SMALLICON, large.Handle);

            //fully hide window but at least load it
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Exit", trayIcon_Close);

            trayIcon = new NotifyIcon();
            trayIcon.Text = this.Text;
            if (SystemInformation.SmallIconSize.Width == 16 && SystemInformation.SmallIconSize.Height == 16) //get 16x16
                trayIcon.Icon = new Icon(small, SystemInformation.SmallIconSize);
            else //just calculate from base 32x32 icon to (hopefully) look better
                trayIcon.Icon = new Icon(large, SystemInformation.SmallIconSize);

            // Add menu to tray icon and show it.
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            trayIcon.MouseClick += trayIcon_MouseClick;

            mahPort = new TeVirtualMIDI(pString);
            pthrough = new Thread(new ThreadStart(readInput));

            this.Load += onLoad;
            this.FormClosed += onClosed;
            this.Resize += onResize;
        }

        public void onLoad(object sender, System.EventArgs e)
        {
            //window loaded, set window back to a normal state
            this.WindowState = FormWindowState.Normal;
            this.Visible = false;
            this.ShowInTaskbar = true;

            pthrough.Start();
            DeregisterOurDevice();
            RegisterOurDevice();
        }
        void trayIcon_Close(object sender, System.EventArgs e)
        {
            //just use form close event for everything
            this.Close();
        }

        void trayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            this.Visible = !this.Visible;
            if (this.Visible)
            {
                //set normal icons
                SendMessage(this.Handle, WM_SETICON, SHGFI_LARGEICON, small.Handle);
                SendMessage(this.Handle, WM_SETICON, SHGFI_SMALLICON, large.Handle);
                //put in front
                this.Activate();
            }
        }
        private void onResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
                this.Visible = false;
            }
        }
        void onClosed(object sender, FormClosedEventArgs e)
        {
            mahPort.shutdown();
            pthrough.Join();
            DeregisterOurDevice();
            trayIcon.Icon = null;
            trayIcon.Dispose();
        }
        void RegisterOurDevice()
        {
            var midiVal = Registry.GetValue(drvPathNative, midi, null);
            var midiVal2 = is64BitProcess ? Registry.GetValue(drvPathWOW64, midi, null) : null;
            if (midiVal != null && normalDrv.Equals(midiVal) && 
                (is64BitProcess == false || (midiVal2 != null && normalDrv.Equals(midiVal2))))
            {
                Registry.SetValue(drvPathNative, midi, ourDrv);
                if (is64BitProcess)
                    Registry.SetValue(drvPathWOW64, midi, ourDrv);
                registered = true;
            }
            setRegisteredStatus();
        }
        void DeregisterOurDevice()
        {
            var midiVal = Registry.GetValue(drvPathNative, midi, null);
            var midiVal2 = is64BitProcess ? Registry.GetValue(drvPathWOW64, midi, null) : null;
            if (midiVal != null && ourDrv.Equals(midiVal) &&
                (is64BitProcess == false || (midiVal2 != null && ourDrv.Equals(midiVal2))))
            {
                Registry.SetValue(drvPathNative, midi, normalDrv);
                if (is64BitProcess)
                    Registry.SetValue(drvPathWOW64, midi, normalDrv);
            }
        }
        private void setRegisteredStatus()
        {
            registered = false;
            var midiVal = Registry.GetValue(drvPathNative, midi, null);
            if (midiVal != null && ourDrv.Equals(midiVal))
                registered = true;
            if (registered == true)
            {
                label6.Text = "Registered";
                label6.ForeColor = Color.FromArgb(0, 128, 0);
            }
            else
            {
                label6.Text = "Not Registered";
                label6.ForeColor = Color.FromArgb(128, 0, 0);
            }
        }

        private void readInput()
        {
            try
            {
                while (true)
                {
                    byte[] command = mahPort.getCommand();
                    mahPort.sendCommand(command); //loopback
                }
            }
            catch (TeVirtualMIDIException)
            {
            }
        }
        private static string byteArrayToString(byte[] ba)
        {
            string hex = BitConverter.ToString(ba);
            return hex.Replace("-", ":");
        }

        //inspired by http://www.brad-smith.info/blog/archives/164
        const int SHGFI_ICON = 0x100;
        const int SHGFI_LARGEICON = 0x0;    // 32x32 pixels
        const int SHGFI_SMALLICON = 0x1;    // 16x16 pixels

        public enum ShellIconSize : int
        {
            SmallIcon = SHGFI_ICON | SHGFI_SMALLICON,
            LargeIcon = SHGFI_ICON | SHGFI_LARGEICON
        }

        public struct SHFILEINFO
        {
            // Handle to the icon representing the file
            public IntPtr hIcon;
            // Index of the icon within the image list
            public int iIcon;
            // Various attributes of the file
            public uint dwAttributes;
            // Path to the file
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDisplayName;
            // File type
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbSizeFileInfo,
            uint uFlags
        );

        public static Icon GetIcon(ShellIconSize size)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            SHGetFileInfo(Application.ExecutablePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), (uint)size);
            return Icon.FromHandle(shinfo.hIcon);
        }

        //http://stackoverflow.com/questions/4048910/setting-a-different-taskbar-icon-to-the-icon-displayed-in-the-titlebar-c
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

        private const uint WM_SETICON = 0x80u;
    }
}
