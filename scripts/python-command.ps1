Set-StrictMode -Version Latest

function Resolve-PythonCommand {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -ne $pythonCommand) {
        return [PSCustomObject]@{ Executable = $pythonCommand.Source; PrefixArgs = @() }
    }

    $pyLauncher = Get-Command py -ErrorAction SilentlyContinue
    if ($null -ne $pyLauncher) {
        return [PSCustomObject]@{ Executable = $pyLauncher.Source; PrefixArgs = @('-3') }
    }

    throw 'Python command not found. Install Python or ensure python/py is on PATH.'
}