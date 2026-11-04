# Estensioni Vascend Engine — API 1

La home mostra le estensioni installate. Vascend Technologies è integrata e comprende tutte le sezioni esistenti. Le altre estensioni vivono in `%USERPROFILE%\.config\vascend-engine\extensions\<id>`.

Per installare una community extension, premi **Aggiungi estensione**. Puoi collegare un repository GitHub pubblico oppure scegliere **Carica da cartella**. Le estensioni sono copiate in locale; le impostazioni sono conservate separatamente.

## Pubblicare e installare da GitHub

1. Crea un repository pubblico con `extension.json` nella radice, usando l'esempio sotto. Per più plugin, crea sottocartelle ognuna con il proprio `extension.json` e i relativi file.
2. Per una pagina dichiarativa basta il manifest. Per un plugin con codice, includi DLL già compilate, dipendenze e `.deps.json` nella cartella del plugin. Engine non esegue build, script di installazione o comandi dal repository.
3. L'utente apre **Aggiungi estensione**, incolla `https://github.com/autore/repository` oppure `autore/repository`, poi preme **Trova plugin** e **Installa** sulla voce desiderata. Sono accettati anche URL con suffisso `.git`.
4. Il campo branch/tag/commit è facoltativo. Lasciandolo vuoto, si segue il branch predefinito del repository. Per i branch con slash, usa il campo dedicato; non incollare URL `/tree/...`.

Manifest minimo funzionante, pronto da pubblicare nella radice di una repo:

```json
{
  "apiVersion": 1,
  "id": "community.mio-plugin",
  "name": "Il mio plugin",
  "version": "1.0.0",
  "author": "Il tuo nome",
  "description": "La mia prima estensione Vascend Engine.",
  "settings": [
    { "key": "enabled", "label": "Abilitata", "type": "boolean", "default": true }
  ]
}
```

L'ID deve essere unico: Engine non sovrascrive un'estensione già installata da un'altra origine. Il file manifest descrive il plugin, ma non implementa comportamenti: per quelli serve l'interfaccia `IEngineExtension` descritta sotto. I plugin di Claude Code non sono direttamente compatibili con Engine.

## Aggiornare un plugin collegato

Nella stessa pagina trovi **Repository collegati**, con origine, riferimento e commit installato. **Aggiorna** scarica il nuovo snapshot e prepara l'aggiornamento, che si applica al prossimo avvio dell'app. Le impostazioni dell'utente non vengono sostituite. Un tag o SHA fissato continua a indicare quella versione; per seguire nuovi commit usa un branch o lascia il campo vuoto.

Il percorso del plugin all'interno della repo e il suo ID devono restare stabili. La provenienza viene salvata dall'host in `extension-sources/<id>.json`. Gli aggiornamenti pronti restano in `extension-updates/<id>`, e gli errori di applicazione vengono registrati in `errori.log`. Non vengono eseguiti controlli di rete automatici all'avvio. Il controllo con icona di aggiornamento nel catalogo rilegge solo le estensioni locali.

Questa versione supporta repository pubblici su github.com, senza login, submodule o Git LFS. I file devono essere presenti nello snapshot del repository, non solo negli asset di una release. Limiti: 64 MB di download, 256 MB estratti, 10.000 elementi nell'archivio e 100 manifest. Percorsi non validi, link simbolici e duplicati vengono rifiutati. Un plugin nativo ha gli stessi permessi dell'utente e richiede un autore fidato.

## Pagina dichiarativa

Vedi `community-example/extension.json`. Campi: `apiVersion` (1), `id` (lettere minuscole, numeri, punti, trattini, underscore), `name`, `version`, `author`, `description`, `settings`.

Ogni impostazione ha `key`, `label`, `type` (`string`, `boolean`, `number`) e `default`. Engine genera la pagina interna e salva con il pulsante **Salva impostazioni**. I numeri usano il punto decimale. I dati sono separati in `extension-settings/<id>.json` e sono preservati al ritorno al catalogo. Modifiche non salvate vengono scartate quando si cambia pagina.

Un manifest da solo definisce una pagina e dati: per aggiungere comportamenti personalizzati usa una DLL.

## Estensione con codice e pagina WPF libera

Crea una class library per `net8.0-windows10.0.19041.0`, `UseWPF=true`, piattaforma x64. Aggiungi un riferimento al `bin/VascendEngine.dll` installato, con `Private=false` (non distribuire una seconda copia di VascendEngine.dll).

```csharp
using System.Windows;
using System.Windows.Controls;
using VascendEngine;

public sealed class MyExtension : IEngineExtension
{
    public FrameworkElement CreatePage(ExtensionContext context)
    {
        var settings = context.LoadSettings();
        return new TextBlock { Text = "La mia pagina community", FontSize = 24 };
    }
}
```

