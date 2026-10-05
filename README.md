# Family Report for Revit 2023

## Project layout

| Folder | Purpose |
| --- | --- |
| `Commands` | Revit entry points: export a folder or open an existing report |
| `Models/Report` | JSON report and parameter value data |
| `Models/Transfer` | Options selected in step 3 |
| `Services/Analysis` | Open, analyze, check and export `.rfa` files |
| `Services/Transfer` | Validate and write a parameter to the open family |
| `UI/Forms` | Three report and transfer screens |
| `UI/Theme` | Shared light red WinForms styling |

## Workflow

1. Run `BatchFamilyHealthCommand` to analyze a folder of `.rfa` files. It writes `FamilyHealthReport/summary.json` and opens the report UI. To read an existing report, run `ShowFamilyReportCommand`.
2. Select a family, then choose a parameter and press **Transfer**.
3. Compare the source parameter with the destination preview. Turn on **Change format** to edit destination group, scope, display unit, precision, and symbol. Turn on **Copy value** to choose a source Type, review its value, and choose a destination Type. Turn on **Copy formula** to review or edit the source formula before transfer. The destination is the family currently open in Revit.

The parameter definition is always copied. With **Change format** off, the source group and scope are retained and the destination family's existing unit format remains in use. Changing the display unit, precision, or symbol affects other parameters of the same data type in the destination family. **Copy value** can write a selected source Type's raw value to a selected destination Type, or copy values to destination Types with matching names. If either family has no Type value to use, the new parameter remains unset. **Copy formula** checks referenced source parameter names against the destination and then asks Revit to assign the formula. It requires a destination Type. Copy formula and Copy value cannot be selected together because the formula controls the value. Element references are not copied.

Older JSON reports can still be viewed and used for definition transfer when their data type is present. To review source format and use **Copy value**, export a new report containing the format and per-Type value fields. Shared parameters require the matching GUID in Revit's configured shared parameter file.

Build with `dotnet build Export_Data_From_Family.csproj -p:Configuration=Debug`. Load the resulting DLL into Revit 2023 using your add-in registration or Add-In Manager.
