using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VascendEngine;

sealed class GitHubExtensionSource
{
    public string Repository { get; set; }
    public string Reference { get; set; } = "HEAD";
    public string Commit { get; set; }
    public string Subdirectory { get; set; }
    public DateTime InstalledAt { get; set; }
}
sealed class GitHubDownload : IDisposable
{
    public string TemporaryDirectory { get; set; }
    public string RepositoryDirectory { get; set; }
    public GitHubExtensionSource Source { get; set; }
    public List<ExtensionManifest> Plugins { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public void Dispose() => GitHubExtensions.TryDelete(TemporaryDirectory);
}

// Scarica snapshot immutabili, senza avviare git, script del repository o compilazioni.
sealed class GitHubExtensions
{
    const long MaxDownload = 64L * 1024 * 1024, MaxExtracted = 256L * 1024 * 1024;
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    static readonly HttpClient SharedClient = CreateClient();
    readonly HttpClient client;
    readonly string root;
    string Extensions => Path.Combine(root,"extensions");
    string Sources => Path.Combine(root,"extension-sources");
    string Pending => Path.Combine(root,"extension-updates");
    public GitHubExtensions(string directory = null, HttpClient http = null) { root=directory ?? BarraVascend.Stile.Cartella; client=http ?? SharedClient; }
    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout=TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VascendEngine/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
    public static string ParseRepository(string input)
    {
        string value=(input ?? "").Trim().TrimEnd('/');
        if(value.StartsWith("https://",StringComparison.OrdinalIgnoreCase))
        {
            if(!Uri.TryCreate(value,UriKind.Absolute,out var uri) || uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new Exception("Inserisci un link https://github.com/autore/repository.");
            value=uri.AbsolutePath.Trim('/');
        }
        if(value.EndsWith(".git",StringComparison.OrdinalIgnoreCase)) value=value[..^4];
        if(!Regex.IsMatch(value,@"^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_][A-Za-z0-9_.-]{0,99}$"))
            throw new Exception("Usa autore/repository oppure il link GitHub del repository. Branch e tag vanno nel campo dedicato.");
        return value;
    }
    static void ValidateId(string id)
    {
        if(id == null || !Regex.IsMatch(id,@"^[a-z0-9][a-z0-9._-]{0,79}$") || id == "vascend-ghiaccio" || id == BarraVascend.ExtensionState.BuiltinId) throw new Exception("ID estensione non valido.");
    }
    static async Task CheckResponse(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode) return;
        if(response.StatusCode == HttpStatusCode.NotFound) throw new Exception("Repository o riferimento non trovato. Sono supportati i repository pubblici.");
        if(response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429) throw new Exception("GitHub ha limitato le richieste. Riprova più tardi.");
        await Task.CompletedTask;
        throw new Exception("GitHub non disponibile (HTTP " + (int)response.StatusCode + ").");
    }
    public async Task<GitHubDownload> DownloadAsync(string input, string reference, CancellationToken token, IProgress<string> progress = null)
    {
        string repository=ParseRepository(input);
        reference=string.IsNullOrWhiteSpace(reference) ? "HEAD" : reference.Trim();
        if(reference.Length > 200 || reference.Any(char.IsControl)) throw new Exception("Riferimento non valido.");
        var download=new GitHubDownload {
            TemporaryDirectory=Path.Combine(root,"extension-downloads",Guid.NewGuid().ToString("N")),
            Source=new GitHubExtensionSource { Repository=repository, Reference=reference }
        };
        try
        {
            progress?.Report("Verifica del repository…");
            using(var response=await client.GetAsync($"https://api.github.com/repos/{repository}/commits/{Uri.EscapeDataString(reference)}",token))
            {
                await CheckResponse(response);
                using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                string sha=document.RootElement.GetProperty("sha").GetString();
                if(sha == null || !Regex.IsMatch(sha,@"^[a-fA-F0-9]{40}$")) throw new Exception("Commit GitHub non valido.");
                download.Source.Commit=sha.ToLowerInvariant();
            }
            Directory.CreateDirectory(download.TemporaryDirectory);
            string archive=Path.Combine(download.TemporaryDirectory,"repository.zip");
            progress?.Report("Download del repository…");
            using(var response=await client.GetAsync($"https://api.github.com/repos/{repository}/zipball/{download.Source.Commit}",HttpCompletionOption.ResponseHeadersRead,token))
            {
                await CheckResponse(response);
                if(response.Content.Headers.ContentLength > MaxDownload) throw new Exception("Repository troppo grande (limite download: 64 MB).");
                using var inputStream=await response.Content.ReadAsStreamAsync(token);
                using var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
                var buffer=new byte[81920]; long total=0; int read;
                while((read=await inputStream.ReadAsync(buffer,token)) > 0)
                {
                    total+=read;
                    if(total > MaxDownload) throw new Exception("Repository troppo grande (limite download: 64 MB).");
                    await output.WriteAsync(buffer.AsMemory(0,read),token);
                }
            }
            progress?.Report("Ricerca delle estensioni…");
            await Task.Run(() => {
                string extracted=Path.Combine(download.TemporaryDirectory,"repository");
                ExtractArchive(archive,extracted,token);
                string[] roots=Directory.GetDirectories(extracted);
                if(roots.Length != 1 || Directory.GetFiles(extracted).Length != 0) throw new Exception("Struttura archivio GitHub non valida.");
                download.RepositoryDirectory=roots[0];
                var manifests=Directory.EnumerateFiles(roots[0],"extension.json",SearchOption.AllDirectories).Take(101).ToList();
                if(manifests.Count > 100) throw new Exception("Il repository contiene troppe estensioni (massimo 100).");
                foreach(string manifest in manifests)
                {
                    token.ThrowIfCancellationRequested();
                    try { download.Plugins.Add(Finestra.LeggiManifest(Path.GetDirectoryName(manifest))); }
                    catch(Exception ex) { download.Errors.Add(Path.GetRelativePath(roots[0],manifest) + ": " + ex.Message); }
                }
                if(download.Plugins.Count == 0) throw new Exception(manifests.Count == 0
                    ? "Nessun plugin Engine trovato. Il repository deve contenere un file extension.json; per le DLL servono i binari già compilati."
                    : "Nessun plugin valido. " + string.Join("; ",download.Errors.Take(3)));
            },token);
            return download;
        }
        catch { download.Dispose(); throw; }
    }
    internal static void ExtractArchive(string zip, string destination, CancellationToken token)
    {
        Directory.CreateDirectory(destination);
        string prefix=Path.GetFullPath(destination)+Path.DirectorySeparatorChar;
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var archive=ZipFile.OpenRead(zip);
        if(archive.Entries.Count > 10000) throw new Exception("Archivio con troppi file.");
        long total=0;
        foreach(var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            string name=entry.FullName;
            // Rifiuta percorsi assoluti, ADS/drive Windows, link e nomi ambigui.
            if(name.Length == 0 || name.Contains('\\') || name.Contains(':') || name.StartsWith('/') || ((entry.ExternalAttributes >> 16)&0xF000) == 0xA000 || (entry.ExternalAttributes&0x400) != 0)
                throw new Exception("L'archivio contiene un percorso o collegamento non supportato.");
            string[] parts=name.TrimEnd('/').Split('/');
            if(parts.Any(part => part.Length == 0 || part == "." || part == ".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(part,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase)))
                throw new Exception("L'archivio contiene un nome di file non valido.");
            string path=Path.GetFullPath(Path.Combine(destination,name.Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) || !paths.Add(path)) throw new Exception("Percorso fuori archivio o duplicato.");
            if(name.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
            total+=entry.Length;
            if(total > MaxExtracted) throw new Exception("Archivio troppo grande una volta estratto (limite: 256 MB).");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var input=entry.Open();
            using var output=new FileStream(path,FileMode.CreateNew);
            var buffer=new byte[81920]; int read; long written=0;
            while((read=input.Read(buffer,0,buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested(); written+=read;
                if(written > entry.Length) throw new Exception("Dimensione archivio non valida.");
                output.Write(buffer,0,read);
            }
        }
    }
    public GitHubExtensionSource ReadSource(string id)
    {
        ValidateId(id);
        string path=Path.Combine(Sources,id+".json");
        if(!File.Exists(path)) return null;
        var source=JsonSerializer.Deserialize<GitHubExtensionSource>(File.ReadAllText(path),Json);
        if(source == null) throw new Exception("Origine GitHub non valida.");
        source.Repository=ParseRepository(source.Repository);
        return source;
    }
    public bool HasPending(string id) { ValidateId(id); return Directory.Exists(Path.Combine(Pending,id)); }
    GitHubExtensionSource SourceFor(GitHubDownload download, ExtensionManifest plugin)
    {
        string directory=Path.GetFullPath(plugin.Directory);
        string repository=Path.GetFullPath(download.RepositoryDirectory);
        if(directory != repository && !directory.StartsWith(repository+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("Plugin fuori dal repository.");
        return new GitHubExtensionSource { Repository=download.Source.Repository, Reference=download.Source.Reference, Commit=download.Source.Commit,
            Subdirectory=Path.GetRelativePath(repository,directory).Replace('\\','/'),InstalledAt=DateTime.UtcNow };
    }
    static void Copy(string source, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if((File.GetAttributes(source)&FileAttributes.ReparsePoint) != 0) throw new Exception("Collegamenti non supportati.");
        Directory.CreateDirectory(destination);
        foreach(string file in Directory.GetFiles(source))
        {
            token.ThrowIfCancellationRequested();
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint) != 0) throw new Exception("Collegamenti non supportati.");
            File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));
        }
        foreach(string directory in Directory.GetDirectories(source)) Copy(directory,Path.Combine(destination,Path.GetFileName(directory)),token);
    }
    void SaveSource(string id, GitHubExtensionSource source)
    {
        Directory.CreateDirectory(Sources);
        string path=Path.Combine(Sources,id+".json"), temporary=path+".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(source,Json));
        File.Move(temporary,path,true);
    }
    public void Install(GitHubDownload download, ExtensionManifest plugin, CancellationToken token)
    {
        ValidateId(plugin.Id);
        var source=SourceFor(download,plugin);
        string destination=Path.Combine(Extensions,plugin.Id);
        if(Directory.Exists(destination)) throw new Exception("Questa estensione è già installata. Usa Aggiorna per il repository collegato.");
        string stage=Path.Combine(root,"extension-stage-"+Guid.NewGuid().ToString("N"));
        bool moved=false;
        try
        {
            Copy(plugin.Directory,stage,token);
            if(Finestra.LeggiManifest(stage).Id != plugin.Id) throw new Exception("ID del plugin cambiato.");
            token.ThrowIfCancellationRequested(); Directory.CreateDirectory(Extensions);
            BarraVascend.ExtensionState.Set(plugin.Id,false,root);
            Directory.Move(stage,destination); moved=true;
            SaveSource(plugin.Id,source);
        }
        catch { if(moved) TryDelete(destination); throw; }
        finally { TryDelete(stage); }
    }
    public string Update(GitHubDownload download, string id, CancellationToken token)
    {
        ValidateId(id);
        var existing=ReadSource(id) ?? throw new Exception("Il plugin non è collegato a GitHub.");
        if(!string.Equals(existing.Repository,download.Source.Repository,StringComparison.OrdinalIgnoreCase) || existing.Reference != download.Source.Reference) throw new Exception("Origine dell'aggiornamento diversa dal plugin installato.");
        var plugin=download.Plugins.SingleOrDefault(p => p.Id == id && Path.GetRelativePath(download.RepositoryDirectory,p.Directory).Replace('\\','/') == existing.Subdirectory)
            ?? throw new Exception("Il plugin non è più presente nello stesso percorso del repository.");
        if(existing.Commit == download.Source.Commit && !HasPending(id)) return "Già aggiornato.";
        var source=SourceFor(download,plugin);
        // Staging per tutti i plugin: nessuna DLL sostituita mentre è caricata.
        string stage=Path.Combine(root,"extension-stage-"+Guid.NewGuid().ToString("N"));
        string pending=Path.Combine(Pending,id);
        try
        {
            Copy(plugin.Directory,Path.Combine(stage,"plugin"),token);
            if(Finestra.LeggiManifest(Path.Combine(stage,"plugin")).Id != id) throw new Exception("ID del plugin cambiato.");
            File.WriteAllText(Path.Combine(stage,"source.json"),JsonSerializer.Serialize(source,Json));
            token.ThrowIfCancellationRequested(); Directory.CreateDirectory(Pending);
            // Mantiene un eventuale aggiornamento precedente finché quello nuovo non è pronto.
            string previous=pending+".previous-"+Guid.NewGuid().ToString("N");
            bool replaced=Directory.Exists(pending);
            if(replaced) Directory.Move(pending,previous);
            try { Directory.Move(stage,pending); }
            catch { if(replaced) Directory.Move(previous,pending); throw; }
            TryDelete(previous);
            return "Aggiornamento pronto. Verrà applicato al prossimo avvio di Engine.";
        }
        finally { TryDelete(stage); }
    }
    public List<string> ApplyPending()
    {
        var errors=new List<string>();
        if(!Directory.Exists(Pending)) return errors;
        foreach(string pending in Directory.GetDirectories(Pending))
        {
            string id=Path.GetFileName(pending),backup=null; bool swapped=false;
            string destination=Path.Combine(Extensions,id);
            try
            {
                ValidateId(id);
                var plugin=Finestra.LeggiManifest(Path.Combine(pending,"plugin"));
                if(plugin.Id != id || !Directory.Exists(destination)) throw new Exception("Aggiornamento senza installazione corrispondente.");
                var source=JsonSerializer.Deserialize<GitHubExtensionSource>(File.ReadAllText(Path.Combine(pending,"source.json")),Json) ?? throw new Exception("Origine mancante.");
                var existing=ReadSource(id) ?? throw new Exception("Origine installata mancante.");
                if(ParseRepository(source.Repository) != existing.Repository || source.Subdirectory != existing.Subdirectory || source.Reference != existing.Reference) throw new Exception("Origine aggiornamento non corrispondente.");
                backup=Path.Combine(root,"extension-backup-"+Guid.NewGuid().ToString("N"));
                Directory.Move(destination,backup);
                Directory.Move(Path.Combine(pending,"plugin"),destination); swapped=true;
                SaveSource(id,source);
                TryDelete(backup); backup=null;
                TryDelete(pending);
            }
            catch(Exception ex)
            {
                if(backup != null && Directory.Exists(backup))
                {
                    if(swapped && Directory.Exists(destination)) Directory.Move(destination,Path.Combine(pending,"plugin"));
                    if(!Directory.Exists(destination)) Directory.Move(backup,destination);
                }
                errors.Add(id+": "+ex.Message);
            }
        }
        return errors;
    }
    internal static void TryDelete(string directory)
    {
        if(string.IsNullOrEmpty(directory)) return;
        try { if(Directory.Exists(directory)) Directory.Delete(directory,true); } catch { }
    }
}
