namespace FrameAnimator.Core.Cli
{
    using System;
    using System.Collections.Generic;

    /// <summary>Textes d'aide (<c>/?</c>) en anglais et en français ; la langue du système choisit, l'anglais est le repli.</summary>
    internal static class HelpText
    {
        private const String EnglishGeneral = """
        Frame Animator - console mode {version}
        Converts Fallout FRM files, runs FAS scripts and processes batches, without any window.
        Without arguments (or with a file to open) the program opens its window as usual.

        Usage:
          {exe} <command> [arguments] [options]
          {exe} <command> /?                 help about a command

        Commands:
          convert    Convert FPR / FRM / FR0-FR5 files to another format
          image      Convert BMP / PNG / GIF images to another image format
          script     Execute a FAS script (File > Execute Script)
          automate   Batch processing (File > Automate)
          info       Show the content of an FRM file or of an FPR project
          decode     Diagnostic: decode a BMP / PNG / GIF image into a 24-bit BMP
          encode     Diagnostic: encode a BMP image into PNG, GIF or BMP

        Options start with /, - or --; a value follows ':' or '=' or a space (/to:frm  --to=frm  -to frm).
        Help: /?  -?  -h  --help  help

        Exit codes: 0 success - 1 invalid command line - 2 unexpected error - 3 an operation failed.
        The program is a windowed application: cmd.exe does not wait for it. To wait for the end and get the exit code,
        use  start /wait {exe} ...  (cmd) or  Start-Process -Wait -NoNewWindow {exe} -ArgumentList ...  (PowerShell),
        or pipe the output:  {exe} ... | more
        """;

        private const String FrenchGeneral = """
        Frame Animator - mode console {version}
        Convertit des fichiers FRM de Fallout, exécute des scripts FAS et traite des lots, sans aucune fenêtre.
        Sans argument (ou avec un fichier à ouvrir) le programme ouvre sa fenêtre comme d'habitude.

        Utilisation :
          {exe} <commande> [arguments] [options]
          {exe} <commande> /?                aide d'une commande

        Commandes :
          convert    Convertir des fichiers FPR / FRM / FR0-FR5 vers un autre format
          image      Convertir des images BMP / PNG / GIF vers un autre format d'image
          script     Exécuter un script FAS (Fichier > Exécuter un script)
          automate   Traitement par lots (Fichier > Automatiser)
          info       Afficher le contenu d'un fichier FRM ou d'un projet FPR
          decode     Diagnostic : décoder une image BMP / PNG / GIF en BMP 24 bits
          encode     Diagnostic : encoder une image BMP en PNG, GIF ou BMP

        Une option commence par /, - ou -- ; sa valeur suit après ':' ou '=' ou un espace (/to:frm  --to=frm  -to frm).
        Aide : /?  -?  -h  --help  help

        Codes de sortie : 0 succès - 1 ligne de commande invalide - 2 erreur inattendue - 3 une opération a échoué.
        Le programme est une application fenêtrée : cmd.exe ne l'attend pas. Pour attendre la fin et récupérer le code de sortie,
        utilisez  start /wait {exe} ...  (cmd) ou  Start-Process -Wait -NoNewWindow {exe} -ArgumentList ...  (PowerShell),
        ou redirigez la sortie :  {exe} ... | more
        """;

        private const String EnglishConvert = """
        {exe} convert <source>... /to:<fpr|frm|fr> [options]

        Converts FPR projects, FRM files and FR0-FR5 sets to another format. The sources may use the wildcards * and ?.

          <source>...             files to convert (.fpr, .frm, .fr0 to .fr5)
          /to:<format>            fpr (project), frm, or fr (FR0-FR5 files)
          /out:<folder|mask>      where to write: a folder, or a mask such as C:\out\*.frm
                                  (default: next to each source)
          /palette:<file>         palette (.act, .pal) for FRM sources
                                  (default: the .pal next to the FRM, else Default.act)
          /frames:<BMP|PNG|GIF>   format of the frame images written with a project (default BMP)
          /continue               go on after an error (default: stop at the first one)
          /quiet                  only print errors

        Examples:
          {exe} convert hero.frm /to:fpr
          {exe} convert C:\fo\*.frm /to:fpr /out:C:\work /frames:PNG
          {exe} convert walk.fpr /to:fr /out:C:\out\
        """;