Nel manifest aggiungi `"entryAssembly": "MyExtension.dll"` e `"entryType": "MyExtension"`. Copia DLL, dipendenze e file `.deps.json` nella cartella dell'estensione. `ExtensionContext` offre `ExtensionDirectory`, `SettingsPath`, `LoadSettings()` e `SaveSettings(Dictionary<string, JsonElement>)`. La DLL può disegnare controlli, reagire ai clic e implementare le proprie funzioni. Le dipendenze vengono risolte per estensione.

Le DLL hanno gli stessi permessi del processo: installa codice solo da autori fidati. Il codice si carica all'apertura della scheda. Questa API è orientata alle pagine: non avvia servizi di background all'avvio di Engine. Per i plugin GitHub usa il flusso di aggiornamento sopra. Per una DLL caricata da cartella locale, chiudi Engine, sostituisci i file e riapri. L'host conserva l'istanza dell'estensione durante la sessione; chi crea la pagina deve gestire l'eventuale riuso dei suoi controlli e il ciclo di vita delle risorse.

## Attivazione, conflitti e ripristino

La home permette di cercare, ordinare e filtrare le estensioni, e di attivarle o disattivarle dalle card. Lo stato persiste in `extension-state.json`. Le nuove installazioni GitHub e locali partono **disattivate**. Anche Vascend Technologies è disattivata per impostazione predefinita; la card integrata ha il simbolo di verifica accanto al nome. I plugin già installati senza una scelta esplicita mantengono il comportamento precedente.

Campi facoltativi del manifest:

```json
{
  "conflictsWith": ["community.altro-plugin"],
  "exclusiveResources": ["desktop.widgets"]
}
```

`conflictsWith` elenca ID incompatibili, controllati in entrambi i sensi. `exclusiveResources` dichiara risorse che il plugin gestisce in esclusiva. Nomi uguali e gerarchie sovrapposte (per esempio `desktop.widgets` e `desktop.widgets.clock`) sono in conflitto. L'attivazione viene bloccata finché l'altra estensione resta attiva. Gli ID duplicati sono bloccati.

Vascend Technologies ha ID `vascend-technologies` e riserva `desktop.bar`, `desktop.widgets`, `desktop.island`, `system.appearance`, `editor.vscode.theme`, `editor.vascend.theme`, `terminal.theme`. L'ID precedente `vascend-ghiaccio` resta riservato. I manifest devono dichiarare correttamente risorse e incompatibilità: Engine non può dedurre ogni effetto arbitrario di una DLL né rilevare conflitti non dichiarati.

Una DLL può implementare `IEngineExtensionLifecycle`, con `OnActivated(ExtensionContext)` e `OnDeactivated()`. L'attivazione del codice avviene alla prima apertura della pagina; `OnDeactivated` deve fermare task e servizi, rimuovere eventi e rilasciare le risorse. `ExtensionContext.ActivationToken` viene annullato alla disattivazione. Per le DLL già caricate prive di questo contratto viene richiesto un riavvio prima della riattivazione; il blocco della pagina non può interrompere forzatamente codice arbitrario già avviato.

Se implementa anche `IEngineExtensionRestoration`, il plugin espone `RestorationDescription` e `Task RestoreSettingsAsync(CancellationToken)`. Alla disattivazione l'utente può scegliere se eseguire il ripristino. Il plugin è responsabile di conservare e ripristinare le proprie impostazioni precedenti.

Disattivando Vascend Technologies compare una finestra con **Solo disattiva** oppure **Disattiva e ripristina**. Si può scegliere di mostrare la taskbar di Windows (fermando il guardiano che la nasconde), togliere l'avvio automatico di barra e stile, e ripristinare tema/font delle app per cui esistono file `.prima-vascend-engine.bak`. Il ripristino delle app interessa i campi gestiti da Engine e preserva le altre impostazioni, salvando anche una copia prima del ripristino. Le preferenze taskbar/avvio vengono conservate per la successiva riattivazione. I colori di Windows e i widget salvati non vengono azzerati.

## Font incluso

La sezione **Vascend Technologies → Font** contiene Vascend Grotesk Mono, incorporato nell'app nei pesi Regular, Medium e SemiBold. Le anteprime usano direttamente i file inclusi; **Installa font incluso** registra i tre pesi per l'utente Windows. Il font è anche in cima ai selettori di editor e terminale, marcato **Incluso**: scegliendolo viene installato prima di impostarlo.

Il menu dell'ordinamento offre Nome A–Z, Nome Z–A, Attive prima e Più recenti. I pulsanti usano morph degli angoli, fondo e pressione, rispettando la preferenza Windows sulle animazioni. Il ritaglio nativo della finestra e quello del contenuto mantengono i vuoti dei bordi a scaletta realmente trasparenti.

Le card sono cliccabili in tutta la loro superficie; l'interruttore è un comando separato. L'estensione integrata rimane consultabile quando è disattivata o in conflitto: navigazione e scorrimento restano disponibili, mentre modifica e applicazione sono bloccate. Per le DLL esterne disattivate, le impostazioni in sola lettura vengono generate dal manifest: la pagina WPF personalizzata non viene eseguita finché il plugin non è attivo.
