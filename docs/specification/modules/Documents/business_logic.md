# ====== [Documents] BUSINESS LOGIC ======

Status: Planned (stage S4)

## Purpose
`Documents` owns:
- Generation of a Word (`.docx`) document from a template with tags, using `DocumentFormat.OpenXml`. Microsoft Word is not needed on the machine.
- Building the default template (`ReportTemplateBuilder`: A4, margins, font, report structure) and rebuilding it on request.
- Replacing tags in the document text, including tags split over several runs (`TagReplacer`).
- Unique output file names, so two reports for the same period do not overwrite each other.

**Not here:**
- Tag values, episode selection, sentence text, file name, output folder, journal: `Reporting`. Documents get a ready `ReportContent` and a target path.
- Unit data for the template (classification, addressee, resolution, font): values live in `Settings`.
- PDF output (a later Could requirement) and opening the file in another program (UI).

## Entities
No persistent entities.

| Class | Basic Fields | Description | Invariants |
|---|---|---|---|
| `DocxReportRenderer` *(implements `IReportRenderer`)* | `Format = Word`, `TemplatePath` | Copies the template, replaces tags, saves. | - The template is never changed, only its copy.<br>- An existing target file is not overwritten (`FileMode.CreateNew`).<br>- A template without a body gives `ReportException`.<br>- The result is written to a temporary file and renamed atomically, so no half-finished file stays. |
| `ReportTemplateBuilder` | `AppSettings` | Builds the `.docx` template from scratch. | - A4, margins 30/10/20/20 mm, line spacing 1.5.<br>- Font and size from `AppSettings` (default Times New Roman, 14).<br>- Stores `TemplateVersion` as a document property. |
| `TagReplacer` *(static)* | - | Replaces tags in paragraphs and body. | - See policies. |

Default template: classification (right, bold), addressee, resolution with rank and name, title "REPORT", the main sentence with tags, then position, rank and name of the signer. Classification, addressee, resolution and font are baked into the template, so changing them needs a rebuild. `{{ПІДРОЗДІЛ}}` and `{{НАКАЗ}}` are filled at generation time.

## Generation flow
`DocxReportRenderer.RenderAsync(content, outputPath, ct)`:
1. If the template does not exist, create the default one. A template version different from the code version gives a warning to the UI, the document is still generated.
2. The target path is already reserved by `Reporting`. If the name exists, a suffix ` (2)` ... ` (999)` is added, then a time suffix. Uniqueness comes from trying `CreateNew`, not from checking existence first.
3. Copy the template to a temporary file in the target folder, replace tags in the body and in all headers and footers, save.
4. Rename to the final name and return the path. Any exception deletes the temporary file.

Rendering runs outside the UI thread and checks the `CancellationToken` between steps.

## Value Objects
None. Input is `ReportContent` (a map from tag to value).

## Domain Policies
| Domain Policy | Description |
|---|---|
| Tags split over runs | Word may cut `{{ПІБ}}` into several runs. The replacer maps characters to runs, writes the value into the first run of the range and removes only the tag characters from the next runs. Formatting of other runs stays. |
| Single pass | All tags of a paragraph are replaced by the original text in one pass. A value containing `{{...}}` does not become a tag. `null` becomes an empty string. |
| Scope | Only paragraphs with known tags are changed. Body, tables, headers, footers, hyperlinks and content controls are covered. Tags inside complex Word fields are not supported (known limit). |
| Multiline values | Lines are split with soft breaks inside one paragraph. |
| Unique file name | Suffix ` (N)` from 2 to 999, then a time suffix, creation is atomic. |
| Template version and auto-create | A missing template is created with defaults. A version mismatch is a warning, not an error. |
| Template rebuild | The existing file is backed up first (`template.<time>.bak.docx`) after a UI warning about manual edits, then a new one is built from saved settings. |

## Domain Services
- `DocxReportRenderer`: `RenderAsync`, `EnsureTemplateExists`, `RebuildTemplate`.
- `ReportTemplateBuilder`: `Build(path)`. `TagReplacer`: `ReplaceInBody`, `ReplaceInParagraph`.

## Application Commands & Queries
No separate Application layer. The module is called from `ReportService` through `IReportRenderer` and from the Settings tab.
- Commands: `IReportRenderer.RenderAsync`, `RebuildTemplate()` (button in Settings, saves settings first), `EnsureTemplateExists()` at startup.
- Queries: `TemplatePath` (`%AppData%\Рапорти\template.docx`) for the "Open template" button.

## Infrastructure
- Library `DocumentFormat.OpenXml`, version pinned in `Directory.Packages.props`. `Reports.Documents` depends only on `Reports.Core`.
- Template in `%AppData%\Рапорти\template.docx`, output files in the folder from `IOutputFolderProvider`, file writing and deleting through `IReportFileStore`.
- `DocxReportRenderer` is registered as `IReportRenderer` in the DI container. The "template rebuilt" event goes to `IAuditSink` from S6.