        private const String FrenchConvert = """
        {exe} convert <source>... /to:<fpr|frm|fr> [options]

        Convertit des projets FPR, des fichiers FRM et des jeux FR0-FR5 vers un autre format. Les sources peuvent contenir les jokers * et ?.

          <source>...             fichiers à convertir (.fpr, .frm, .fr0 à .fr5)
          /to:<format>            fpr (projet), frm, ou fr (fichiers FR0-FR5)
          /out:<dossier|masque>   destination : un dossier, ou un masque comme C:\out\*.frm
                                  (défaut : à côté de chaque source)
          /palette:<fichier>      palette (.act, .pal) pour les sources FRM
                                  (défaut : le .pal à côté du FRM, sinon Default.act)
          /frames:<BMP|PNG|GIF>   format des images de frames écrites avec un projet (défaut BMP)
          /continue               poursuivre après une erreur (défaut : s'arrêter à la première)
          /quiet                  n'afficher que les erreurs

        Exemples :
          {exe} convert hero.frm /to:fpr
          {exe} convert C:\fo\*.frm /to:fpr /out:C:\work /frames:PNG
          {exe} convert walk.fpr /to:fr /out:C:\out\
        """;

        private const String EnglishImage = """
        {exe} image <source>... /to:<bmp|png|gif> [options]

        Converts BMP, PNG and GIF images to another image format. The sources may use the wildcards * and ?.
        An animated GIF gives one file per image (name_0, name_1...).

          <source>...             images to convert (.bmp, .png, .gif)
          /to:<format>            bmp, png or gif
          /out:<folder|mask>      where to write: a folder, or a mask such as C:\out\*.png or *_copy.png
                                  (default: next to each source, same name with the new extension)
          /palette:<file>         palette (.act, .pal) used to reduce the colors of a GIF (default: Default.act)
          /continue               go on after an error (default: stop at the first one)
          /quiet                  only print errors

        Examples:
          {exe} image hero.bmp /to:png
          {exe} image C:\shots\*.png /to:bmp /out:C:\work\
          {exe} image walk.gif /to:png /out:C:\work\frame_*.png
        """;

        private const String EnglishScript = """
        {exe} script <file.fas> [options]

        Executes a FAS script on an empty project, like File > Execute Script. The relative paths of the script
        are resolved from the folder of the script.

          <file.fas>              the script to execute
          /open:<file>            project (.fpr) or FRM (.frm, .fr0 to .fr5) to open before the script runs
          /palette:<file>         palette to load first (used by an FRM given with /open)
          /frames:<BMP|PNG|GIF>   format of the frame images written when a project made from an FRM is saved
                                  (default BMP)
          /continue               go on after an error in the script (default: stop at the first one)
          /quiet                  only print errors

        Examples:
          {exe} script Sample.fas
          {exe} script fix.fas /open:hero.frm /palette:fo.pal
        """;

        private const String FrenchImage = """
        {exe} image <source>... /to:<bmp|png|gif> [options]

        Convertit des images BMP, PNG et GIF vers un autre format d'image. Les sources peuvent contenir les jokers * et ?.
        Un GIF animé donne un fichier par image (nom_0, nom_1...).

          <source>...             images à convertir (.bmp, .png, .gif)
          /to:<format>            bmp, png ou gif
          /out:<dossier|masque>   destination : un dossier, ou un masque comme C:\out\*.png ou *_copie.png
                                  (défaut : à côté de chaque source, même nom avec la nouvelle extension)
          /palette:<fichier>      palette (.act, .pal) qui réduit les couleurs d'un GIF (défaut : Default.act)
          /continue               poursuivre après une erreur (défaut : s'arrêter à la première)
          /quiet                  n'afficher que les erreurs

        Exemples :
          {exe} image hero.bmp /to:png
          {exe} image C:\shots\*.png /to:bmp /out:C:\work\
          {exe} image walk.gif /to:png /out:C:\work\frame_*.png
        """;

        private const String FrenchScript = """
        {exe} script <fichier.fas> [options]

        Exécute un script FAS sur un projet vide, comme Fichier > Exécuter un script. Les chemins relatifs du script
        sont résolus à partir du dossier du script.

          <fichier.fas>           le script à exécuter
          /open:<fichier>         projet (.fpr) ou FRM (.frm, .fr0 à .fr5) à ouvrir avant l'exécution du script
          /palette:<fichier>      palette à charger d'abord (utilisée par un FRM donné avec /open)
          /frames:<BMP|PNG|GIF>   format des images de frames écrites quand on enregistre un projet issu d'un FRM
                                  (défaut BMP)
          /continue               poursuivre après une erreur du script (défaut : s'arrêter à la première)
          /quiet                  n'afficher que les erreurs

        Exemples :
          {exe} script Sample.fas
          {exe} script fix.fas /open:hero.frm /palette:fo.pal
        """;

