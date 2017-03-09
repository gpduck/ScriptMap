# ScriptMap

Combines individual PowerShell scripts into a singe file and generates a script map that maps lines
from the single file back to each individual file location.  Also includes a function to load the
combined file (and map) and rewrite the extents to make all the functions look like they came from
the original files/locations.  This means any errors will show the original source location instead
of the location from the combined file.

## Example

Suppose you are in a module folder that has a subfolder `ExportedFunctions` with an individual file
for each function in the module.  You can run the following commands to generate a `ExportedFunctions.ps1`
and `ExportedFunctions.ps1.map` file in the root of the module:

```
Import-Moduse .\ScriptMap
Join-ModuleScripts -FunctionPath .\ExportedFunctions -OutPath .\
```

Your folder structure will look like this:

```
\Module.psm1
\Module.psd1
\ExportedFunctions\*.ps1
\ExportedFunctions.ps1
\ExportedFunctions.ps1.map
```

In your Module.psm1 file, you can now load everything from the `ExportedFunctions.ps1` file using the following:

```
$Loader = Get-ModuleScriptLoader $PSScriptRoot\ExportedFunctions.ps1
. $Loader
```
