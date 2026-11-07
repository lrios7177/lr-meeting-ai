# Validación de desarrollo
- SDK local: .NET 10.0.403, compilación cruzada desde Linux.
- Versión 0.2.0: `dotnet build -c Release` correcto, cero advertencias y cero errores.
- `dotnet run --project tests/LRMeeting.AudioChecks -c Release`: 10 grupos correctos: ocho comprobaciones del medidor, rotación WAV con reconstrucción exacta de muestras y offsets, y empaquetado con verificación de datos, cancelación y preservación de archivos existentes.
- La prueba de rotación usa un límite reducido de 32 bytes de audio para cubrir varios cambios de archivo sin grabar gigabytes. No reemplaza la prueba de una reunión prolongada.
- Primera ejecución Windows de GitHub Actions: correcta, compilación, pruebas de medidores, publicación autónoma e instalador Inno Setup generados. Ejecución: https://github.com/lrios7177/lr-meeting-ai/actions/runs/42398850602 (código inicial 0.1.0).
- Los cambios 0.2.0 se verificarán mediante la siguiente ejecución de Actions al subirlos.
- El runner compila y empaqueta: no demuestra instalación en un equipo de usuario, funcionamiento de WPF, captura WASAPI, sincronización ni estabilidad en hardware. Pendiente protocolo docs/PRUEBA-WINDOWS.md.
- No hay integración IA ni pruebas de transcripción: se mantiene la puerta de captura solicitada.
