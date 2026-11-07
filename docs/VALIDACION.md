# Validación 0.3.0
- Compilación local cruzada: .NET SDK 10.0.403, cero errores y cero advertencias.
- AudioChecks: 10 grupos; niveles PCM/float, rotación WAV sin pérdida de muestras, offsets, empaquetado, cancelación y conservación de originales.
- CoreChecks: 28 comprobaciones; SQLite real y transacciones, correcciones y original, invalidación de informe, proceso hijo de prueba para el contrato CLI Whisper y cancelación, checkpoints de reanudación, exclusión de WAV abiertos, huecos temporales, lectura incremental de JSONL y tiempos de más de 24 horas, restricciones de Ollama local, análisis por lotes, caché, evidencia inválida, truncamiento y escape HTML.
- La dependencia nativa SQLite se actualizó a 3.53.3; restauración sin la advertencia de vulnerabilidad de la biblioteca nativa anterior.
- GitHub Actions incorpora compilación Windows, ambos grupos de pruebas, publicación autónoma, Inno Setup y prueba de instalación/render WPF/conversión de audio/desinstalación. Sus resultados finales están en la ejecución asociada al commit.
- El modo WPF smoke genera vistas de Reunión y Ajustes sin ejecutar motores reales. Además convierte 61 s de audio sintético estéreo float 48 kHz a tres bloques PCM16 mono 16 kHz y comprueba silencio.
- Las respuestas Ollama de CoreChecks son un transporte HTTP de prueba y el CLI Whisper es un proceso fixture. No son pruebas de calidad de un modelo real. No hay motores/modelos reales configurados en este entorno de desarrollo.
- Pendiente en hardware Windows: captura simultánea, jack de 3.5 mm, desconexiones, latencia, sincronización/deriva, duración prolongada, calidad de transcripción y objetividad del informe con modelos reales. Consultar PRUEBA-WINDOWS.md.
