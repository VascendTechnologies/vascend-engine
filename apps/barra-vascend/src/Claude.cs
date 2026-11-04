using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BarraVascend;

// Consumi di Claude Code: limiti 5 ore / 7 giorni dall'API OAuth e token dai transcript locali.
// Il token OAuth si legge dal file delle credenziali di Claude Code e non finisce mai nei log.
static class Claude
{
    public static string Cartella => Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is string d && d.Length > 0 ? d : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude");
    static string Dati => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BarraVascend");

    public static double? Cinque, Sette;
    public static DateTime? CinqueReset, SetteReset;
    public static bool Scaduto;
    public static event Action Cambiato;
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static bool inUso, inScansione;
    static DateTime ultimoUso = DateTime.MinValue, ultimaScansione = DateTime.MinValue;

    // giorno → modello → [input, output, cache creata, cache letta]
    public static Dictionary<string, Dictionary<string, long[]>> Giorni = new();

    public static void Avvia()
    {
        Directory.CreateDirectory(Dati);
        LeggiCacheUso();
        Aggiorna();
    }

    public static void Aggiorna(bool subito = false)
    {
        if (subito || (DateTime.Now - ultimoUso).TotalSeconds > 300) Uso();
        if (subito || (DateTime.Now - ultimaScansione).TotalSeconds > 300) Scansiona();
    }

    static void Avvisa() => App.Io.Dispatcher.BeginInvoke(() => Cambiato?.Invoke());

    static void LeggiCacheUso()
    {
        try
        {
            var j = JsonNode.Parse(File.ReadAllText(Path.Combine(Dati, "claude_uso.json")));
            Imposta(j);
        }
        catch { }
    }

    static void Imposta(JsonNode j)
    {
        Cinque = (double?)j?["five_hour"]?["utilization"];
        Sette = (double?)j?["seven_day"]?["utilization"];
        CinqueReset = Data(j?["five_hour"]?["resets_at"]);
        SetteReset = Data(j?["seven_day"]?["resets_at"]);
    }

