using System;
using System.Windows;

namespace VascendEngine;

class App : Application
{
    [STAThread]
    static void Main(string[] args)
    {
        // VascendEngine --applica [vscode] [ide] [terminali] [barra]: applica lo stile salvato senza aprire la finestra
        if (args.Length > 0 && args[0] == "--applica")
        {
            if(!BarraVascend.ExtensionState.Enabled(BarraVascend.ExtensionState.BuiltinId)) return;
            var st = BarraVascend.Stile.Carica();
            var mete = args.Length > 1 ? args[1..] : new[] { "vscode", "terminali", "barra" };
            foreach (var m in mete)
                try
                {
                    var r = m switch { "vscode" => Generatori.ApplicaVsCode(st), "ide" => Generatori.ApplicaIde(st), "terminali" => Generatori.ApplicaTerminale(st), "barra" => Generatori.ApplicaBarra(), _ => "?" };
                    System.IO.File.AppendAllText(System.IO.Path.Combine(BarraVascend.Stile.Cartella, "applica.log"), $"{DateTime.Now:s} {m}: {r}\n");
                }
                catch (Exception e) { System.IO.File.AppendAllText(System.IO.Path.Combine(BarraVascend.Stile.Cartella, "applica.log"), $"{DateTime.Now:s} {m}: {e.Message}\n"); }
            return;
        }
        I18n.InitializeCulture();
        var a = new App { ShutdownMode = ShutdownMode.OnMainWindowClose };
        a.DispatcherUnhandledException += (_, e) =>
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(BarraVascend.Stile.Cartella, "errori.log"), $"{DateTime.Now:s} {e.Exception}\n"); } catch { }
            e.Handled = true;
        };
        System.IO.Directory.CreateDirectory(BarraVascend.Stile.Cartella);
        var updateErrors = new GitHubExtensions().ApplyPending();
        if (updateErrors.Count > 0)
            System.IO.File.AppendAllText(System.IO.Path.Combine(BarraVascend.Stile.Cartella,"errori.log"),
                $"{DateTime.Now:s} Aggiornamenti estensioni: {string.Join("; ", updateErrors)}\n");
        // --sezione <nome> apre direttamente una sezione
        int i = Array.IndexOf(args, "--sezione");
        a.Run(new Finestra(i >= 0 && i + 1 < args.Length ? args[i + 1] : null));
    }
}
