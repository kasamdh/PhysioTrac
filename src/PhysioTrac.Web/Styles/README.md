# Tailwind CSS build

This project uses Tailwind CSS v4 via the standalone CLI (no Node.js
required). The binary isn't committed (large, platform-specific) -- fetch it
once per machine:

```powershell
Invoke-WebRequest -Uri "https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-windows-x64.exe" -OutFile ".tools\tailwindcss.exe"
```

(On macOS/Linux, grab the matching `tailwindcss-macos-*`/`tailwindcss-linux-*`
asset instead and update the `TailwindCli` path in `PhysioTrac.Web.csproj`.)

The `BuildTailwind` MSBuild target (in `PhysioTrac.Web.csproj`) runs the CLI
automatically before every build, compiling `Styles/app.tailwind.css` ->
`wwwroot/css/app.built.css`. To watch for changes while developing:

```powershell
.tools\tailwindcss.exe -i src\PhysioTrac.Web\Styles\app.tailwind.css -o src\PhysioTrac.Web\wwwroot\css\app.built.css --watch
```
