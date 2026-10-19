# Turns docs/manual.md into a stand-alone HTML page that ships with the app.
# Usage: pwsh build/manual.ps1 -Output "publish/installer/BookHeart Manual.html" [-Version 0.1.0]
param([Parameter(Mandatory)][string]$Output, [string]$Version)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$body = (ConvertFrom-Markdown -Path (Join-Path $root 'docs/manual.md')).Html

$html = @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>BookHeart manual</title>
<style>
  :root { color-scheme: light dark; --bg: #F3F7FB; --ink: #0A2540; --muted: #3D5570; --link: #1747B8; --line: #B9C7D8; --surface: #FFFFFF; }
  @media (prefers-color-scheme: dark) { :root { --bg: #0A1628; --ink: #F2F6FA; --muted: #B8C8DA; --link: #FFD100; --line: #3A5475; --surface: #13233B; } }
  body { margin: 0; background: var(--bg); color: var(--ink); font: 1.2rem/1.6 "Atkinson Hyperlegible", "Segoe UI", Verdana, sans-serif; }
  main { max-width: 46rem; margin: 0 auto; padding: 1.5rem 1.25rem 4rem; }
  h1 { font-size: 2.2rem; line-height: 1.2; }
  h2 { margin-top: 2.5rem; padding-top: 1rem; border-top: 2px solid var(--line); }
  a { color: var(--link); }
  a:focus-visible { outline: 3px solid var(--link); outline-offset: 2px; }
  table { border-collapse: collapse; width: 100%; background: var(--surface); }
  th, td { border: 1px solid var(--line); padding: 0.4rem 0.7rem; text-align: left; vertical-align: top; }
  code { font-size: 0.95em; }
  .version { color: var(--muted); }
</style>
</head>
<body>
<main>
$body
$(if ($Version) { "<p class=`"version`">BookHeart $Version</p>" })
</main>
<script>
  // Give headings the same ids GitHub does, so the contents links work.
  document.querySelectorAll('h2, h3').forEach(function (h) {
    if (!h.id) h.id = h.textContent.trim().toLowerCase().replace(/[^\w\- ]+/g, '').replace(/ /g, '-');
  });
</script>
</body>
</html>
"@
New-Item -ItemType Directory (Split-Path $Output -Parent) -Force | Out-Null
Set-Content -Path $Output -Value $html -Encoding utf8
Write-Host "Wrote $Output"
