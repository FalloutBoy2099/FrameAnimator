# Frame Animator — .NET 10 rewrite

A rewrite in .NET 10 / WPF of **Frame Animator 2.70.1** by Jochua, the tool that builds Fallout FRM files from BMP, GIF and PNG
images (single frames, animations, six directions, FR0–FR5 sets). The FRM files it produces are **byte-for-byte identical** to
those of the original program.

There is **one executable**: `FrameAnimator.exe` (needs the .NET 10 Desktop Runtime) or `FrameAnimator-NET.exe` (the .NET runtime is
built in, nothing to install). Started without arguments it opens its window; started with a command line it works as a
[console tool](#console-mode).

## How it was made: reverse engineering

The original program (a 2011 Borland C++Builder / VCL application) was reverse engineered rather than guessed:

* **Static analysis** of the executable: PE sections and resources, the form definitions (DFM) of the five windows, the published
  methods, and an annotated disassembly of the application code. This gave the data model, the file formats (FRM, FPR projects, FAS
  scripts, ACT / PAL palettes), the algorithms (transparent color, auto-cutting, color indexing, offsets and positions, name masks)
  and the exact behavior and messages of the interface.
* **Dynamic validation**: the original executable was driven programmatically through the Win32 API (toolbar buttons, menu entries,
  file dialogs) to produce reference files from controlled inputs. The new code is compared with them in the test suite — FRM, FR0–FR5,
  BMP and project files are identical byte for byte; PNG output is identical pixel for pixel.
* **Quirks are reproduced on purpose.** The original has a bug in its nearest-color search for colors that are not in the palette
  (color differences are converted to unsigned integers before use, so the "Dithering Method" is really a distance metric distorted by
  that bug). The rewrite reproduces it by default to stay byte-identical; set `project.Settings.LegacyColorMatching = false` for a
  mathematically correct quantization.

The results of the analysis (formats, algorithms, behaviors, validation scenarios) are written down in a reverse-engineering
specification that the code follows.

## Structure

```
FrameAnimator.sln
├─ FrameAnimator.Core/            dependency-free library (net10.0)
│  ├─ Model/                      Project, Direction, Frame, Palette, Bitmap32, settings, undo stack
│  ├─ Formats/                    FRM / FR0-FR5 (FrmFormat), file name masks, paths, ANSI text
│  ├─ Imaging/                    BMP, PNG and GIF codecs (full reading, writing)
│  ├─ Geometry/                   positions and offsets (FrameMath), color search (ColorIndexer)
│  ├─ Services/                   ProjectService (open / save), ProjectEditor (editing), IUserInteraction
│  ├─ Scripting/                  FAS scripts (ScriptRunner) and batch processing (AutomationRunner)
│  ├─ Cli/                        console mode: command-line parsing, /? help, commands
│  └─ Default.act                 default palette (embedded resource)
├─ FrameAnimator.App/             WPF Fluent user interface (net10.0-windows) — the executable project
│  ├─ Program.cs                  entry point: console mode or window
│  ├─ App.xaml / Themes/          application, compact styles, compact menu
│  ├─ ViewModels/                 MVVM view models (CommunityToolkit.Mvvm): MainViewModel and one view model per dialog
│  ├─ Controls/                   FrameCanvas (drawing area: background, hexes, ghost, frame, zoom)
│  ├─ Views/                      main window, dialogs (thin XAML views), dialog service / host, theme, window memory
│  ├─ Localization/               Localizer + JSON dictionaries (11 languages)
│  ├─ Infrastructure/             messenger messages, dialog contracts, preferences, console host, file dialog memory
│  └─ Assets/                     application icon, hexagon tile
├─ FrameAnimator.Core.Tests/      xUnit tests of the Core and of the console mode
│  └─ Golden/                     reference files produced by the original executable
├─ FrameAnimator.App.Tests/       xUnit tests of the view models (net10.0-windows, no window is created)
├─ Publish.ps1                    publishes the two executables to publish/
└─ publish/                       FrameAnimator.exe, FrameAnimator-NET.exe (generated)
```

| Project | Role | Status |
|---|---|---|
| `FrameAnimator.Core` | model, FRM / FPR / FAS / ACT / PAL formats, BMP / PNG / GIF codecs, color quantization, FAS scripts, batch processing, console mode — **no dependencies** | complete |
| `FrameAnimator.App` | **WPF Fluent** interface (native .NET 10 theme, light / dark): frame list, hexagonal drawing area with zoom, Move / Pick tools, Offset / Options / Automate / About / color dialogs, file picker | complete |
| `FrameAnimator.Core.Tests` | 136 xUnit tests, including 12 **golden** tests that compare the output byte for byte with the original executable's, and the console mode tests | passing |
| `FrameAnimator.App.Tests` | 112 xUnit tests of the view models (main window and every dialog), with a fake dialog service | passing |

## Build, test, publish

```
dotnet build FrameAnimator.sln
dotnet test FrameAnimator.sln
dotnet run --project FrameAnimator.App [project.fpr | file.frm]         # graphical interface
dotnet run --project FrameAnimator.App -- /?                            # console mode help
.\Publish.ps1                                                           # publish\FrameAnimator.exe and FrameAnimator-NET.exe
```

`Publish.ps1` builds two single-file executables: `FrameAnimator.exe` (about 0.5 MB, framework-dependent) and `FrameAnimator-NET.exe`
(about 59 MB, .NET runtime included). Options: `-Configuration Release|Debug`, `-Runtime win-x64|win-x86|win-arm64`. The previous
`publish` folder is replaced only when both builds succeed.

## Graphical interface

* **Window**: title bar drawn in the application theme (menus, title, *Theme* and *Language* buttons, system buttons), toolbar with
  vector icons, frame list, drawing area (zoom 1×–8× with `Ctrl +/−/0` or `Ctrl`+wheel), status bar.
* **Theme**: automatic (follows Windows), light or dark. **Language**: English, Czech, German, Spanish, French, Italian, Polish,
  Portuguese, Russian, Swedish, Traditional Chinese (the Windows language is detected on first start). Texts are translated through
  JSON dictionaries (`FrameAnimator.App/Localization/*.json`; the English text is the key, and a missing translation is shown in
  English). Error messages coming from the Core stay in English.
* **Open / save file dialogs** are the application's own (themed): the last folder is remembered separately for opening and for
  saving, **favorite** folders (star next to the path, context menu), new folder, `F5` to refresh. *View > System dialogs* switches
  to the Windows ones (files and messages).
* **Position and size**: *View > Save position* / *Save size* remember those of all windows (except message boxes).
* Preferences (theme, language, last folders, favorites, window options) are stored in `%LOCALAPPDATA%\FrameAnimator\settings.txt`.
* Additions to the original: zoom, built-in RGB color picker, themes, languages, favorites, console mode.
* Development switches: `--snapshot out.png` opens the file, saves the window as a PNG and exits (with `--dialog
  options|automate|about|color|offset|open`, `--theme dark|light`, `--lang fr`, `--system-dialogs`).

## Console mode

With a command (`convert`, `image`, `script`, `automate`, `info`, `decode`, `encode`) or `/?`, the same executable works without a
window and writes to the console that started it (or to the redirected output). `/?` (also `-?`, `-h`, `--help`, `help`) shows the
help, and `<command> /?` the help of a command (in French or English according to the language). An option starts with `/`, `-` or
`--`; its value follows after `:`, `=` or a space (`/to:frm`, `--to=frm`, `-to frm`). The wildcards `*` and `?` in sources are
expanded by the program.

```
FrameAnimator convert hero.frm /to:fpr                              # FRM -> project (frame images as BMP)
FrameAnimator convert C:\fo\*.frm /to:fpr /out:C:\work /frames:PNG  # a folder of FRM files
FrameAnimator convert walk.fpr /to:fr /out:C:\out\                   # project -> FR0-FR5
FrameAnimator image hero.bmp /to:png                                    # BMP / PNG / GIF image conversion
FrameAnimator image walk.gif /to:png /out:frames/                       # animated GIF -> walk_0.png, walk_1.png...
FrameAnimator script Sample.fas /open:hero.frm /palette:fo.pal      # File > Execute Script
FrameAnimator automate a.frm b.frm /dest:C:\out\*.fpr /script:prepare.fas /palette:fo.pal   # File > Automate
```

* `convert`: plain FPR / FRM / FR0–FR5 conversion to `fpr`, `frm` or `fr` (`/to`; destination `/out`: a folder or a mask).
* `image`: converts BMP / PNG / GIF images to `bmp`, `png` or `gif` (`/to`; destination `/out`: a folder or a mask such as
  `*_copy.png`; `/palette` to reduce the colors of a GIF). An animated GIF gives one file per image; a result never replaces its own
  source.
* `script`: runs a FAS script on an empty project, or on the file given by `/open` (`/palette` for an FRM). `run` is an alias.
* `automate`: exactly the *Automate* dialog (`/dest` mask, `/format`, `/palette`, `/script`, `/frames BMP|PNG|GIF`, sources as
  arguments or with `/source`).
* `info`, `decode`, `encode`: diagnostic tools (content of an FRM or a project, image decoding / encoding).
* By default processing **stops at the first error**; `/continue` (alias `/yes`) goes on with the other files; `/quiet` only prints
  errors.
* Exit codes: `0` success, `1` invalid command line, `2` unexpected error, `3` an operation failed.
* The executable is a windowed application, so `cmd.exe` does not wait for it. To wait for the end and read the exit code use
  `start /wait FrameAnimator convert ...` (cmd), `Start-Process -Wait -NoNewWindow ...` (PowerShell), or redirect the output
  (`FrameAnimator ... | more`). Started without a console (Explorer, task scheduler), help and syntax errors are shown in a message
  box; a processing run never blocks.
* A quoted value must not end with a `\` (`/out:"C:\folder\"` is misread by Windows); the program repairs that case when the argument
  is the last one on the line.

## Architecture of the interface (MVVM)

The interface follows MVVM with [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/):

* **View models** (`ViewModels/`) derive from `ObservableObject`; properties are generated with `[ObservableProperty]` (partial
  properties) and commands with `[RelayCommand]` (with `CanExecute`). Commands do not poll WPF, so a view model re-evaluates them
  explicitly when its state changes (`NotifyCanExecuteChanged`). `MainViewModel` holds the project, the selection, the tools and all
  menu / toolbar commands; each modal dialog has its own view model deriving from `DialogViewModelBase`, which asks for the dialog to
  close with `RequestClose(accepted)` and exposes its result.
* **Views** (`Views/`, XAML) are thin: bindings and commands, no logic. The remaining code-behind is pure view code (title bar
  buttons, forwarding the list selection, focusing a text box). `FrameCanvas` is a custom control that draws the project.
* **Services**: `IDialogService` (files, dialogs) and `IMessageService` (messages, questions) are the only way a view model reaches
  the UI; `DialogService` / `MessageService` implement them with `DialogHost.ShowModal`, which binds a window to its view model and
  closes it when the view model asks. The tests replace them with `FakeDialogService`.
* **Messenger**: the language and theme changes are broadcast with `WeakReferenceMessenger` (`LanguageChangedMessage`,
  `ThemeChangedMessage`); the view models that show computed texts or theme colors react to them.

## Notes

* **Fidelity to the original**: the generated FRM files are byte-identical to those of the original FrameAnimator.exe, including the
  nearest-color bug described above (disable it with `project.Settings.LegacyColorMatching = false`).
* `ProjectService` depends on no user interface: every message and question goes through `IUserInteraction` (`ShowError`, `Confirm`,
  `PromptSaveFramesAs`, `PromptPalette`, `Progress`, `CancelRequested`). The application and the console mode each provide an
  implementation.
* `ProjectEditor` holds the editing logic of the interface (*Frames Offset* / *Frame Offset* / *First Frame Offset* dialogs, offset
  wizard, mouse moves with the Shift constraint, 10-level undo) so that a user interface only has to call it.
* `ProjectService.AppDirectory` is the folder where `Default.act` is looked for (like the original's executable folder); failing that,
  the embedded palette (identical) is used. `ProjectService.ConfirmPaletteOnOpen` (true by default, like the original) asks "Use
  palette ...?" when an FRM without a neighbouring `.PAL` is opened; the application and the console mode turn it off.
* An FRM file shorter than its header (62 bytes), or with a frame larger than 16 million pixels, is rejected as corrupt.
* FAS scripts ignore blank lines here (the original reports them as `Unknown keyword ''`).
