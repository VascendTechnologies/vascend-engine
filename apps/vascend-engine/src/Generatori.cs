using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BarraVascend;

namespace VascendEngine;

// Porta lo stile dove serve: tema di VS Code, Windows Terminal, impostazioni dei caratteri e la barra.
static class Generatori
{
    static string Casa => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static string Locale => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public const string IdEstensione = "vascend.tema-ghiaccio";
    public const string NomeTema = "Vascend Ghiaccio";

    // "#RRGGBB" con alfa 0-1 → "#RRGGBBAA"
    static string A(string hex, double a) => Stile.Hex(Stile.Colore(hex)) + ((int)Math.Round(a * 255)).ToString("X2");
    static string H(string hex) => Stile.Hex(Stile.Colore(hex));
    static string Chiaro(string hex, double f) => Stile.Hex(Stile.Scala(Stile.Colore(hex), f));

    // ---------- VS Code ----------

    public static string TemaVsCode(Stile s)
    {
        var (c, tok, sem) = Parti(s);
        var tema = new Dictionary<string, object>
        {
            ["$schema"] = "vscode://schemas/color-theme", ["name"] = NomeTema, ["type"] = "dark", ["semanticHighlighting"] = true,
            ["colors"] = c, ["tokenColors"] = tok, ["semanticTokenColors"] = sem,
        };
        return JsonSerializer.Serialize(tema, new JsonSerializerOptions { WriteIndented = true });
    }

