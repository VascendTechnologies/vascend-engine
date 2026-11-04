using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;

namespace BarraVascend;

// Musica in riproduzione dai controlli multimediali di Windows (Spotify, browser, ecc.).
// Se nessun lettore suona ma un'app manda audio (una live in PiP, un video senza controlli), la fonte diventa quell'app:
// titolo dalla sua finestra, pausa = silenzia l'app nel mixer.
static class Musica
{
    static GlobalSystemMediaTransportControlsSessionManager gestore;
    static GlobalSystemMediaTransportControlsSession sessione;
    public static string Titolo = "", Artista = "", App = "";
    public static ImageSource Copertina;
    public static bool Suona, Presente;
    public static TimeSpan Posizione, Durata;
    static DateTimeOffset aggiornata;
    public static event Action Cambiata;
    public static bool DaAudio;
    static DateTime ultimoSuono;
    static System.Windows.Threading.DispatcherTimer guardia;

    public static async void Avvia()
    {
        try
        {
            gestore = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            gestore.CurrentSessionChanged += (_, _) => BarraVascend.App.Io.Dispatcher.BeginInvoke(Collega);
            gestore.SessionsChanged += (_, _) => BarraVascend.App.Io.Dispatcher.BeginInvoke(Collega);
            Collega();
        }
        catch (Exception e) { BarraVascend.App.Log("musica: " + e.Message); }
        guardia = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        guardia.Tick += (_, _) => Sorveglia();
        guardia.Start();
    }

