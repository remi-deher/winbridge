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
        // 1. Initialisation du moteur
        await TermWebView.EnsureCoreWebView2Async();

        // 2. Désactiver le menu contextuel par défaut du navigateur (clic droit)
        // pour permettre notre propre gestion (Coller au clic droit comme Putty/Windows Terminal)
        TermWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

        // 3. Charger le Terminal
        TermWebView.NavigateToString(GetTerminalHtml());
        TermWebView.WebMessageReceived += TermWebView_WebMessageReceived;

        // Cache le chargement
        if (LoadingRing != null)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }

    private void TermWebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string message = args.TryGetWebMessageAsString();

        if (message == "PASTE_REQ")
        {
            // Gérer le "Coller" depuis le presse-papier Windows vers le SSH
            PasteFromClipboard();
        }
        else if (message.StartsWith("COPY:"))
        {
            // Gérer le "Copier" (Sélection de texte -> Presse-papier Windows)
            var textToCopy = message.Substring(5);
            CopyToClipboard(textToCopy);
        }
        else
        {
            // Sinon, c'est une touche clavier standard
            _sshService.SendData(message);
        }
    }

    private void CopyToClipboard(string text)
    {
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
    }

    private async void PasteFromClipboard()
    {
        var dataPackage = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (dataPackage.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            string text = await dataPackage.GetTextAsync();
            _sshService.SendData(text);
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ServerModel server)
        {
            _currentServer = server;

            _sshService.DataReceived += (data) =>
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    // Encodage Base64 pour éviter les bugs de caractères spéciaux
                    var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
                    if (TermWebView.CoreWebView2 != null)
                    {
                        await TermWebView.CoreWebView2.ExecuteScriptAsync($"writeBase64('{base64}')");
                    }
                });
            };

            try
            {
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
        _sshService.Dispose();
        TermWebView.WebMessageReceived -= TermWebView_WebMessageReceived;
        base.OnNavigatingFrom(e);
    }

    private async Task ShowErrorAndGoBack(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Erreur",
            Content = message,
            CloseButtonText = "Retour",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
        if (Frame.CanGoBack) Frame.GoBack();
    }

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
        <script src='https://cdn.jsdelivr.net/npm/xterm-addon-web-links@0.9.0/lib/xterm-addon-web-links.js'></script>
        <style>
            /* Fond transparent pour laisser WinUI gérer la couleur de fond (Mica/Acrylic possible) */
            body { margin: 0; padding: 0; background-color: transparent; overflow: hidden; height: 100vh; user-select: none; }
            /* Padding pour ne pas coller au bord de la fenêtre */
            #terminal { width: 100%; height: 100%; padding: 8px; box-sizing: border-box; }
            ::-webkit-scrollbar { display: none; }
        </style>
    </head>
    <body>
        <div id='terminal'></div>
        <script>
            // Palette EXACTE 'Campbell' (Défaut Windows Terminal)
            const campbellTheme = {
                background: '#0C0C0C', // Noir profond Windows
                foreground: '#CCCCCC', // Gris clair
                cursor: '#FFFFFF',
                selectionBackground: '#FFFFFF40', // Sélection blanche semi-transparente
                
                black: '#0C0C0C',
                red: '#C50F1F',
                green: '#13A10E',
                yellow: '#C19C00',
                blue: '#0037DA',
                magenta: '#881798', // C'est ce rose qui manquait sur 'ARTEMIS'
                cyan: '#3A96DD',
                white: '#CCCCCC',
                
                brightBlack: '#767676',
                brightRed: '#E74856',
                brightGreen: '#16C60C',
                brightYellow: '#F9F1A5',
                brightBlue: '#3B78FF',
                brightMagenta: '#B4009E',
                brightCyan: '#61D6D6',
                brightWhite: '#F2F2F2'
            };

            const term = new Terminal({
                cursorBlink: true,
                cursorStyle: 'bar',
                // Stack de polices : Cascadia d'abord (la police native Windows 11), puis Consolas
                fontFamily: 'Cascadia Mono, Consolas, monospace', 
                fontSize: 14,
                fontWeight: 'normal',
                fontWeightBold: 'bold',
                lineHeight: 1.1, // Espacement natif
                theme: campbellTheme,
                allowProposedApi: true
            });
            
            const fitAddon = new FitAddon.FitAddon();
            term.loadAddon(fitAddon);
            term.loadAddon(new WebLinksAddon.WebLinksAddon());

            term.open(document.getElementById('terminal'));
            
            // Astuce : On rend le terminal transparent pour le look moderne
            term.options.theme.background = '#0C0C0C'; 

            fitAddon.fit();
            window.onresize = () => fitAddon.fit();

            term.onData(e => window.chrome.webview.postMessage(e));

            document.addEventListener('contextmenu', event => {
                event.preventDefault();
                window.chrome.webview.postMessage('PASTE_REQ');
            });

            term.onSelectionChange(() => {
                if (term.hasSelection()) {
                    window.chrome.webview.postMessage('COPY:' + term.getSelection());
                }
            });

            function writeBase64(b64) {
                try {
                    const str = atob(b64);
                    const bytes = new Uint8Array(str.length);
                    for (let i = 0; i < str.length; i++) {
                        bytes[i] = str.charCodeAt(i);
                    }
                    term.write(bytes);
                } catch (e) { console.error(e); }
            }
        </script>
    </body>
    </html>";
    }
}