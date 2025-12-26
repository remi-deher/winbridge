using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Core.Services;

namespace WinBridge.App.Components;

public sealed partial class TerminalControl : UserControl
{
    public event EventHandler<bool>? ToggleFullScreen;
    private SshService? _sshService;
    private bool _isFullScreen = false;

    public TerminalControl()
    {
        this.InitializeComponent();
        InitializeTerminal();
    }

    public void SetSshService(SshService service)
    {
        _sshService = service;
        _sshService.DataReceived += OnDataReceived;
        _sshService.StartTerminal(); // Démarre le shell interactif
    }

    private void OnDataReceived(string data)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
            if (TermWebView.CoreWebView2 != null)
            {
                await TermWebView.CoreWebView2.ExecuteScriptAsync($"writeBase64('{base64}')");
            }
        });
    }

    private async void InitializeTerminal()
    {
        await TermWebView.EnsureCoreWebView2Async();
        TermWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        TermWebView.NavigateToString(GetTerminalHtml());
        TermWebView.WebMessageReceived += TermWebView_WebMessageReceived;
    }

    private void TermWebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string message = args.TryGetWebMessageAsString();
        // Ici vous pourrez ajouter la gestion du copier/coller plus tard
        _sshService?.SendData(message);
    }

    private void BtnExpand_Click(object sender, RoutedEventArgs e)
    {
        _isFullScreen = !_isFullScreen;
        IconExpand.Symbol = _isFullScreen ? Symbol.BackToWindow : Symbol.FullScreen;
        ToggleFullScreen?.Invoke(this, _isFullScreen);
    }

    public void Dispose()
    {
        if (_sshService != null) _sshService.DataReceived -= OnDataReceived;
        try { TermWebView.Close(); } catch { }
    }

    // HTML avec le correctif ResizeObserver intégré
    private string GetTerminalHtml()
    {
        return @"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='utf-8' />
            <meta name='viewport' content='width=device-width, initial-scale=1.0' />
            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/xterm@5.3.0/css/xterm.css' />
            <script src='https://cdn.jsdelivr.net/npm/xterm@5.3.0/lib/xterm.js'></script>
            <script src='https://cdn.jsdelivr.net/npm/xterm-addon-fit@0.8.0/lib/xterm-addon-fit.js'></script>
            <style>
                html, body { height: 100vh; width: 100%; margin: 0; background-color: #0C0C0C; overflow: hidden; }
                #terminal { width: 100%; height: 100%; }
                ::-webkit-scrollbar { display: none; }
            </style>
        </head>
        <body>
            <div id='terminal'></div>
            <script>
                const term = new Terminal({ 
                    fontFamily: 'Cascadia Mono, Consolas, monospace', 
                    fontSize: 13, 
                    theme: { background: '#0C0C0C' } 
                });
                const fitAddon = new FitAddon.FitAddon();
                term.loadAddon(fitAddon);
                term.open(document.getElementById('terminal'));
                
                // --- CORRECTIF AFFICHAGE ---
                const resizeObserver = new ResizeObserver(() => { try { fitAddon.fit(); } catch {} });
                resizeObserver.observe(document.getElementById('terminal'));
                // ---------------------------

                term.onData(e => window.chrome.webview.postMessage(e));
                
                function writeBase64(b64) {
                    const str = atob(b64);
                    const bytes = new Uint8Array(str.length);
                    for (let i = 0; i < str.length; i++) bytes[i] = str.charCodeAt(i);
                    term.write(bytes);
                }
            </script>
        </body>
        </html>";
    }
}