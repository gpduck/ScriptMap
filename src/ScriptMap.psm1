# Implement your module commands in this script.

function Join-ModuleScripts {
	param(
		$FunctionPath,
		$Outfile = "ExportedFunctions.ps1",
		$OutPath = $FunctionPath
	)
	$MapFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath( (Join-Path $OutPath "$OutFile.map") )
	$OutPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath( (Join-Path $OutPath $OutFile) )


	$Map = New-Object SourcemapToolkit.SourcemapParser.SourceMap

	$Map.Version = 3
	$Map.File = $OutFile
	$Map.ParsedMappings = New-Object System.Collections.Generic.LinkedList[SourcemapToolkit.SourcemapParser.MappingEntry]
	$Map.Sources = New-Object System.Collections.Generic.LinkedList[String]

	$SourceRoot = (Resolve-Path $FunctionPath).Path
	#$Map.SourceRoot = (Resolve-Path $FunctionPath).Path


	$MappingsSB = New-Object System.Text.StringBuilder
	$SourceIndex = 0
	$GeneratedLine = 0
	try {
		$outfs = [IO.File]::Open($OutPath, "Create")
		$outWriter = New-Object System.IO.StreamWriter($OutFs)
		dir $FunctionPath -Recurse -Include *.ps1 -Exclude *.Tests.ps1 -File | %{
			$FullPath = $_.Fullname
			$RelativePath = $FullPath.Substring($Sourceroot.Length)
			$SourceLine = 0
			[IO.File]::ReadAllLines($FullPath) | ForEach-Object {
				$Mapping = New-Object SourcemapToolkit.SourcemapParser.MappingEntry
				$Mapping.GeneratedSourcePosition = New-Object SourcemapToolkit.SourcemapParser.SourcePosition -Property @{
					ZeroBasedLineNumber = $GeneratedLine
					ZeroBasedColumnNumber = 0
				}

				$Mapping.OriginalSourcePosition = New-Object SourcemapToolkit.SourcemapParser.SourcePosition -Property @{
					ZeroBasedLineNumber = $SourceLine
					ZeroBasedColumnNumber = 0
				}

				$Mapping.OriginalFileName = $RelativePath

				$Map.ParsedMappings.Add($Mapping)

				$OutWriter.WriteLine($_)

				$SourceLine++
				$GeneratedLine++
			}

			$Map.Sources.Add($RelativePath)
			#$Map.sourcesContent += $null #Get-Content $FullPath -Raw

			$SourceIndex++
		}

		$SourceMapGenerator = New-Object SourcemapToolkit.SourcemapParser.SourceMapGenerator
		Set-Content -Path $MapFile -Value $SourceMapGenerator.SerializeMapping($Map)

		#Set-Content -Path $MapFile -Value (ConvertTo-Json $Map)
	} finally {
		if($OutWriter) {
			$OutWriter.Dispose()
		}
		if($OutFS) {
			$OutFS.Dispose()
		}
	}
}

function Import-SourceMap {
	param(
		$Path
	)
	$Path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath( $Path )

	$Parser = New-Object SourcemapToolkit.SourcemapParser.SourcemapParser
	$Parser.ParseSourceMap($Path)
}

function Get-ModuleScriptLoader {
	param(
		$FunctionPath
	)
	$Tokens = $null
	$ParseErrors = @()

	$FunctionPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath( $FunctionPath )

	$Ast = [System.Management.Automation.Language.Parser]::ParseFile($FunctionPath, [ref]$Tokens, [ref]$ParseErrors)
	$ParseErrors | ForEach-Object {
		Write-Error $_
	}
	$Map = Import-SourceMap "$FunctionPath.map"
	$Global:Map = $Map
	$Global:Ast = $Ast
	$PSMapper = New-Object SourceMapper
	$MappedAst = $PSMapper.MapFunctions($Ast, $Map)
	$MappedAst.GetScriptBlock()
}


# Export only the functions using PowerShell standard verb-noun naming.
Export-ModuleMember -Function *-*
