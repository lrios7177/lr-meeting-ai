# LR Meeting AI — 0.3.0
**Ing. Luis Ríos, MBA** · Windows x64 · C# / WPF / .NET 10

Aplicación de escritorio para grabar reuniones, transcribir con Whisper.cpp local y generar un informe ejecutivo con Ollama local. No utiliza servicios externos de transcripción ni APIs de pago. Es una versión de desarrollo: la calidad y estabilidad con hardware y modelos reales deben validarse en el equipo de destino.

## Lo que incluye
- Interfaz moderna con Reunión, Transcripción, Informe, Historial y Ajustes.
- Salida de Windows por WASAPI Loopback y micrófono elegido, independientes y simultáneos. Medidores, prueba de 30 segundos, reproducción por canal y errores visibles.
- Grabación sin límite fijo de tiempo: partes WAV automáticas de aproximadamente 30 segundos de audio por canal, siempre por debajo de 4 GiB. La duración real depende del disco, del hardware y de la estabilidad del sistema.
- Pausa y reanudación en tomas separadas, sin sobrescribir el audio previo.
- Whisper.cpp por bloques, con español/inglés, progreso, conservación de marcas temporales y almacenamiento incremental en SQLite. La captura no espera al motor. Los bloques ya guardados no se duplican al reintentar.
- Ollama al terminar: resumen ejecutivo con redacción propia, temas, acuerdos, tareas, responsables/fechas por confirmar cuando falte evidencia, riesgos y asuntos pendientes. Las afirmaciones estructuradas deben citar IDs reales de la transcripción.
- Historial SQLite, correcciones que conservan el original, reanudación de trabajos e informes versionados en la base de datos.
- Importación de WAV y formatos compatibles con los decodificadores de NAudio/Windows (MP3, FLAC, M4A según el equipo).
- Informe HTML moderno, JSON y transcripción TXT/JSON. El navegador permite imprimir o guardar PDF. Paquete `.lrmeeting.zip` con ambos canales, tiempos y resultados disponibles; ZIP64 para tamaños grandes.

## Instalar la aplicación
En GitHub abre **Actions → Windows build and installer**, selecciona una ejecución satisfactoria y descarga **LRMeetingAI-installer-win-x64** desde **Artifacts**. Descomprime y ejecuta el `.exe`. El runtime .NET va incluido. También existe un paquete portátil con todos los archivos necesarios.

El instalador no incluye Whisper.cpp, modelos ni Ollama. No tiene firma Authenticode. La publicación comercial requiere validar el equipo objetivo y preparar firma/actualizaciones; revisa `docs/THIRD-PARTY-NOTICES.md`.

## Primera configuración
1. Instala Ollama para Windows desde https://ollama.com/download/windows y ejecuta `ollama pull qwen3:4b`, o instala otro modelo local compatible con salida JSON estructurada. El tamaño y la velocidad dependen del modelo y del hardware. Evita variantes cloud.
2. Descarga una compilación Windows x64 de **whisper.cpp** desde https://github.com/ggml-org/whisper.cpp/releases. Conserva sus DLL junto a `whisper-cli.exe`.
3. Obtén un modelo **multilingüe** de Whisper compatible con whisper.cpp, por ejemplo `ggml-base.bin` o `ggml-small.bin`, siguiendo la documentación oficial de whisper.cpp. Los modelos `.en` son solo para inglés. La aplicación no descarga archivos automáticamente.
4. En **Ajustes**, selecciona el ejecutable y el modelo `.bin`, el idioma y el nombre exacto del modelo Ollama instalado. Mantén `http://127.0.0.1:11434` si usas la configuración normal.
5. Guarda y pulsa **Comprobar configuración y Ollama**. Esto comprueba archivos presentes y `/api/show` de Ollama, no la calidad del reconocimiento. La primera transcripción verifica la compatibilidad del CLI con `-oj`.