    static (Dictionary<string, string>, object[], Dictionary<string, object>) Parti(Stile s)
    {
        string fondo = H(s.Fondo), lato = H(s.Lato), barra = H(s.Barra), alto = H(s.Alto), testo = H(s.Testo);
        string tenue = A(s.Testo, .62), spento = A(s.Testo, .36), acc = H(s.Accento), gh = H(s.Ghiaccio), ghC = Chiaro(s.Ghiaccio, 1.6);
        string scuro = Chiaro(s.Fondo, 1.03), bordo = A(s.Vetro, .10), sopra = A("#FFFFFF", .07), sel = A(s.Ghiaccio, .2);
        string az = H(s.Azzurro), ar = H(s.Arancio), vi = H(s.Viola), ro = H(s.Rosa), va = H(s.VerdeAcqua), rs = H(s.Rosso);
        string accSopra = Chiaro(s.Accento, 1.25), terminale = Stile.Hex(Stile.Scala(Stile.Colore(s.Fondo), .9));
        var c = new Dictionary<string, string>
        {
            ["focusBorder"] = A(s.Ghiaccio, .5), ["foreground"] = testo, ["descriptionForeground"] = tenue, ["disabledForeground"] = spento,
            ["errorForeground"] = rs, ["icon.foreground"] = tenue, ["widget.border"] = bordo, ["widget.shadow"] = "#00000066",
            ["selection.background"] = sel, ["sash.hoverBorder"] = A(s.Ghiaccio, .4), ["contrastBorder"] = "#00000000",
            ["textLink.foreground"] = gh, ["textLink.activeForeground"] = ghC, ["textPreformat.foreground"] = acc,
            ["textBlockQuote.background"] = lato, ["textBlockQuote.border"] = A(s.Ghiaccio, .33), ["textCodeBlock.background"] = alto,
            ["toolbar.hoverBackground"] = sopra, ["toolbar.activeBackground"] = A("#FFFFFF", .12),

            ["titleBar.activeBackground"] = barra, ["titleBar.activeForeground"] = testo, ["titleBar.inactiveBackground"] = barra,
            ["titleBar.inactiveForeground"] = spento, ["titleBar.border"] = bordo,
            ["commandCenter.background"] = A("#FFFFFF", .05), ["commandCenter.border"] = bordo, ["commandCenter.foreground"] = tenue,
            ["commandCenter.activeBackground"] = A("#FFFFFF", .1), ["commandCenter.activeBorder"] = A(s.Ghiaccio, .33),
            ["menu.background"] = lato, ["menu.foreground"] = testo, ["menu.selectionBackground"] = sel, ["menu.selectionForeground"] = testo,
            ["menu.separatorBackground"] = bordo, ["menu.border"] = bordo, ["menubar.selectionBackground"] = sopra,

            ["activityBar.background"] = barra, ["activityBar.foreground"] = testo, ["activityBar.inactiveForeground"] = spento,
            ["activityBar.border"] = bordo, ["activityBar.activeBorder"] = acc, ["activityBar.activeBackground"] = sopra,
            ["activityBarBadge.background"] = acc, ["activityBarBadge.foreground"] = fondo,
            ["activityBarTop.foreground"] = testo, ["activityBarTop.inactiveForeground"] = spento, ["activityBarTop.activeBorder"] = acc,

            ["sideBar.background"] = lato, ["sideBar.foreground"] = tenue, ["sideBar.border"] = bordo,
            ["sideBarTitle.foreground"] = testo, ["sideBarSectionHeader.background"] = lato, ["sideBarSectionHeader.foreground"] = testo,
            ["sideBarSectionHeader.border"] = bordo,
            ["list.activeSelectionBackground"] = sel, ["list.activeSelectionForeground"] = testo, ["list.inactiveSelectionBackground"] = A(s.Ghiaccio, .12),
            ["list.inactiveSelectionForeground"] = testo, ["list.hoverBackground"] = sopra, ["list.hoverForeground"] = testo,
            ["list.focusOutline"] = A(s.Ghiaccio, .4), ["list.focusBackground"] = sel, ["list.highlightForeground"] = acc,
            ["list.focusHighlightForeground"] = acc, ["list.dropBackground"] = A(s.Ghiaccio, .15), ["list.errorForeground"] = rs,
            ["list.warningForeground"] = ar, ["tree.indentGuidesStroke"] = A(s.Vetro, .15), ["tree.inactiveIndentGuidesStroke"] = A(s.Vetro, .07),
            ["badge.background"] = acc, ["badge.foreground"] = fondo,

            ["editorGroupHeader.tabsBackground"] = barra, ["editorGroupHeader.tabsBorder"] = bordo, ["editorGroupHeader.noTabsBackground"] = barra,
            ["editorGroup.border"] = bordo, ["editorGroup.dropBackground"] = A(s.Ghiaccio, .12),
            ["tab.activeBackground"] = fondo, ["tab.activeForeground"] = testo, ["tab.activeBorderTop"] = acc, ["tab.activeBorder"] = fondo,
            ["tab.inactiveBackground"] = barra, ["tab.inactiveForeground"] = spento, ["tab.hoverBackground"] = Chiaro(s.Barra, 1.04),
            ["tab.hoverForeground"] = testo, ["tab.border"] = bordo, ["tab.unfocusedActiveBorderTop"] = A(s.Ghiaccio, .4),
            ["tab.unfocusedActiveForeground"] = tenue, ["tab.unfocusedInactiveForeground"] = spento,
            ["tab.activeModifiedBorder"] = acc, ["tab.inactiveModifiedBorder"] = A(s.Accento, .4),
            ["breadcrumb.foreground"] = spento, ["breadcrumb.focusForeground"] = testo, ["breadcrumb.activeSelectionForeground"] = testo,
            ["breadcrumb.background"] = fondo, ["breadcrumbPicker.background"] = lato,

            ["editor.background"] = fondo, ["editor.foreground"] = testo, ["editorLineNumber.foreground"] = A(s.Testo, .2),
            ["editorLineNumber.activeForeground"] = gh, ["editorCursor.foreground"] = acc,
            ["editor.selectionBackground"] = A(s.Ghiaccio, .25), ["editor.inactiveSelectionBackground"] = A(s.Ghiaccio, .13),
            ["editor.selectionHighlightBackground"] = A(s.Ghiaccio, .13), ["editor.wordHighlightBackground"] = A(s.Ghiaccio, .11),
            ["editor.wordHighlightStrongBackground"] = A(s.Accento, .11), ["editor.findMatchBackground"] = A(s.Accento, .33),
            ["editor.findMatchHighlightBackground"] = A(s.Accento, .15), ["editor.findRangeHighlightBackground"] = A(s.Ghiaccio, .07),
            ["editor.lineHighlightBackground"] = A("#FFFFFF", .03), ["editor.lineHighlightBorder"] = "#00000000",
            ["editor.rangeHighlightBackground"] = A(s.Ghiaccio, .07), ["editorWhitespace.foreground"] = A(s.Vetro, .12),
            ["editorIndentGuide.background1"] = A(s.Vetro, .07), ["editorIndentGuide.activeBackground1"] = A(s.Vetro, .22),
            ["editorRuler.foreground"] = A(s.Vetro, .08), ["editorLink.activeForeground"] = gh,
            ["editorBracketMatch.background"] = A(s.Accento, .12), ["editorBracketMatch.border"] = A(s.Accento, .5),
            ["editorBracketHighlight.foreground1"] = gh, ["editorBracketHighlight.foreground2"] = vi,
            ["editorBracketHighlight.foreground3"] = ar, ["editorBracketHighlight.foreground4"] = va,
            ["editorBracketHighlight.foreground5"] = ro, ["editorBracketHighlight.foreground6"] = acc,
            ["editorBracketHighlight.unexpectedBracket.foreground"] = rs,
            ["editorError.foreground"] = rs, ["editorWarning.foreground"] = ar, ["editorInfo.foreground"] = az, ["editorHint.foreground"] = gh,
            ["editorGutter.addedBackground"] = A(s.Accento, .7), ["editorGutter.modifiedBackground"] = A(s.Azzurro, .7),
            ["editorGutter.deletedBackground"] = A(s.Rosso, .7), ["editorCodeLens.foreground"] = spento,
            ["editorInlayHint.background"] = A("#FFFFFF", .05), ["editorInlayHint.foreground"] = spento,
            ["editorGhostText.foreground"] = A(s.Testo, .3), ["editorOverviewRuler.border"] = "#00000000",
            ["editorStickyScroll.background"] = fondo, ["editorStickyScrollHover.background"] = scuro, ["editorStickyScroll.shadow"] = "#00000055",
            ["editorWidget.background"] = lato, ["editorWidget.border"] = bordo, ["editorWidget.foreground"] = testo,
            ["editorSuggestWidget.background"] = lato, ["editorSuggestWidget.border"] = bordo, ["editorSuggestWidget.foreground"] = tenue,
            ["editorSuggestWidget.selectedBackground"] = sel, ["editorSuggestWidget.selectedForeground"] = testo,
            ["editorSuggestWidget.highlightForeground"] = acc, ["editorSuggestWidget.focusHighlightForeground"] = acc,
            ["editorHoverWidget.background"] = lato, ["editorHoverWidget.border"] = bordo,
            ["peekView.border"] = A(s.Ghiaccio, .33), ["peekViewEditor.background"] = terminale, ["peekViewResult.background"] = lato,
            ["peekViewTitle.background"] = barra, ["peekViewEditor.matchHighlightBackground"] = A(s.Accento, .2),
            ["peekViewResult.matchHighlightBackground"] = A(s.Accento, .2), ["peekViewResult.selectionBackground"] = sel,
            ["diffEditor.insertedTextBackground"] = A(s.Accento, .12), ["diffEditor.removedTextBackground"] = A(s.Rosso, .12),
            ["diffEditor.insertedLineBackground"] = A(s.Accento, .07), ["diffEditor.removedLineBackground"] = A(s.Rosso, .07),
            ["minimap.background"] = fondo, ["minimap.selectionHighlight"] = A(s.Ghiaccio, .33), ["minimap.findMatchHighlight"] = A(s.Accento, .53),
            ["minimapSlider.background"] = A(s.Vetro, .07), ["minimapSlider.hoverBackground"] = A(s.Vetro, .12), ["minimapSlider.activeBackground"] = A(s.Vetro, .17),
            ["scrollbar.shadow"] = "#00000000", ["scrollbarSlider.background"] = A(s.Vetro, .1), ["scrollbarSlider.hoverBackground"] = A(s.Vetro, .18),
            ["scrollbarSlider.activeBackground"] = A(s.Vetro, .25),

            ["input.background"] = alto, ["input.foreground"] = testo, ["input.border"] = bordo, ["input.placeholderForeground"] = spento,
            ["inputOption.activeBackground"] = A(s.Accento, .15), ["inputOption.activeBorder"] = A(s.Accento, .5), ["inputOption.activeForeground"] = testo,
            ["inputValidation.errorBackground"] = lato, ["inputValidation.errorBorder"] = rs,
            ["inputValidation.warningBackground"] = lato, ["inputValidation.warningBorder"] = ar,
            ["inputValidation.infoBackground"] = lato, ["inputValidation.infoBorder"] = az,
            ["dropdown.background"] = alto, ["dropdown.border"] = bordo, ["dropdown.foreground"] = testo, ["dropdown.listBackground"] = lato,
            ["checkbox.background"] = alto, ["checkbox.border"] = bordo, ["checkbox.foreground"] = acc,
            ["button.background"] = acc, ["button.foreground"] = fondo, ["button.hoverBackground"] = accSopra, ["button.border"] = "#00000000",
            ["button.secondaryBackground"] = A("#FFFFFF", .08), ["button.secondaryForeground"] = testo, ["button.secondaryHoverBackground"] = A("#FFFFFF", .13),
            ["button.separator"] = A(s.Fondo, .33),
            ["progressBar.background"] = acc, ["keybindingLabel.background"] = A("#FFFFFF", .06), ["keybindingLabel.foreground"] = testo,
            ["keybindingLabel.border"] = bordo, ["keybindingLabel.bottomBorder"] = bordo,
            ["quickInput.background"] = lato, ["quickInput.foreground"] = testo, ["quickInputTitle.background"] = barra,
            ["quickInputList.focusBackground"] = sel, ["quickInputList.focusForeground"] = testo, ["pickerGroup.foreground"] = gh, ["pickerGroup.border"] = bordo,

            ["panel.background"] = lato, ["panel.border"] = bordo, ["panelTitle.activeForeground"] = testo, ["panelTitle.activeBorder"] = acc,
            ["panelTitle.inactiveForeground"] = spento, ["panelSection.border"] = bordo, ["panelSectionHeader.background"] = lato, ["panelInput.border"] = bordo,
            ["terminal.background"] = terminale, ["terminal.foreground"] = testo, ["terminalCursor.foreground"] = acc,
            ["terminal.selectionBackground"] = A(s.Ghiaccio, .25), ["terminal.border"] = bordo, ["terminal.tab.activeBorder"] = acc,

            ["statusBar.background"] = barra, ["statusBar.foreground"] = tenue, ["statusBar.border"] = bordo,
            ["statusBar.noFolderBackground"] = barra, ["statusBar.debuggingBackground"] = ar, ["statusBar.debuggingForeground"] = fondo,
            ["statusBarItem.hoverBackground"] = sopra, ["statusBarItem.activeBackground"] = A("#FFFFFF", .12),
            ["statusBarItem.remoteBackground"] = acc, ["statusBarItem.remoteForeground"] = fondo, ["statusBarItem.remoteHoverBackground"] = accSopra,
            ["statusBarItem.remoteHoverForeground"] = fondo, ["statusBarItem.prominentBackground"] = A("#FFFFFF", .08),
            ["statusBarItem.errorBackground"] = rs, ["statusBarItem.errorForeground"] = fondo,
            ["statusBarItem.warningBackground"] = ar, ["statusBarItem.warningForeground"] = fondo,

            ["notifications.background"] = lato, ["notifications.foreground"] = testo, ["notifications.border"] = bordo,
            ["notificationCenterHeader.background"] = barra, ["notificationToast.border"] = bordo, ["notificationLink.foreground"] = gh,
            ["notificationsInfoIcon.foreground"] = az, ["notificationsWarningIcon.foreground"] = ar, ["notificationsErrorIcon.foreground"] = rs,
            ["banner.background"] = barra, ["banner.foreground"] = testo, ["banner.iconForeground"] = gh,

            ["gitDecoration.addedResourceForeground"] = acc, ["gitDecoration.untrackedResourceForeground"] = A(s.Accento, .8),
            ["gitDecoration.modifiedResourceForeground"] = az, ["gitDecoration.deletedResourceForeground"] = rs,
            ["gitDecoration.conflictingResourceForeground"] = ar, ["gitDecoration.ignoredResourceForeground"] = A(s.Testo, .25),
            ["gitDecoration.renamedResourceForeground"] = va,
            ["welcomePage.tileBackground"] = lato, ["welcomePage.tileHoverBackground"] = alto, ["welcomePage.tileBorder"] = bordo,
            ["walkThrough.embeddedEditorBackground"] = terminale,
            ["debugToolBar.background"] = lato, ["debugToolBar.border"] = bordo,
            ["chat.requestBackground"] = A("#FFFFFF", .03), ["chat.requestBorder"] = bordo, ["chat.slashCommandForeground"] = acc,
            ["chat.avatarBackground"] = alto, ["chat.avatarForeground"] = testo,
        };
        foreach (var (k, v) in Ansi(s)) c["terminal.ansi" + char.ToUpper(k[0]) + k[1..]] = v;

        object T(string[] scope, string fg, string stile = null) => stile == null
            ? new { scope, settings = new { foreground = fg } }
            : new { scope, settings = (object)new { foreground = fg, fontStyle = stile } };
        string param = Stile.Hex(Mescola(Stile.Colore(s.Testo), Stile.Colore(s.Arancio), .18));
        string prop = Stile.Hex(Mescola(Stile.Colore(s.Testo), Stile.Colore(s.Ghiaccio), .3));
        string spazio = Stile.Hex(Mescola(Stile.Colore(s.Testo), Stile.Colore(s.Ghiaccio), .55));
        string punt = Stile.Hex(Mescola(Stile.Colore(s.Testo), Stile.Colore(s.Ghiaccio), .45));
        var tok = new object[]
        {
            T(new[] { "comment", "punctuation.definition.comment" }, H(s.Commenti), "italic"),
            T(new[] { "keyword", "storage", "storage.type", "storage.modifier", "keyword.control" }, gh),
            T(new[] { "keyword.operator", "punctuation" }, punt),
            T(new[] { "string", "string.quoted", "string.template" }, H(s.Stringhe)),
            T(new[] { "constant.character.escape", "string.regexp" }, va),
            T(new[] { "constant.numeric", "constant.language", "constant.language.boolean", "constant", "variable.other.constant", "variable.other.enummember" }, ar),
            T(new[] { "entity.name.function", "support.function", "meta.function-call.generic" }, az),
            T(new[] { "entity.name.type", "entity.name.class", "support.class", "support.type", "entity.other.inherited-class" }, va),
            T(new[] { "entity.name.tag", "meta.tag.sgml" }, gh),
            T(new[] { "entity.other.attribute-name" }, vi, "italic"),
            T(new[] { "variable", "meta.definition.variable" }, testo),
            T(new[] { "variable.parameter" }, param, "italic"),
            T(new[] { "variable.language", "variable.language.this", "variable.language.self" }, ro, "italic"),
            T(new[] { "variable.other.property", "variable.other.object.property", "support.variable.property", "meta.object-literal.key" }, prop),
            T(new[] { "entity.name.namespace", "entity.name.module", "entity.name.type.module" }, spazio),
            T(new[] { "meta.decorator", "entity.name.function.decorator", "punctuation.decorator" }, vi),
            T(new[] { "support.type.property-name.json", "support.type.property-name" }, gh),
            T(new[] { "markup.heading", "entity.name.section" }, acc, "bold"),
            T(new[] { "markup.bold" }, testo, "bold"), T(new[] { "markup.italic" }, testo, "italic"),
            T(new[] { "markup.inline.raw", "markup.fenced_code" }, H(s.Stringhe)), T(new[] { "markup.underline.link" }, az),
            T(new[] { "markup.inserted" }, acc), T(new[] { "markup.deleted" }, rs), T(new[] { "markup.changed" }, az),
            T(new[] { "invalid", "invalid.illegal" }, rs),
        };
        var sem = new Dictionary<string, object>
        {
            ["function"] = az, ["method"] = az, ["class"] = va, ["interface"] = va, ["struct"] = va, ["enum"] = va, ["type"] = va,
            ["typeParameter"] = va, ["namespace"] = spazio, ["parameter"] = new { foreground = param, italic = true }, ["property"] = prop,
            ["enumMember"] = ar, ["variable.readonly"] = ar, ["decorator"] = vi, ["macro"] = vi, ["keyword"] = gh,
        };
        return (c, tok, sem);
    }

