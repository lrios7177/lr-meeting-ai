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

Resultado: `artifacts/installer/LRMeetingAI-Setup-0.2.0-win-x64.exe`. El script publica una aplicación autónoma: el usuario final no necesita instalar .NET. Primera restauración y publicación requieren Internet en el equipo de desarrollo; la captura instalada funciona localmente.

## Uso
Seleccionar salida y micrófono, reproducir sonido y hablar, pulsar «Prueba de 30 segundos» y escuchar ambos canales por separado. Las grabaciones se almacenan en `%LOCALAPPDATA%\LR Meeting AI\Recordings`. Cada sesión conserva dos WAV y `capture.jsonl`; `diagnostics.log` recoge errores generales.

La captura por loopback registra lo que pasa por el endpoint elegido, incluidas otras aplicaciones. No capta automáticamente todas las salidas de Windows. No incorpora cancelación de eco ni separación de hablantes.

Consultar `docs/PRUEBA-WINDOWS.md` antes de avanzar con IA y `docs/ARQUITECTURA.md` para alcance, límites de sincronización, soporte y módulos futuros. El instalador está definido, pero debe construirse y validarse en Windows.

## Descargar desde GitHub Actions
Cada envío a `main` compila en Windows, comprueba el medidor y genera el instalador con Inno Setup. En GitHub abre **Actions → Windows build and installer**, elige una ejecución completada y descarga **LRMeetingAI-installer-win-x64** desde **Artifacts**. Descomprime el ZIP y ejecuta el instalador `.exe`. También se publica el paquete portátil completo; conserva sus archivos juntos.

Los paquetes son de desarrollo y no tienen firma Authenticode. La compilación automática no prueba el hardware: primero completa `docs/PRUEBA-WINDOWS.md`. No se publican Releases automáticamente.

## Reuniones largas y archivo único
La duración no tiene un límite fijo de tiempo en el código. Cada canal rota automáticamente en WAV de hasta 1 GiB, conservando muestras completas y orden de partes. «Escuchar A/B» reproduce las partes consecutivamente. Esto elimina el límite de 4 GiB por WAV; la duración real depende de disco, formato de audio, alimentación, sistema de archivos y estabilidad del equipo. Todavía deben validarse las reuniones largas en hardware Windows.

Al detener, pulsa **Guardar reunión en un archivo** para crear un paquete `.lrmeeting.zip` (ZIP64 cuando haga falta) junto a la carpeta de sesión. Contiene ambos canales, todas sus partes, el registro temporal y un manifiesto. Es un contenedor de reunión, no un audio mezclado reproducible directamente. Puedes abrirlo con herramientas ZIP compatibles. Los WAV originales se conservan: al empaquetar necesitas espacio adicional aproximadamente igual al tamaño de la reunión. Se evita sobrescribir un archivo ya creado.

El informe ejecutivo sigue pendiente de la puerta de captura; consulta `docs/INFORME-EJECUTIVO.md` para el comportamiento previsto.
