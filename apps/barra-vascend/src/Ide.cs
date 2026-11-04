using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BarraVascend;

// Gli agenti del Vascend IDE: l'IDE scrive il loro stato in un file e legge le azioni da una cartella accanto.
static class Ide
{
    public class Sessione
    {
        public string Id, Titolo, Dettaglio, Stato, Strumento, Conferma, Comando, IdConferma, Passo;
        public int Fatti, Totali;
        public long Aggiornata;
        public bool Attiva => Stato is "working" or "waiting";
    }

    static string Cartella => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vascend", "isola");
    static string File_ => Path.Combine(Cartella, "horizon-ide.json");

    public static List<Sessione> Sessioni = new();
    public static bool Aperto;
    public static event Action Cambiato;
    static FileSystemWatcher guardia;
    static System.Windows.Threading.DispatcherTimer rilettura, vita;
    static int pid;

    public static void Avvia()
    {
        try
        {
            Directory.CreateDirectory(Cartella);
            guardia = new FileSystemWatcher(Cartella, "horizon-ide.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            rilettura = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            rilettura.Tick += (_, _) => { rilettura.Stop(); Leggi(); };
            void Presto() => App.Io.Dispatcher.BeginInvoke(() => { rilettura.Stop(); rilettura.Start(); });
            guardia.Changed += (_, _) => Presto();
            guardia.Created += (_, _) => Presto();
            guardia.Deleted += (_, _) => Presto();
            guardia.Renamed += (_, _) => Presto();
            guardia.EnableRaisingEvents = true;
            // se l'IDE si chiude di colpo il file resta: il processo che l'ha scritto dice se vale ancora
            vita = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            vita.Tick += (_, _) => { if (Aperto && !Vivo(pid)) Leggi(); };
            vita.Start();
        }
        catch (Exception e) { App.Log("ide: " + e.Message); }
        Leggi();
    }

    static bool Vivo(int p)
    {
        try { using var x = System.Diagnostics.Process.GetProcessById(p); return !x.HasExited; }
        catch { return false; }
    }

    static void Leggi()
    {
        var nuove = new List<Sessione>();
        bool aperto = false;
        try
        {
            if (File.Exists(File_))
            {
                string testo;
                using (var f = new FileStream(File_, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(f)) testo = r.ReadToEnd();
                using var d = JsonDocument.Parse(testo);
                var radice = d.RootElement;
                pid = radice.TryGetProperty("pid", out var p) ? p.GetInt32() : 0;
                aperto = Vivo(pid);
                if (aperto && radice.TryGetProperty("sessions", out var ss))
                    foreach (var s in ss.EnumerateArray())
                    {
                        string S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                        var x = new Sessione { Id = S(s, "id"), Titolo = S(s, "label") ?? "", Dettaglio = S(s, "detail"), Stato = S(s, "state") ?? "done" };
                        if (s.TryGetProperty("updated", out var u) && u.TryGetInt64(out var ul)) x.Aggiornata = ul;
                        if (s.TryGetProperty("live", out var live) && live.ValueKind == JsonValueKind.Object)
                        {
                            x.Strumento = S(live, "tool");
                            if (live.TryGetProperty("todos", out var t) && t.ValueKind == JsonValueKind.Object)
                            {
                                x.Fatti = t.TryGetProperty("done", out var a) ? a.GetInt32() : 0;
                                x.Totali = t.TryGetProperty("total", out var b) ? b.GetInt32() : 0;
                                x.Passo = S(t, "current");
                            }
                            if (live.TryGetProperty("confirm", out var c) && c.ValueKind == JsonValueKind.Object)
                            {
                                x.IdConferma = S(c, "toolCallId");
                                x.Conferma = S(c, "title");
                                x.Comando = S(c, "command");
                            }
                        }
                        if (x.Id != null) nuove.Add(x);
                    }
            }
        }
        catch (IOException) { rilettura?.Start(); return; }
        catch (Exception e) { App.Log("ide: " + e.Message); }
        Aperto = aperto;
        Sessioni = nuove;
        Cambiato?.Invoke();
    }

    // Davanti la sessione che conta di più: quella che chiede qualcosa, poi quella al lavoro, poi la più recente.
    public static Sessione Prima => Sessioni.OrderByDescending(s => s.Stato == "waiting" || s.IdConferma != null ? 2 : s.Stato == "working" ? 1 : 0).ThenByDescending(s => s.Aggiornata).FirstOrDefault();

    public static int AlLavoro => Sessioni.Count(s => s.Attiva);

    static void Chiedi(object azione)
    {
        try
        {
            var dir = Path.Combine(Cartella, "horizon-ide-azioni");
            Directory.CreateDirectory(dir);
            var nome = Path.Combine(dir, $"{DateTime.Now:HHmmssfff}-{Guid.NewGuid():N}");
            File.WriteAllText(nome + ".tmp", JsonSerializer.Serialize(azione));
            File.Move(nome + ".tmp", nome + ".json");
        }
        catch (Exception e) { App.Log("ide azione: " + e.Message); }
    }

    public static void Apri(Sessione s) => Chiedi(new { kind = "openSession", sessionId = s.Id });
    public static void Conferma(Sessione s, bool si) => Chiedi(new { kind = "confirm", sessionId = s.Id, toolCallId = s.IdConferma, allow = si });
    public static void NuovaChat() => Chiedi(new { kind = "command", command = "newChat" });
    public static void Mostra() => Chiedi(new { kind = "focus" });
}
