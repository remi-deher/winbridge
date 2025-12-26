using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text;
using System.Threading.Tasks;
using WinBridge.Core.Services;
using WinBridge.Models.Entities;

namespace WinBridge.App.Views;

public sealed partial class TerminalPage : Page
{
    private SshService _sshService = new SshService();
    private ServerModel? _currentServer;

    public TerminalPage()
    {
        this.InitializeComponent();
        InitializeTerminal();
    }

    private async void InitializeTerminal()
    {
        // Initialisation du moteur WebView2
        await TermWebView.EnsureCoreWebView2Async();

        // Chargement du HTML (Terminal Xterm.js)
        TermWebView.NavigateToString(GetTerminalHtml());

        // Abonnement à l'événement (nécessite le using Microsoft.Web.WebView2.Core)
        TermWebView.WebMessageReceived += TermWebView_WebMessageReceived;

        // Cache le cercle de chargement
        if (LoadingRing != null)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }

    // C'est ici que l'erreur CS0123 se produisait car le type args n'était pas reconnu
    private void TermWebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        // Récupère le message envoyé par le JS (touche clavier)
        string data = args.TryGetWebMessageAsString();

        // Envoie la donnée brute via SSH
        _sshService.SendData(data);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ServerModel server)
        {
            _currentServer = server;

            // Réception des données SSH -> Envoi vers le JS
            _sshService.DataReceived += (data) =>
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));

                    // Sécurité : on vérifie que le moteur Web est toujours là
                    if (TermWebView.CoreWebView2 != null)
                    {
                        await TermWebView.CoreWebView2.ExecuteScriptAsync($"writeBase64('{base64}')");
                    }
                });
            };

            try
            {
                // Connexion SSH en arrière-plan
                await Task.Run(() => _sshService.Connect(server));
            }
            catch (Exception ex)
            {
                await ShowErrorAndGoBack(ex.Message);
            }
        }
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        // Nettoyage propre
        _sshService.Dispose();

        // On désabonne l'événement pour éviter les fuites de mémoire
        TermWebView.WebMessageReceived -= TermWebView_WebMessageReceived;

        base.OnNavigatingFrom(e);
    }

    private async Task ShowErrorAndGoBack(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Erreur de connexion",
            Content = message,
            CloseButtonText = "Retour",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
        if (Frame.CanGoBack) Frame.GoBack();
    }

    // Code HTML/JS du terminal
    private string GetTerminalHtml()
    {
        return @"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8' />
            <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/xterm@5.3.0/css/xterm.css' />
            <script src='https://cdn.jsdelivr.net/npm/xterm@5.3.0/lib/xterm.js'></script>
            <script src='https://cdn.jsdelivr.net/npm/xterm-addon-fit@0.8.0/lib/xterm-addon-fit.js'></script>
            <style>
                body { margin: 0; padding: 0; background-color: #0c0c0c; overflow: hidden; height: 100vh; }
                #terminal { width: 100%; height: 100%; }
                /* Cache la barre de défilement native du navigateur pour laisser xterm gérer */
                ::-webkit-scrollbar { display: none; }
            </style>
        </head>
        <body>
            <div id='terminal'></div>
            <script>
                const term = new Terminal({
                    cursorBlink: true,
                    fontFamily: 'Consolas, monospace',
                    fontSize: 14,
                    theme: { 
                        background: '#0c0c0c', 
                        foreground: '#cccccc',
                        cursor: '#ffffff'
                    },
                    allowProposedApi: true
                });
                
                const fitAddon = new FitAddon.FitAddon();
                term.loadAddon(fitAddon);
                term.open(document.getElementById('terminal'));
                fitAddon.fit();

                window.onresize = () => fitAddon.fit();

                // JS vers C# (Clavier)
                term.onData(e => {
                    window.chrome.webview.postMessage(e);
                });

                // C# vers JS (Affichage)
                function writeBase64(b64) {
                    try {
                        const str = atob(b64);
                        const bytes = new Uint8Array(str.length);
                        for (let i = 0; i < str.length; i++) {
                            bytes[i] = str.charCodeAt(i);
                        }
                        term.write(bytes);
                    } catch (e) {
                        console.error('Erreur décodage base64', e);
                    }
                }
            </script>
        </body>
        </html>";
    }
}