    public static System.Windows.Media.Color Mescola(System.Windows.Media.Color a, System.Windows.Media.Color b, double t) =>
        System.Windows.Media.Color.FromRgb((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    // I sedici colori ANSI, dalla tavolozza: le versioni "bright" sono le stesse schiarite.
    public static List<(string, string)> Ansi(Stile s)
    {
        string nero = H(s.Alto), bianco = Stile.Hex(Mescola(Stile.Colore(s.Testo), Stile.Colore(s.Ghiaccio), .3));
        var base_ = new[] { ("black", nero), ("red", H(s.Rosso)), ("green", H(s.Accento)), ("yellow", H(s.Arancio)), ("blue", H(s.Azzurro)),
                            ("magenta", H(s.Viola)), ("cyan", H(s.VerdeAcqua)), ("white", bianco) };
        var r = base_.ToList();
        foreach (var (k, v) in base_)
            r.Add(("bright" + char.ToUpper(k[0]) + k[1..], k == "black" ? H(s.Commenti) : k == "white" ? H(s.Testo) : Chiaro(v, 1.3)));
        return r;
    }

    static string PackageJson() => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["name"] = "tema-ghiaccio", ["displayName"] = NomeTema, ["version"] = "1.0.0", ["publisher"] = "vascend",
        ["description"] = "Tema generato da Vascend Engine.", ["engines"] = new { vscode = "^1.80.0" }, ["categories"] = new[] { "Themes" },
        ["contributes"] = new { themes = new[] { new { label = NomeTema, uiTheme = "vs-dark", path = "./themes/vascend-ghiaccio-color-theme.json" } } },
    }, new JsonSerializerOptions { WriteIndented = true });

