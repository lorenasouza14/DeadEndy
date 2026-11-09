param([string]$UnityEditorPath)
$ErrorActionPreference = 'Stop'
$repoRaiz = Split-Path $PSScriptRoot -Parent
Push-Location $repoRaiz
try {
    if (-not $UnityEditorPath) {
        $versaoUnity = [regex]::Match((Get-Content ProjectSettings/ProjectVersion.txt -Raw), 'm_EditorVersion: ([^\r\n]+)').Groups[1].Value
        $UnityEditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$versaoUnity/Editor"
    }
    $dotnetSave = Join-Path $UnityEditorPath 'Data/NetCoreRuntime/dotnet.exe'
    $cscSave = (Get-ChildItem (Join-Path $UnityEditorPath 'Data/DotNetSdk/sdk') -Filter csc.dll -Recurse | Select-Object -First 1).FullName
    $runtimeSave = Get-ChildItem (Join-Path $UnityEditorPath 'Data/NetCoreRuntime/shared/Microsoft.NETCore.App') -Directory |
        Sort-Object Name -Descending | Select-Object -First 1
    $rspUnity = Get-ChildItem Library/Bee/artifacts -Filter Assembly-CSharp.rsp -Recurse |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $rspUnity) { throw 'Compile o projeto no Unity uma vez para gerar as referências.' }
    $destinoSave = Join-Path $repoRaiz 'Logs/autosave-compile'
    New-Item -ItemType Directory -Force -Path $destinoSave | Out-Null
    $argumentosSave = @(Get-Content $rspUnity.FullName | ForEach-Object {
        if ($_ -like '-out:*') { '-out:"Logs/autosave-compile/Assembly-CSharp.dll"' }
        elseif ($_ -like '-refout:*') { '-refout:"Logs/autosave-compile/Assembly-CSharp.ref.dll"' }
        else { $_.Replace('"Assets/Scripts/MenuManeger.cs"', '"Assets/Scripts/MenuManager.cs"') }
    })
    foreach ($fonteSave in Get-ChildItem Assets/Scripts -Filter '*.cs') {
        $entradaSave = '"Assets/Scripts/' + $fonteSave.Name + '"'
        if ($argumentosSave -notcontains $entradaSave) { $argumentosSave += $entradaSave }
    }
    $argumentosSave | Set-Content "$destinoSave/Assembly-CSharp.rsp" -Encoding utf8
    & $dotnetSave $cscSave '@Logs/autosave-compile/Assembly-CSharp.rsp'
    if ($LASTEXITCODE -ne 0) { throw 'A compilação dos scripts de jogo falhou.' }
    $referenciasSave = @('-nologo', '-target:exe', '-out:Logs/autosave-compile/AutosavePersistenceChecks.dll', '-r:Logs/autosave-compile/Assembly-CSharp.dll')
    foreach ($nomeRef in @('System.Private.CoreLib.dll', 'System.Runtime.dll', 'System.Console.dll', 'System.IO.FileSystem.dll', 'System.IO.FileSystem.Primitives.dll', 'netstandard.dll')) {
        $referenciasSave += '-r:"' + (Join-Path $runtimeSave.FullName $nomeRef) + '"'
    }
    $referenciasSave += 'Tests/AutosavePersistenceChecks.cs'
    $referenciasSave | Set-Content "$destinoSave/checks.rsp" -Encoding utf8
    @{ runtimeOptions = @{ tfm = 'net8.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $runtimeSave.Name } } } |
        ConvertTo-Json -Depth 5 | Set-Content "$destinoSave/AutosavePersistenceChecks.runtimeconfig.json" -Encoding utf8
    & $dotnetSave $cscSave '@Logs/autosave-compile/checks.rsp'
    if ($LASTEXITCODE -ne 0) { throw 'A compilação dos testes falhou.' }
    & $dotnetSave 'Logs/autosave-compile/AutosavePersistenceChecks.dll' 'Logs/autosave-tests' |
        Tee-Object -FilePath 'Logs/autosave-persistence-checks.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Os testes de persistência falharam.' }
}
finally { Pop-Location }