Las descargas iniciales necesitan Internet; una vez instalados los motores y modelos, la app usa archivos locales y HTTP loopback. El adaptador no acepta servidores Ollama externos ni redirecciones; rechaza modelos identificados como remotos en sus metadatos.

## Reunión e informe automático
Selecciona los dispositivos, realiza la prueba de 30 segundos y escucha A y B por separado. Con audífonos de 3.5 mm elige el endpoint de reproducción que realmente usa Windows; no se necesita «Stereo Mix».

Activa **Transcribir progresivamente y generar el informe al terminar**, pulsa **Iniciar reunión** y luego **Terminar reunión**. Si faltan motores, el audio puede grabarse de todas formas y el procesamiento queda pendiente con un error visible. Puedes corregir Ajustes y reanudar con **Transcribir y generar informe**. Una cancelación de IA no detiene la captura activa. Cerrar durante una reunión guarda la captura y los puntos de reanudación; el análisis largo se retoma desde Historial.

En **Transcripción**, corrige el texto y pulsa **Guardar correcciones**; después regenera el informe. En **Informe**, abre el HTML y usa **Imprimir / Guardar PDF**. El informe siempre es un borrador revisable. La IA puede equivocarse en nombres, cifras, decisiones y atribuciones; una referencia válida no demuestra por sí sola que una interpretación sea correcta.

**Guardar paquete de reunión** conserva audio de ambos canales, registros temporales, metadatos, transcripciones e informe disponibles. Los archivos de trabajo y la base SQLite compartida no se incluyen. Se escribe primero a un temporal y se conservan los originales. Si ya existe un paquete, se crea otra versión. Necesitas espacio adicional similar al tamaño de la reunión; usa un sistema de archivos que admita archivos grandes. No hay importador de paquetes ZIP en esta versión; los WAV se pueden extraer e importar individualmente.

## Datos y límites
Todo se guarda en `%LOCALAPPDATA%\LR Meeting AI`: `meetings.db`, `settings.json`, `Recordings` y diagnóstico. No hay cifrado propio de grabaciones/base de datos. Las copias de paquetes e informes contienen conversaciones: gestiona su acceso y borrado.

Las marcas de captura son aproximadas, derivadas de callbacks, y no corrigen deriva entre dispositivos. Se conserva la discontinuidad temporal de silencios cuando existe el registro de captura; la reproducción por canal concatena audio y puede omitir silencios donde WASAPI no emitió paquetes. A/B indica fuente, no identidad de cada participante. No hay cancelación de eco ni diarización automática. Los registros antiguos 0.1/0.2 sin metadatos numéricos no ofrecen el mismo mapeo temporal. Un cierre forzado puede dejar el último WAV sin finalizar: se debe revisar; no se repara automáticamente.

Windows 11 x64 con soporte vigente es la opción recomendada. Windows 10 22H2 tiene compatibilidad técnica, pero requiere cobertura de soporte aplicable (por ejemplo ESU); su soporte general terminó el 14/10/2025. No se comprueba ESU desde el instalador.

## Desarrollo y verificaciones
```powershell
dotnet build LRMeetingAI.sln -c Release
dotnet run --project tests/LRMeeting.AudioChecks -c Release
dotnet run --project tests/LRMeeting.CoreChecks -c Release
dotnet run --project src/LRMeeting.App
powershell -ExecutionPolicy Bypass -File scripts/Build-Windows.ps1
```
Requiere SDK .NET 10 e Inno Setup 6 para generar el instalador. GitHub Actions compila, ejecuta pruebas de audio/persistencia/contratos IA, construye e instala el `.exe`, abre y renderiza WPF en modo de prueba y verifica conversión de audio. El artefacto **LRMeetingAI-interface-preview** contiene capturas de la interfaz sin grabar ni simular resultados de modelos reales.

Las pruebas automatizadas usan un proceso Whisper de prueba y respuestas HTTP de Ollama controladas para verificar el contrato y los fallos; no demuestran calidad de modelos reales ni captura de hardware. Sigue `docs/PRUEBA-WINDOWS.md` para la aceptación en equipo real.
