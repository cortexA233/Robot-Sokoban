$ErrorActionPreference = 'Stop'

# Keep terminal calls on the same server and project as .codex/config.toml.
$mcpCli = Get-Command unity-mcp -ErrorAction SilentlyContinue
if ($null -eq $mcpCli) {
    throw 'Install the CLI first: uv tool install --python 3.13 mcpforunityserver==10.2.0'
}

# The upstream CLI prints Unicode status symbols even with JSON output.
$previousEncoding = $env:PYTHONIOENCODING
try {
    $env:PYTHONIOENCODING = 'utf-8'
    & $mcpCli.Source --host 127.0.0.1 --port 8087 --instance Sokoban_3D_Test @args
    $cliExitCode = $LASTEXITCODE
}
finally {
    $env:PYTHONIOENCODING = $previousEncoding
}
exit $cliExitCode