    static DateTime? Data(JsonNode n) => n != null && DateTime.TryParse((string)n, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToLocalTime() : null;

    static void Uso()
    {
        if (inUso) return;
        inUso = true;
        ultimoUso = DateTime.Now;
        Task.Run(async () =>
        {
            try
            {
                var cred = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Cartella, ".credentials.json")))?["claudeAiOauth"];
                long scade = (long?)cred?["expiresAt"] ?? 0;
                Scaduto = scade > 0 && DateTimeOffset.FromUnixTimeMilliseconds(scade) < DateTimeOffset.Now;
                var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + (string)cred?["accessToken"]);
                req.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
                var r = await http.SendAsync(req);
                if (r.IsSuccessStatusCode)
                {
                    var testo = await r.Content.ReadAsStringAsync();
                    Imposta(JsonNode.Parse(testo));
                    Scaduto = false;
                    await File.WriteAllTextAsync(Path.Combine(Dati, "claude_uso.json"), testo);
                }
                else App.Log("claude uso: HTTP " + (int)r.StatusCode);
            }
            catch (Exception e) { App.Log("claude uso: " + e.Message); }
            finally { inUso = false; Avvisa(); }
        });
    }

    class FileInfoCache
    {
        public long M { get; set; }
        public long S { get; set; }
        public Dictionary<string, Dictionary<string, long[]>> D { get; set; } = new();
    }

    // Scansione incrementale: si rilegge solo il transcript cambiato (data e dimensione).
    static void Scansiona()
    {
        if (inScansione) return;
        inScansione = true;
        ultimaScansione = DateTime.Now;
        Task.Run(() =>
        {
            string percorsoCache = Path.Combine(Dati, "claude_storico.json");
            try
            {
                Dictionary<string, FileInfoCache> vecchi;
                try { vecchi = JsonSerializer.Deserialize<Dictionary<string, FileInfoCache>>(File.ReadAllText(percorsoCache)) ?? new(); }
                catch { vecchi = new(); }
                var nuovi = new Dictionary<string, FileInfoCache>();
                var limite = DateTime.Now.AddDays(-35);
                var cartella = Path.Combine(Cartella, "projects");
                int letti = 0;
                foreach (var f in Directory.EnumerateFiles(cartella, "*.jsonl", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTime < limite) continue;
                        long m = fi.LastWriteTimeUtc.Ticks, s = fi.Length;
                        if (vecchi.TryGetValue(f, out var c) && c.M == m && c.S == s) { nuovi[f] = c; continue; }
                        nuovi[f] = new FileInfoCache { M = m, S = s, D = Leggi(f) };
                        if (++letti % 25 == 0) { Unisci(nuovi); Avvisa(); }
                    }
                    catch { }
                }
                Unisci(nuovi);
                File.WriteAllText(percorsoCache, JsonSerializer.Serialize(nuovi));
            }
            catch (Exception e) { App.Log("claude storico: " + e.Message); }
            finally { inScansione = false; Avvisa(); }
        });
    }

    static void Unisci(Dictionary<string, FileInfoCache> files)
    {
        var g = new Dictionary<string, Dictionary<string, long[]>>();
        foreach (var f in files.Values)
            foreach (var (giorno, modelli) in f.D)
            {
                if (!g.TryGetValue(giorno, out var dst)) g[giorno] = dst = new();
                foreach (var (mod, c) in modelli)
                {
                    if (!dst.TryGetValue(mod, out var t)) dst[mod] = t = new long[4];
                    for (int i = 0; i < 4; i++) t[i] += c[i];
                }
            }
        Giorni = g;
    }

    // Dalle righe del transcript si leggono solo numeri, data e modello, mai i contenuti.
    static Dictionary<string, Dictionary<string, long[]>> Leggi(string f)
    {
        var d = new Dictionary<string, Dictionary<string, long[]>>();
        using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var r = new StreamReader(fs);
        string riga;
        while ((riga = r.ReadLine()) != null)
        {
            if (!riga.Contains("\"usage\"")) continue;
            try
            {
                using var doc = JsonDocument.Parse(riga);
                var o = doc.RootElement;
                if (!o.TryGetProperty("message", out var msg) || msg.ValueKind != JsonValueKind.Object) continue;
                if (!msg.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object) continue;
                long N(string k) => u.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
                var c = new[] { N("input_tokens"), N("output_tokens"), N("cache_creation_input_tokens"), N("cache_read_input_tokens") };
                if (c.All(x => x == 0)) continue;
                if (!o.TryGetProperty("timestamp", out var ts) || !DateTime.TryParse(ts.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) continue;
                string giorno = t.ToLocalTime().ToString("yyyy-MM-dd");
                string mod = msg.TryGetProperty("model", out var mv) && mv.ValueKind == JsonValueKind.String ? mv.GetString() : "unknown";
                if (u.TryGetProperty("speed", out var sp) && sp.ValueKind == JsonValueKind.String && sp.GetString() == "fast") mod += "[fast]";
                if (!d.TryGetValue(giorno, out var gm)) d[giorno] = gm = new();
                if (!gm.TryGetValue(mod, out var tot)) gm[mod] = tot = new long[4];
                for (int i = 0; i < 4; i++) tot[i] += c[i];
            }
            catch { }
        }
        return d;
    }

    // Prezzi API in $ per milione di token: input, output, scrittura cache, lettura cache.
    static readonly (string pre, double[] p)[] prezzi = new (string, double[])[]
    {
        ("claude-opus-5-5", new[] { 4.0, 20, 5, .20 }),
        ("claude-opus-5", new[] { 5.0, 25, 6.25, .50 }),
        ("claude-opus-4", new[] { 5.0, 25, 6.25, .50 }),
        ("claude-sonnet-5-5", new[] { 2.0, 10, 2.5, .20 }),
        ("claude-sonnet-5", new[] { 2.0, 10, 2.5, .20 }),
        ("claude-sonnet-4", new[] { 3.0, 15, 3.75, .30 }),
        ("claude-fable-5-1", new[] { 10.0, 50, 12.5, .25 }),
        ("claude-fable-5", new[] { 10.0, 50, 12.5, 1.0 }),
        ("claude-mythos-5", new[] { 10.0, 50, 12.5, .25 }),
        ("claude-haiku-4-5", new[] { 1.0, 5, 1.25, .10 }),
    }.OrderByDescending(x => x.Item1.Length).ToArray();

    public static double Costo(string mod, long[] c)
    {
        var b = mod.Split('[')[0].ToLowerInvariant();
        var p = prezzi.FirstOrDefault(x => b.StartsWith(x.pre)).p;
        if (p == null) return 0;
        double k = mod.Contains("[fast]") ? 2 : 1;
        return k * (c[0] * p[0] + c[1] * p[1] + c[2] * p[2] + c[3] * p[3]) / 1e6;
    }

    public static string NomeModello(string mod)
    {
        var b = mod.Split('[')[0].ToLowerInvariant().Replace("claude-", "");
        var parti = b.Split('-').TakeWhile(x => !(x.Length >= 8 && x.All(char.IsDigit))).ToList();
        if (parti.Count == 0) return mod;
        string nome = char.ToUpper(parti[0][0]) + parti[0][1..];
        string ver = string.Join(".", parti.Skip(1));
        return (nome + " " + ver).Trim() + (mod.Contains("[fast]") ? " fast" : "");
    }

    static bool Nascosto(string mod) => mod.Split('[')[0].ToLowerInvariant() == "claude-opus-5" || mod.StartsWith("<synthetic");

    public class Riepilogo
    {
        public List<string> Etichette = new();
        public List<DateTime> Date = new();
        public List<double> Token = new(), Costi = new();
        public long Totale;
        public double Spesa;
        public List<(string nome, long token, double costo)> Modelli = new();
    }

    public static Riepilogo Periodo(int giorni)
    {
        var it = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        var r = new Riepilogo();
        var perModello = new Dictionary<string, (long, double)>();
        string[] iniziali = { "D", "L", "M", "M", "G", "V", "S" };
        for (int i = giorni - 1; i >= 0; i--)
        {
            var d = DateTime.Today.AddDays(-i);
            r.Etichette.Add(giorni <= 7 ? iniziali[(int)d.DayOfWeek] : d.Day.ToString());
            r.Date.Add(d);
            long tok = 0;
            double costo = 0;
            if (Giorni.TryGetValue(d.ToString("yyyy-MM-dd"), out var mods))
                foreach (var (m, c) in mods)
                {
                    long t = c.Sum();
                    double k = Costo(m, c);
                    tok += t;
                    costo += k;
                    if (Nascosto(m)) continue;
                    var nome = NomeModello(m);
                    perModello.TryGetValue(nome, out var a);
                    perModello[nome] = (a.Item1 + t, a.Item2 + k);
                }
            r.Token.Add(tok);
            r.Costi.Add(costo);
            r.Totale += tok;
            r.Spesa += costo;
        }
        r.Modelli = perModello.OrderByDescending(x => x.Value.Item2).Select(x => (x.Key, x.Value.Item1, x.Value.Item2)).ToList();
        return r;
    }

    public static string Token(double n) => n >= 1e9 ? $"{n / 1e9:0.0}B" : n >= 1e6 ? $"{n / 1e6:0.0}M" : n >= 1e3 ? $"{n / 1e3:0}K" : $"{n:0}";
    public static string Soldi(double d) => d >= 1000 ? "$" + d.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture) : d >= 10 ? $"${d:0}" : $"${d:0.00}".Replace(',', '.');

    public static string Mancano(DateTime? t)
    {
        if (t == null) return "";
        var m = t.Value - DateTime.Now;
        if (m.TotalMinutes < 1) return "ora";
        if (m.TotalHours < 1) return $"tra {(int)m.TotalMinutes} min";
        if (m.TotalDays < 1) return $"tra {(int)m.TotalHours} h {m.Minutes:00} min";
        return $"tra {(int)m.TotalDays} g {m.Hours} h";
    }
}