    static string CartellaEstensioni => Path.Combine(Casa, ".vscode", "extensions");
    static string Installata => Directory.Exists(CartellaEstensioni)
        ? Directory.GetDirectories(CartellaEstensioni, IdEstensione + "-*").OrderBy(x => x).LastOrDefault() : null;

    static string Codice
    {
        get
        {
            foreach (var p in new[] { Path.Combine(Locale, @"Programs\Microsoft VS Code\bin\code.cmd"), @"C:\Program Files\Microsoft VS Code\bin\code.cmd" })
                if (File.Exists(p)) return p;
            return null;
        }
    }

    public static bool VsCodePresente => Codice != null;

    // Il pacchetto .vsix: uno zip con manifest, tipi e la cartella extension/.
    public static void Vsix(Stile s, string percorso)
    {
        var man = $"""
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata>
    <Identity Language="en-US" Id="tema-ghiaccio" Version="1.0.0" Publisher="vascend" />
    <DisplayName>{NomeTema}</DisplayName>
    <Description xml:space="preserve">Tema generato da Vascend Engine.</Description>
    <Categories>Themes</Categories>
    <Properties><Property Id="Microsoft.VisualStudio.Code.Engine" Value="^1.80.0" /></Properties>
  </Metadata>
  <Installation><InstallationTarget Id="Microsoft.VisualStudio.Code" /></Installation>
  <Dependencies />
  <Assets><Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true" /></Assets>
</PackageManifest>
""";
        var tipi = """<?xml version="1.0" encoding="utf-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension=".json" ContentType="application/json" /><Default Extension=".vsixmanifest" ContentType="text/xml" /></Types>""";
        if (File.Exists(percorso)) File.Delete(percorso);
        using var z = ZipFile.Open(percorso, ZipArchiveMode.Create);
        void Scrivi(string nome, string testo) { using var w = new StreamWriter(z.CreateEntry(nome).Open(), new UTF8Encoding(false)); w.Write(testo); }
        Scrivi("[Content_Types].xml", tipi);
        Scrivi("extension.vsixmanifest", man);
        Scrivi("extension/package.json", PackageJson());
        Scrivi("extension/themes/vascend-ghiaccio-color-theme.json", TemaVsCode(s));
    }

