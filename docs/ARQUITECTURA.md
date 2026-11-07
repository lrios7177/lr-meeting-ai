# LR Meeting AI — arquitectura y alcance
Autor: Ing. Luis Ríos, MBA.

## Primera entrega
WPF y .NET 10; biblioteca independiente LRMeeting.Audio con NAudio/WASAPI. Dos fuentes elegidas explícitamente, WAV independiente, medidores, prueba de 30 segundos, reproducción individual, reloj monotónico común, registro JSONL, errores visibles y comprobación de disponibilidad cada dos segundos durante la captura. Las fuentes quedan fijas hasta detener la sesión: cambiar el dispositivo predeterminado de Windows no redirige la grabación.

## Límite de validación
Compilar no valida WASAPI. Este entorno Linux no ejecuta WPF ni prueba hardware de Windows. La implementación es inicial y debe pasar el protocolo de aceptación antes de integrar IA. No se incluye todavía Whisper, Ollama, SQLite, informe ejecutivo, exportación de actas, pausa/reanudación ni importación.

Las marcas de tiempo representan llegada de callbacks y el inicio de cada capturador, no el reloj del hardware. No garantizan sincronización de muestras ni compensación de deriva. Loopback puede omitir buffers durante el silencio: el WAV puede tener una duración menor que el tiempo de pared; capture.jsonl conserva tiempos de llegada y offsets. Antes de transcribir será necesario reconstruir una línea temporal por canal, insertar silencios y validar deriva durante una reunión prolongada. El callback escribe a disco: medir latencia y desarrollar una cola acotada con consumidor dedicado antes de usar en producción. Los WAV rotan automáticamente a 1 GiB por parte, alineados a muestras; el registro identifica archivo, offset y posición dentro del callback. No hay límite fijo de tiempo de sesión. El empaquetado usa ZIP64 cuando corresponde y requiere espacio adicional porque conserva los originales. Falta medir estabilidad en hardware para reuniones prolongadas. Un cierre forzado del proceso puede dejar un WAV sin finalizar. Las grabaciones contienen información sensible y no se cifran en esta versión.

## Módulos siguientes (diseñados, no implementados)
- Core: Meeting, AudioTrack, TranscriptSegment con SourceChannel y tiempos absolutos; TranscriptRevision mantiene texto original y correcciones.
- Transcription: adaptador a proceso whisper.cpp local, modelos español/inglés, cola persistente independiente del grabador; bloques con solapamiento y reconciliación por tiempos/texto para evitar duplicados. Si se retrasa, acumula trabajo en disco.
- Intelligence: cliente HTTP limitado a Ollama local, salida estructurada validada, referencias a segmentos como evidencia, responsables desconocidos cuando no exista evidencia. El texto transcrito es dato, no instrucciones. Modelos y licencias se seleccionarán antes de distribuir.
- Persistence: SQLite con migraciones, WAL, reuniones, pistas, segmentos originales, revisiones, trabajos y resultados de análisis. Escrituras incrementales transaccionales y recuperación de trabajos interrumpidos.
- Export: servicios de exportación independientes de WPF, con revisión humana del acta.

## Distribución
Publicación autónoma win-x64, incluye runtime .NET; Inno Setup 6 crea instalador por usuario sin administrador. No se incluyen modelos ni Ollama en esta etapa. Revisar licencias de dependencias y modelos, avisos legales, firma Authenticode y política de actualización antes de venta. La autoría no constituye una licencia comercial de terceros.
Windows 11 x64 con soporte vigente es la opción preferida. Windows 10 22H2 se admite técnicamente, pero su soporte general finalizó el 14/10/2025: usar solo si cuenta con cobertura vigente aplicable. El instalador no comprueba ESU. La versión mínima no implica soporte comercial del sistema.

## Archivo de reunión
LRMeeting.Storage es independiente de WPF y WASAPI. Genera un paquete ZIP con las partes WAV, capture.jsonl y meeting.json (schemaVersion 1). Se escribe primero a un temporal; solo se publica la ruta final al completar. Cancelación o error eliminan el temporal y conservan la grabación. No incluye transcripción ni informe todavía. No implementa importación de paquetes en esta entrega.