    static bool LettoreSuona()
    {
        try { return sessione?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
        catch { return false; }
    }

    // Le fonti fra cui scegliere con le frecce del pannello: ogni lettore e ogni app che suona senza lettore.
    public class Voce { public string Chiave, Aumid, Exe; public GlobalSystemMediaTransportControlsSession Sessione; }
    public static List<Voce> Fonti = new();
    static readonly Dictionary<string, DateTime> sentite = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, (string tit, string art)> titoliNoti = new(StringComparer.OrdinalIgnoreCase);
    static string fissata;

    public static string Chiave => DaAudio ? "a:" + App.ToLowerInvariant() : "s:" + (sessione?.SourceAppUserModelId ?? "");

    static bool Corrisponde(string aumid, string exe)
    {
        if (string.IsNullOrEmpty(aumid)) return false;
        var a = aumid.ToLowerInvariant();
        var n = System.IO.Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
        return a == n || a == n + ".exe" || a.StartsWith(n + ".") || a.EndsWith("!" + n);
    }

    static bool Suona_(GlobalSystemMediaTransportControlsSession s)
    {
        try { return s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
        catch { return false; }
    }

    static void Elenca(List<Audio.Fonte> suonano)
    {
        var l = new List<Voce>();
        try
        {
            if (gestore != null)
                foreach (var s in gestore.GetSessions())
                    if (!l.Exists(v => v.Chiave == "s:" + s.SourceAppUserModelId)) l.Add(new Voce { Chiave = "s:" + s.SourceAppUserModelId, Aumid = s.SourceAppUserModelId, Sessione = s });
        }
        catch { }
        // un'app resta fra le fonti per qualche secondo dopo l'ultimo suono: le pause di una live non la fanno sparire
        var adesso = DateTime.Now;
        foreach (var f in suonano) sentite[f.Exe] = adesso;
        foreach (var k in sentite.Where(x => (adesso - x.Value).TotalSeconds > 5).Select(x => x.Key).ToList()) sentite.Remove(k);
        var audio = sentite.Keys.ToList();
        if (DaAudio && !audio.Exists(e => string.Equals(System.IO.Path.GetFileName(e), App, StringComparison.OrdinalIgnoreCase)) && percorsoAudio != null) audio.Add(percorsoAudio);
        foreach (var exe in audio)
        {
            // l'audio di un'app il cui lettore sta suonando è quel lettore
            if (l.Exists(v => v.Sessione != null && Corrisponde(v.Aumid, exe) && Suona_(v.Sessione))) continue;
            var k = "a:" + System.IO.Path.GetFileName(exe).ToLowerInvariant();
            if (!l.Exists(v => v.Chiave == k)) l.Add(new Voce { Chiave = k, Exe = exe });
        }
        if (l.Select(v => v.Chiave).SequenceEqual(Fonti.Select(v => v.Chiave))) { Fonti = l; return; }
        Fonti = l;
        BarraVascend.App.Log("musica: fonti " + string.Join(", ", l.Select(v => v.Chiave)));
        Avvisa();
    }

    public static ImageSource Icona(Voce v)
    {
        if (v == null) return null;
        var gruppi = BarraVascend.App.Io.Finestre.Gruppi;
        if (v.Exe != null) return Gruppo(v.Exe)?.Icona ?? Shell.Icona(v.Exe, 32);
        var g = gruppi.FirstOrDefault(x => x.Aumid != null && string.Equals(x.Aumid, v.Aumid, StringComparison.OrdinalIgnoreCase))
            ?? gruppi.FirstOrDefault(x => x.Exe != null && Corrisponde(v.Aumid, x.Exe));
        return g?.Icona ?? Shell.IconaApp(v.Aumid);
    }

    public static Voce Attuale => Fonti.FirstOrDefault(v => v.Chiave == Chiave);

    // Le frecce del pannello: la fonte scelta resta finché c'è, poi si torna alla scelta automatica.
    public static void Cambia(int d)
    {
        if (Fonti.Count < 2) return;
        int i = Fonti.FindIndex(v => v.Chiave == Chiave);
        i = ((i < 0 ? 0 : i) + d + Fonti.Count) % Fonti.Count;
        fissata = Fonti[i].Chiave;
        Mostra(Fonti[i]);
    }

    static void Mostra(Voce v)
    {
        if (v.Sessione != null)
        {
            if (!DaAudio && sessione == v.Sessione) return;
            DaAudio = false;
            Usa(v.Sessione, true);
        }
        else if (!DaAudio || !string.Equals(App, System.IO.Path.GetFileName(v.Exe), StringComparison.OrdinalIgnoreCase)) Prendi(v.Exe);
        else Titola(v.Exe);
    }

    static bool inCorso;
    static async void Sorveglia()
    {
        if (inCorso) return;
        inCorso = true;
        List<Audio.Fonte> suonano;
        try { suonano = await Task.Run(Audio.Suonano); }
        finally { inCorso = false; }
        Elenca(suonano);
        if (fissata != null)
        {
            var v = Fonti.FirstOrDefault(x => x.Chiave == fissata);
            if (v != null) { if (v.Sessione == null && suonano.Count > 0) ultimoSuono = DateTime.Now; Mostra(v); return; }
            fissata = null;
            if (!DaAudio) Collega();
        }
        if (LettoreSuona())
        {
            if (DaAudio) { DaAudio = false; _ = Leggi(); }
            return;
        }
        // messa in pausa da qui: resta finché è silenziata
        if (DaAudio && !Suona && Audio.MutoApp(App)) return;
        var f = suonano.FirstOrDefault();
        if (f != null)
        {
            ultimoSuono = DateTime.Now;
            if (!DaAudio || !string.Equals(App, f.Nome, StringComparison.OrdinalIgnoreCase) || !Suona) Prendi(f.Exe);
            else Titola(f.Exe);
            return;
        }
        if (!DaAudio) return;
        if ((DateTime.Now - ultimoSuono).TotalSeconds < 4) return;
        DaAudio = false;
        _ = Leggi();
    }

    static string percorsoAudio;

    static void Prendi(string exe)
    {
        DaAudio = true;
        percorsoAudio = exe;
        App = System.IO.Path.GetFileName(exe);
        Suona = true;
        Presente = true;
        Durata = Posizione = TimeSpan.Zero;
        aggiornata = default;
        var g = Gruppo(exe);
        Copertina = g?.Icona ?? Shell.Icona(exe);
        if (titoliNoti.TryGetValue(exe, out var noto)) (Titolo, Artista) = noto;
        else Titolo = Artista = "";
        Titola(exe);
        if (Titolo.Length == 0) { Titolo = g?.Nome ?? System.IO.Path.GetFileNameWithoutExtension(exe); Artista = "In riproduzione"; }
        BarraVascend.App.Log($"musica: fonte audio {App} · {Titolo} · {Artista}");
        Avvisa();
    }

    static Gruppo Gruppo(string exe) => BarraVascend.App.Io.Finestre.Gruppi.FirstOrDefault(x => x.Exe != null && string.Equals(System.IO.Path.GetFileName(x.Exe), System.IO.Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase) && x.Finestre.Count > 0)
        ?? BarraVascend.App.Io.Finestre.Gruppi.FirstOrDefault(x => x.Exe != null && string.Equals(System.IO.Path.GetFileName(x.Exe), System.IO.Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase));

    static readonly string[] Siti = { "tiktok", "youtube", "twitch", "kick", "netflix", "prime video", "disney", "live", "rai", "mediaset", "dazn" };
    static readonly string[] Browser = { " - Google Chrome", " - Brave", " - Microsoft Edge", " - Mozilla Firefox", " - Opera", " - Vivaldi", " — Mozilla Firefox" };

    // Il titolo della finestra che più probabilmente suona: prima quelle con un sito di video o musica.
    static void Titola(string exe)
    {
        var g = Gruppo(exe);
        if (g == null) return;
        // tutte le finestre dell'eseguibile: i profili di Chrome sono gruppi diversi
        var nome = System.IO.Path.GetFileName(exe);
        var titoli = BarraVascend.App.Io.Finestre.Gruppi.Where(x => x.Exe != null && string.Equals(System.IO.Path.GetFileName(x.Exe), nome, StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.Finestre).Select(x => x.Titolo ?? "").Where(t => t.Length > 0 && t != "Picture in picture").ToList();
        var t = titoli.FirstOrDefault(x => Siti.Any(k => System.Text.RegularExpressions.Regex.IsMatch(x, @"\b" + System.Text.RegularExpressions.Regex.Escape(k) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) ?? (titoli.Count == 1 ? titoli[0] : null);
        if (t == null) { if (Titolo.Length == 0) { Titolo = g.Nome ?? ""; Artista = "In riproduzione"; Avvisa(); } return; }
        foreach (var b in Browser) if (t.EndsWith(b)) { t = t[..^b.Length]; break; }
        string tit = t, art = g.Nome ?? "";
        int i = t.LastIndexOf(" - ");
        if (i > 0) { tit = t[..i].Trim(); art = t[(i + 3)..].Trim(); }
        titoliNoti[exe] = (tit, art);
        if (tit == Titolo && art == Artista) return;
        Titolo = tit;
        Artista = art;
        Avvisa();
    }

    static void Avvisa() => BarraVascend.App.Io.Dispatcher.BeginInvoke(() => Cambiata?.Invoke());

    static void Collega()
    {
        if (gestore == null) return;
        GlobalSystemMediaTransportControlsSession s = null;
        if (fissata != null && fissata.StartsWith("s:"))
            try { s = gestore.GetSessions().FirstOrDefault(x => "s:" + x.SourceAppUserModelId == fissata); } catch { }
        s ??= gestore.GetCurrentSession();
        Usa(s, false);
    }

    static void Usa(GlobalSystemMediaTransportControlsSession s, bool forza)
    {
        if (s == sessione && !forza) { _ = Leggi(); return; }
        if (sessione != null)
        {
            sessione.MediaPropertiesChanged -= Prop;
            sessione.PlaybackInfoChanged -= Info;
            sessione.TimelinePropertiesChanged -= Tempo;
        }
        sessione = s;
        if (s != null)
        {
            s.MediaPropertiesChanged += Prop;
            s.PlaybackInfoChanged += Info;
            s.TimelinePropertiesChanged += Tempo;
        }
        _ = Leggi();
    }

    static void Prop(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) => _ = Leggi();
    static void Info(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e)
    {
        if (DaAudio) { BarraVascend.App.Io.Dispatcher.BeginInvoke(Sorveglia); return; }
        LeggiStato(); Avvisa();
    }
    static void Tempo(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) { if (DaAudio) return; LeggiTempo(); Avvisa(); }

    static void LeggiStato()
    {
        try { Suona = sessione?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; }
        catch { Suona = false; }
    }

    static void LeggiTempo()
    {
        try
        {
            var t = sessione?.GetTimelineProperties();
            if (t == null) return;
            Posizione = t.Position;
            Durata = t.EndTime - t.StartTime;
            aggiornata = t.LastUpdatedTime;
        }
        catch { }
    }

    static async Task Leggi()
    {
        if (DaAudio) return;
        try
        {
            if (sessione == null)
            {
                Presente = false;
                Titolo = Artista = "";
                Copertina = null;
                Avvisa();
                return;
            }
            var p = await sessione.TryGetMediaPropertiesAsync();
            if (DaAudio) return;
            Titolo = p?.Title ?? "";
            Artista = p?.Artist ?? "";
            App = sessione.SourceAppUserModelId ?? "";
            Presente = Titolo.Length > 0;
            Copertina = null;
            if (p?.Thumbnail != null)
            {
                using var st = await p.Thumbnail.OpenReadAsync();
                using var ms = new MemoryStream();
                await st.AsStreamForRead().CopyToAsync(ms);
                ms.Position = 0;
                await BarraVascend.App.Io.Dispatcher.InvokeAsync(() =>
                {
                    var b = new BitmapImage();
                    b.BeginInit();
                    b.CacheOption = BitmapCacheOption.OnLoad;
                    b.DecodePixelWidth = 256;
                    b.StreamSource = ms;
                    b.EndInit();
                    b.Freeze();
                    Copertina = b;
                });
            }
            LeggiStato();
            LeggiTempo();
        }
        catch (Exception e) { BarraVascend.App.Log("musica: " + e.Message); }
        Avvisa();
    }

    // Posizione stimata: Windows la aggiorna di rado, in mezzo si conta il tempo trascorso.
    public static TimeSpan Ora
    {
        get
        {
            if (!Suona || aggiornata == default) return Posizione;
            var p = Posizione + (DateTimeOffset.Now - aggiornata);
            return Durata > TimeSpan.Zero && p > Durata ? Durata : p;
        }
    }

    public static double Avanzamento => Durata.TotalSeconds > 0 ? Math.Clamp(Ora.TotalSeconds / Durata.TotalSeconds, 0, 1) : 0;

    public static async void PlayPausa()
    {
        if (DaAudio)
        {
            Suona = !Suona;
            Audio.MutaApp(App, !Suona);
            ultimoSuono = DateTime.Now;
            Avvisa();
            return;
        }
        try { await sessione?.TryTogglePlayPauseAsync(); } catch { }
    }
    public static async void Avanti() { if (DaAudio) return; try { await sessione?.TrySkipNextAsync(); } catch { } }
    public static async void Indietro() { if (DaAudio) return; try { await sessione?.TrySkipPreviousAsync(); } catch { } }
    public static async void Vai(double frazione)
    {
        try { if (Durata > TimeSpan.Zero) await sessione?.TryChangePlaybackPositionAsync((long)(Durata.Ticks * frazione)); } catch { }
    }
}