        private const String EnglishAutomate = """
        {exe} automate <source>... /dest:<mask> [options]

        Same as File > Automate: opens each source, optionally applies a palette and a script, then saves it under the
        destination mask (the wildcards * and ? stand for the name of the source).

          <source>...             sources (.fpr, .frm, .fr0 to .fr5), wildcards allowed
          /source:<file>          another way to give a source; the option can be repeated
          /dest:<mask>            destination mask such as *.frm or C:\out\*.fpr; its extension (.fpr, .frm, .fr0)
                                  chooses the format
          /format:<fpr|frm|fr>    force the destination format (replaces the extension of the mask, like the dialog)
          /palette:<file>         palette used for FRM sources
          /script:<file>          FAS script executed on each source before saving
          /frames:<BMP|PNG|GIF>   format of the frame images written with a project (default BMP)
          /continue               go on after an error (default: stop at the first one)
          /quiet                  only print errors

        Examples:
          {exe} automate a.frm b.frm /dest:C:\out\*.fpr
          {exe} automate C:\fo\*.fpr /dest:*.frm /script:prepare.fas
        """;

        private const String FrenchAutomate = """
        {exe} automate <source>... /dest:<masque> [options]

        Comme Fichier > Automatiser : ouvre chaque source, applique éventuellement une palette et un script, puis
        l'enregistre sous le masque de destination (les jokers * et ? reprennent le nom de la source).

          <source>...             sources (.fpr, .frm, .fr0 à .fr5), jokers autorisés
          /source:<fichier>       autre façon de donner une source ; l'option peut être répétée
          /dest:<masque>          masque de destination comme *.frm ou C:\out\*.fpr ; son extension (.fpr, .frm, .fr0)
                                  choisit le format
          /format:<fpr|frm|fr>    imposer le format de destination (remplace l'extension du masque, comme la boîte)
          /palette:<fichier>      palette utilisée pour les sources FRM
          /script:<fichier>       script FAS exécuté sur chaque source avant l'enregistrement
          /frames:<BMP|PNG|GIF>   format des images de frames écrites avec un projet (défaut BMP)
          /continue               poursuivre après une erreur (défaut : s'arrêter à la première)
          /quiet                  n'afficher que les erreurs

        Exemples :
          {exe} automate a.frm b.frm /dest:C:\out\*.fpr
          {exe} automate C:\fo\*.fpr /dest:*.frm /script:prepare.fas
        """;

        private const String EnglishInfo = """
        {exe} info <file.frm|file.fpr>

        Shows the content of an FRM file (header) or of an FPR project (settings, directions, frame counts).
        """;

        private const String FrenchInfo = """
        {exe} info <fichier.frm|fichier.fpr>

        Affiche le contenu d'un fichier FRM (en-tête) ou d'un projet FPR (réglages, directions, nombre de frames).
        """;

        private const String EnglishDecode = """
        {exe} decode <image.bmp|png|gif> <out.bmp>

        Diagnostic: decodes an image and writes it as a 24-bit BMP (one file per sub-image for an animated GIF).
        """;

        private const String FrenchDecode = """
        {exe} decode <image.bmp|png|gif> <out.bmp>

        Diagnostic : décode une image et l'écrit en BMP 24 bits (un fichier par sous-image pour un GIF animé).
        """;

        private const String EnglishEncode = """
        {exe} encode <in.bmp> <out.png|out.gif|out.bmp>

        Diagnostic: encodes a BMP image as PNG, GIF (default palette) or BMP.
        """;

        private const String FrenchEncode = """
        {exe} encode <in.bmp> <out.png|out.gif|out.bmp>

        Diagnostic : encode une image BMP en PNG, GIF (palette par défaut) ou BMP.
        """;

        private static readonly Dictionary<String, (String English, String French)> Topics = new()
        {
            [""] = (EnglishGeneral, FrenchGeneral),
            ["convert"] = (EnglishConvert, FrenchConvert),
            ["image"] = (EnglishImage, FrenchImage),
            ["script"] = (EnglishScript, FrenchScript),
            ["automate"] = (EnglishAutomate, FrenchAutomate),
            ["info"] = (EnglishInfo, FrenchInfo),
            ["decode"] = (EnglishDecode, FrenchDecode),
            ["encode"] = (EnglishEncode, FrenchEncode),
        };

        /// <summary>Aide d'une commande (chaîne vide : aide générale). Retourne l'aide générale pour une commande inconnue.</summary>
        public static String Get(String command, String language, String exe, String version)
        {
            if (!Topics.TryGetValue(command, out (String English, String French) topic))
                topic = Topics[""];
            String text = language == "fr" ? topic.French : topic.English;
            return text.Replace("{exe}", exe).Replace("{version}", version);
        }
    }
}