    // Se l'estensione c'è già si riscrive il file del tema (VS Code lo ricarica da solo), altrimenti si installa il .vsix.
    public static string ApplicaVsCode(Stile s)
    {
        var dir = Installata;
        if (dir != null)
        {
            Directory.CreateDirectory(Path.Combine(dir, "themes"));
            File.WriteAllText(Path.Combine(dir, "themes", "vascend-ghiaccio-color-theme.json"), TemaVsCode(s), new UTF8Encoding(false));
        }
        else
        {
            var cmd = Codice ?? throw new Exception("VS Code non trovato");
            var vsix = Path.Combine(Path.GetTempPath(), "tema-ghiaccio-1.0.0.vsix");
            Vsix(s, vsix);
            var p = Process.Start(new ProcessStartInfo(cmd, $"--install-extension \"{vsix}\" --force") { CreateNoWindow = true, UseShellExecute = false });
            p?.WaitForExit(60000);
        }
        ModificaJson(Path.Combine(Roaming, @"Code\User\settings.json"), "settings.json", o => Editor(o, s, s.FontCodice, s.DimEditor, true));
        return dir != null ? "tema aggiornato" : "tema installato";
    }

    // Font e tema nelle impostazioni di un editor della famiglia di VS Code.
    static void Editor(JsonObject o, Stile s, string font, int dim, bool tema)
    {
        var f = $"'{font}', 'Cascadia Code', Consolas, monospace";
        o["editor.fontFamily"] = f;
        o["editor.fontSize"] = dim;
        o["editor.fontLigatures"] = true;
        o["terminal.integrated.fontFamily"] = f;
        o["terminal.integrated.fontSize"] = dim;
        o["chat.editor.fontFamily"] = f;
        if (!tema) return;
        o["workbench.colorTheme"] = NomeTema;
        // VS Code non rilegge il file del tema finché non si ricarica: gli stessi colori nelle impostazioni,
        // solo per questo tema, si vedono subito
        var (c, tok, sem) = Parti(s);
        var chiave = "[" + NomeTema + "]";
        JsonObject Sotto(string nome) { var x = o[nome] as JsonObject ?? new JsonObject(); o[nome] = x; return x; }
        Sotto("workbench.colorCustomizations")[chiave] = JsonSerializer.SerializeToNode(c);
        Sotto("editor.tokenColorCustomizations")[chiave] = new JsonObject { ["textMateRules"] = JsonSerializer.SerializeToNode(tok) };
        Sotto("editor.semanticTokenColorCustomizations")[chiave] = new JsonObject { ["enabled"] = true, ["rules"] = JsonSerializer.SerializeToNode(sem) };
    }

