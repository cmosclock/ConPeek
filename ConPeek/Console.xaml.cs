using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CoreRCON;
using Vanara.Extensions;
using Vanara.PInvoke;
using Timer = System.Timers.Timer;

namespace ConPeek;

public partial class Console : Window
{
    private readonly Timer _timer;
    private HWND _hwndEngine;
    private readonly string _loopbackInterface;

    public Console()
    {
        InitializeComponent();
        Title = "-usercon";
        // var exePath = Assembly.GetEntryAssembly().Location;
        // var logFolder = Path.Combine(Path.GetDirectoryName(exePath), nameof(ConPeek));
        // Directory.CreateDirectory(logFolder);
        // var logPath = Path.Combine(logFolder, "console.log");
        // File.WriteAllText(logPath, String.Empty);
        DataContext = new ConsoleViewModel();

        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        var networkInterface = networkInterfaces.First();
        _loopbackInterface = networkInterface.GetIPProperties().UnicastAddresses
            .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(a => a.Address.MapToIPv4().ToString())
            .First();
        var logReceiver = new LogReceiver(50000, new IPEndPoint(IPAddress.Parse(_loopbackInterface), 27015));
        logReceiver.ListenRaw(line =>
        {
            TextBlock1.Dispatcher.Invoke(() =>
            {
                TextBlock1.Text += $"{line}\n";
                ScrollViewer1.ScrollToBottom();
            });
        });

        _timer = new Timer(TimeSpan.FromSeconds(1).TotalMilliseconds);
        _timer.Elapsed += (sender, args) =>
        {
            var hwndEngine = User32.FindWindow("Valve001", null);
            if (_hwndEngine == HWND.NULL && hwndEngine != HWND.NULL)
            {
                // ;con_logfile {logPath}
                SendCommand(hwndEngine, $"ip 0.0.0.0;rcon_password {nameof(ConPeek)};logaddress_add {_loopbackInterface}:50000;log on;net_start;");
            }
            _hwndEngine = hwndEngine;
        };
        _timer.Start();
    }

    public async Task<string> SendCommand(string cmd)
    {
        using var rcon = new RCON(IPAddress.Parse("127.0.0.1"), 27015, nameof(ConPeek));
        await rcon.ConnectAsync();
        var res = await rcon.SendCommandAsync(cmd);
        return res;
    }
    void SendCommand(HWND hwndEngine, string text)
    {
        var ansiStr = Encoding.ASCII.GetString(Encoding.Convert(Encoding.UTF8, Encoding.ASCII, Encoding.UTF8.GetBytes(text)));
        var cmd = IntPtr.Zero;
        try
        {
            cmd = Marshal.StringToHGlobalAnsi(ansiStr);
            COPYDATASTRUCT copyData;
            copyData.cbData = text.Length + 1;
            copyData.dwData = IntPtr.Zero;
            copyData.lpData = cmd;
            User32.SendMessage(hwndEngine, User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, ref copyData);
        }
        finally
        {
            if (cmd != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(cmd);
            }
        }
    }
    
    private void TextBox1_OnKeyboardKeyDown(object sender, KeyEventArgs e)
    {
        var enteredText = $"{TextBox1.Text}".Trim();
        if (e.Key != Key.Enter || string.IsNullOrEmpty(enteredText)) return;
        var hwndEngine = User32.FindWindow("Valve001", null);
        if (hwndEngine == HWND.NULL) return;
        TextBlock1.Text += $"> {enteredText}\n";
        ScrollViewer1.ScrollToBottom();
        TextBox1.Text = "";
        Task.Run(async () =>
        {
            try
            {
                var res = await SendCommand(enteredText);
                TextBlock1.Dispatcher.Invoke(() =>
                {
                    TextBlock1.Text += $"< {res}\n";
                    ScrollViewer1.ScrollToBottom();
                });
            }
            catch (Exception ex)
            {
                TextBlock1.Dispatcher.Invoke(() =>
                {
                    TextBlock1.Text += $"< {ex}\n";
                    ScrollViewer1.ScrollToBottom();
                });
            }
            
        });
    }

    public struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }
}