# LR Meeting AI
**Autor: Ing. Luis Ríos, MBA**

Primera implementación de captura doble local para Windows x64, con C#, WPF, .NET 10 y NAudio. Es una entrega de desarrollo, pendiente de prueba física de audio en Windows. No contiene todavía transcripción ni análisis IA.

## Ejecutar y construir
Instalar SDK .NET 10 en Windows. Desde la raíz:

```powershell
dotnet build LRMeetingAI.sln -c Release
dotnet run --project src/LRMeeting.App
```

Para generar instalador, instalar además Inno Setup 6:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-Windows.ps1
```

Resultado: `artifacts/installer/LRMeetingAI-Setup-0.1.0-win-x64.exe`. El script publica una aplicación autónoma: el usuario final no necesita instalar .NET. Primera restauración y publicación requieren Internet en el equipo de desarrollo; la captura instalada funciona localmente.

## Uso
Seleccionar salida y micrófono, reproducir sonido y hablar, pulsar «Prueba de 30 segundos» y escuchar ambos canales por separado. Las grabaciones se almacenan en `%LOCALAPPDATA%\LR Meeting AI\Recordings`. Cada sesión conserva dos WAV y `capture.jsonl`; `diagnostics.log` recoge errores generales.

La captura por loopback registra lo que pasa por el endpoint elegido, incluidas otras aplicaciones. No capta automáticamente todas las salidas de Windows. No incorpora cancelación de eco ni separación de hablantes.

Consultar `docs/PRUEBA-WINDOWS.md` antes de avanzar con IA y `docs/ARQUITECTURA.md` para alcance, límites de sincronización, soporte y módulos futuros. El instalador está definido, pero debe construirse y validarse en Windows.

## Descargar desde GitHub Actions
Cada envío a `main` compila en Windows, comprueba el medidor y genera el instalador con Inno Setup. En GitHub abre **Actions → Windows build and installer**, elige una ejecución completada y descarga **LRMeetingAI-installer-win-x64** desde **Artifacts**. Descomprime el ZIP y ejecuta el instalador `.exe`. También se publica el paquete portátil completo; conserva sus archivos juntos.

Los paquetes son de desarrollo y no tienen firma Authenticode. La compilación automática no prueba el hardware: primero completa `docs/PRUEBA-WINDOWS.md`. No se publican Releases automáticamente.
