# Vascend Engine

Applicazione desktop Windows di **Vascend Technologies** per personalizzare l'ambiente di lavoro e gestire estensioni della community.

## Funzioni

- Catalogo di estensioni con ricerca, filtri, ordinamento, attivazione e controllo dei conflitti dichiarati.
- Installazione di plugin da repository GitHub pubblici o cartelle locali, con aggiornamenti al riavvio.
- Estensione integrata **Vascend Technologies**, verificata e disattivata di default: temi, barra desktop, isola, widget, Windows, editor e terminali.
- Impostazioni consultabili in sola lettura mentre un'estensione è disattivata.
- Widget con stili Classico, Editoriale, Terminale e Vetro, con firma personalizzabile.
- Vascend Grotesk Mono incluso: Regular, Medium e SemiBold.
- Interfaccia scura, icone vettoriali, morph dei pulsanti e paesaggio pixel animato.
- Ripristino selettivo di taskbar e impostazioni delle app alla disattivazione.

## Requisiti

Windows 10 versione 2004 o successiva, architettura x64, e **.NET SDK 8** per compilare. Per eseguire la distribuzione framework-dependent serve **.NET Desktop Runtime 8 x64**.

Le API più recenti, come la qualità Wi-Fi senza accesso alla posizione, sono disponibili secondo versione di Windows e supporto del driver; quando non disponibili viene usato un fallback o mostrato uno stato non disponibile.

## Compilazione

Da PowerShell nella radice del repository:

```powershell
dotnet build .\VascendEngine.sln -c Release
.\tools\build.ps1
```

La pubblicazione produce `artifacts/engine` e `artifacts/desktop`, senza includere il runtime .NET. Per installare nella cartella dell'utente:

```powershell
.\tools\install.ps1
```

L'installer compila, prepara una copia di sicurezza dei file dell'app e copia Engine, il componente desktop e gli asset nelle rispettive cartelle sotto `%USERPROFILE%\.config`. Non abilita l'estensione né configura avvii automatici. Le preferenze personali già presenti vengono mantenute. Al termine avvia manualmente:

```powershell
& "$env:USERPROFILE\.config\vascend-engine\bin\VascendEngine.exe"
```

Se un'app è in esecuzione, chiudila prima di installare. Attivando l'estensione integrata dalla home, Engine può avviare il componente desktop.

## Estensioni

La documentazione del manifest, dell'API WPF e del ciclo di vita è in [docs/EXTENSIONS.md](docs/EXTENSIONS.md). Un plugin dichiarativo pronto da usare si trova in [examples/community-example](examples/community-example).

Le DLL di estensione vengono eseguite con i permessi dell'utente. Il rilevamento dei conflitti usa incompatibilità e risorse dichiarate nel manifest; non può dedurre gli effetti arbitrari di codice esterno.

## Struttura

```text
apps/vascend-engine/src/   Host WPF, catalogo e gestione delle estensioni
apps/barra-vascend/src/    Barra, isola, widget e modelli condivisi
apps/barra-vascend/assets/ Icone del componente desktop
assets/scripts/           Supporto per la taskbar Windows
docs/                     Documentazione delle estensioni
examples/                 Plugin di esempio
tools/                    Compilazione e installazione
```

I due progetti condividono alcuni file C# tramite riferimenti relativi. La soluzione si compila senza le cartelle di lavoro originali del computer di sviluppo.

## Configurazione locale e integrazioni opzionali

Engine conserva preferenze e stato in `%USERPROFILE%\.config\vascend-engine`. Questi dati, token, cache, backup e output di compilazione non fanno parte del repository.

Il monitor Claude usa `CLAUDE_CONFIG_DIR` se impostata; altrimenti cerca nella cartella `.claude` del profilo Windows. Nessuna credenziale è inclusa nei sorgenti.

I collegamenti WSL possono essere configurati tramite `VASCEND_WSL_DISTRO` (predefinito `Ubuntu`) e `VASCEND_WSL_HOME` (percorso UNC completo della home Linux). Se il nome utente Linux differisce da quello Windows, impostare esplicitamente `VASCEND_WSL_HOME`.

Il font Mono è incluso come risorsa dell'app. Gli asset grafici e i font Vascend mantengono la propria titolarità; il repository non attribuisce una licenza open source implicita.
