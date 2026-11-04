# Esempio di plugin Vascend Engine

Pubblica i file di questa cartella nella radice di un repository GitHub pubblico. Cambia `id`, `name`, `author` e `description` in `extension.json` per creare il tuo plugin.

In Vascend Engine: **Aggiungi estensione → incolla il link del repository → Trova plugin → Installa**.

Questo esempio genera una pagina con impostazioni persistenti. Per implementare funzioni personalizzate, aggiungi una DLL WPF che implementi `VascendEngine.IEngineExtension` e specifica `entryAssembly` e `entryType` nel manifest. I dettagli si trovano in `EXTENSIONS.md` accanto all'app installata.