    // ---------- Vascend IDE ----------

    // L'IDE installato e quello di sviluppo: impostazioni e cartella delle estensioni di ciascuno.
    public static readonly (string nome, string impostazioni, string estensioni)[] Ide =
    {
        ("Installato", Path.Combine(Roaming, @"Horizon IDE\User\settings.json"), Path.Combine(Casa, ".horizon-ide", "extensions")),
        ("Sviluppo", Path.Combine(Roaming, @"code-oss-dev\User\settings.json"), Path.Combine(Casa, ".horizon-ide-dev", "extensions")),
    };

    public static bool IdePresente(int i) => Directory.Exists(Ide[i].estensioni) || File.Exists(Ide[i].impostazioni);

    public static bool? LeggiIde(int i, string chiave)
    {
        try
        {
            var f = Ide[i].impostazioni;
            if (!File.Exists(f)) return null;
            var o = JsonNode.Parse(File.ReadAllText(f), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
            return o?[chiave] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
        }
        catch { return null; }
    }

    // L'estensione del tema scritta già spacchettata e annotata in extensions.json, come farebbe l'installazione.
    static void EstensioneIde(Stile s, string cartella)
    {
        var nome = IdEstensione + "-1.0.0";
        var dir = Path.Combine(cartella, nome);
        Directory.CreateDirectory(Path.Combine(dir, "themes"));
        File.WriteAllText(Path.Combine(dir, "package.json"), PackageJson(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "themes", "vascend-ghiaccio-color-theme.json"), TemaVsCode(s), new UTF8Encoding(false));
        var elenco = Path.Combine(cartella, "extensions.json");
        JsonArray a = new();
        try { if (File.Exists(elenco)) a = JsonNode.Parse(File.ReadAllText(elenco)) as JsonArray ?? new JsonArray(); } catch { }
        if (a.Any(x => x?["identifier"]?["id"]?.GetValue<string>() == IdEstensione)) return;
        var unita = char.ToLowerInvariant(dir[0]);
        var percorso = "/" + unita + dir[1..].Replace('\\', '/');
        a.Add(new JsonObject
        {
            ["identifier"] = new JsonObject { ["id"] = IdEstensione },
            ["version"] = "1.0.0",
            ["location"] = new JsonObject
            {
                ["$mid"] = 1, ["fsPath"] = unita + dir[1..], ["_sep"] = 1,
                ["external"] = "file://" + percorso.Replace(":", "%3A"), ["path"] = percorso, ["scheme"] = "file",
            },
            ["relativeLocation"] = nome,
            ["metadata"] = new JsonObject { ["installedTimestamp"] = DateTimeOffset.Now.ToUnixTimeMilliseconds(), ["source"] = "vsix" },
        });
        File.WriteAllText(elenco, a.ToJsonString(), new UTF8Encoding(false));
    }

    public static string ApplicaIde(Stile s)
    {
        var fatti = new List<string>();
        for (int i = 0; i < Ide.Length; i++)
        {
            if (!IdePresente(i) || !(s.IdeDestinazioni?.Contains(Ide[i].nome) ?? true)) continue;
            var (nome, impostazioni, estensioni) = Ide[i];
            if (s.IdeTema) { Directory.CreateDirectory(estensioni); EstensioneIde(s, estensioni); }
            ModificaJson(impostazioni, "IDE " + nome, o =>
            {
                Editor(o, s, string.IsNullOrEmpty(s.IdeFont) ? s.FontCodice : s.IdeFont, s.IdeDim, s.IdeTema);
                if (s.IdePrestazioni is bool p) o["horizon.performanceMode"] = p;
                o["horizon.island.vascend"] = s.IdeIsola;
            });
            fatti.Add(nome.ToLowerInvariant());
        }
        return fatti.Count == 0 ? "nessun IDE trovato" : "IDE " + string.Join(" e ", fatti) + " aggiornato";
    }

    // ---------- Windows Terminal ----------

    static IEnumerable<string> FileTerminale()
    {
        foreach (var pk in new[] { "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe" })
        {
            var f = Path.Combine(Locale, "Packages", pk, @"LocalState\settings.json");
            if (File.Exists(f)) yield return f;
        }
        var nonPacchetto = Path.Combine(Locale, @"Microsoft\Windows Terminal\settings.json");
        if (File.Exists(nonPacchetto)) yield return nonPacchetto;
    }

    public static bool TerminalePresente => FileTerminale().Any();

    public static string ApplicaTerminale(Stile s)
    {
        int n = 0;
        foreach (var f in FileTerminale())
        {
            ModificaJson(f, "terminale", o =>
            {
                var schema = new JsonObject
                {
                    ["name"] = NomeTema, ["background"] = H(s.Fondo), ["foreground"] = H(s.Testo), ["cursorColor"] = H(s.Accento),
                    ["selectionBackground"] = H(s.Ghiaccio),
                };
                foreach (var (k, v) in Ansi(s)) schema[k.Replace("magenta", "purple").Replace("Magenta", "Purple")] = v;
                var tema = new JsonObject
                {
                    ["name"] = NomeTema,
                    ["window"] = new JsonObject { ["applicationTheme"] = "dark", ["useMica"] = false, ["rainbowFrame"] = false, ["frame"] = H(s.Barra) + "FF", ["unfocusedFrame"] = H(s.Barra) + "FF" },
                    ["tabRow"] = new JsonObject { ["background"] = H(s.Barra) + "FF", ["unfocusedBackground"] = H(s.Barra) + "FF" },
                    ["tab"] = new JsonObject { ["background"] = H(s.Fondo) + "FF", ["unfocusedBackground"] = H(s.Barra) + "FF", ["showCloseButton"] = "hover", ["iconStyle"] = "default" },
                };
                o["schemes"] = Sostituisci(o["schemes"] as JsonArray, schema);
                o["themes"] = Sostituisci(o["themes"] as JsonArray, tema);
                o["theme"] = NomeTema;
                var prof = o["profiles"] as JsonObject ?? new JsonObject();
                o["profiles"] = prof;
                var d = prof["defaults"] as JsonObject ?? new JsonObject();
                prof["defaults"] = d;
                d["colorScheme"] = NomeTema;
                d["cursorShape"] = s.Cursore ?? "filledBox";
                d["padding"] = "14, 10, 14, 10";
                d["opacity"] = s.Acrilico;
                d["useAcrylic"] = s.Acrilico < 100;
                d["intenseTextStyle"] = "bright";
                d["scrollbarState"] = "hidden";
                d["font"] = new JsonObject { ["face"] = s.FontDelTerminale(), ["size"] = s.DimTerminale, ["weight"] = "normal" };
            });
            n++;
        }
        return n == 0 ? "Windows Terminal non trovato" : "terminali aggiornati";
    }

    static JsonArray Sostituisci(JsonArray a, JsonObject nuovo)
    {
        var r = new JsonArray();
        if (a != null)
            foreach (var x in a.ToList())
                if (x is JsonObject j && (string)j["name"] != (string)nuovo["name"]) { a.Remove(x); r.Add(x); }
        r.Add(nuovo);
        return r;
    }

    // Legge un JSON con commenti, lo modifica e lo riscrive; la prima volta ne tiene una copia accanto.
    static void ModificaJson(string f, string nome, Action<JsonObject> cambia)
    {
        JsonObject o = new();
        if (File.Exists(f))
        {
            var copia = f + ".prima-vascend-engine.bak";
            if (!File.Exists(copia)) File.Copy(f, copia);
            var testo = File.ReadAllText(f);
            if (testo.Trim().Length > 0)
                o = JsonNode.Parse(testo, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject ?? new JsonObject();
        }
        else Directory.CreateDirectory(Path.GetDirectoryName(f));
        cambia(o);
        File.WriteAllText(f, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));
    }

    // ---------- Barra ----------

    static string EseguibileBarra => Path.Combine(Casa, @".config\barra-vascend\bin\BarraVascend.exe");
    public static bool BarraPresente => File.Exists(EseguibileBarra);

    // La barra legge lo stile all'avvio: si riavvia.
    public static string ApplicaBarra()
    {
        foreach (var p in Process.GetProcessesByName("BarraVascend"))
            try { p.Kill(); p.WaitForExit(5000); } catch { }
        Process.Start(new ProcessStartInfo(EseguibileBarra) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(EseguibileBarra) });
        return "barra riavviata";
    }
}
