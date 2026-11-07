# LR Meeting AI — 0.4.0
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
En GitHub abre **Actions → Windows build and installer**, selecciona una ejecución satisfactoria y descarga **LRMeetingAI-installer-win-x64** desde **Artifacts**. Descomprime y ejecuta el `.exe`. El runtime .NET va incluido. El ZIP del instalador completo contiene el ejecutable y sus archivos de datos; no ejecutes solo el EXE sin extraer los demás.

La edición completa incluye Whisper.cpp b5454 para CPU, su modelo base multilingüe, Ollama 0.40.15, qwen3:1.7b y las DLL de Visual C++ necesarias. No requiere descargas de IA al instalar ni durante el uso. Extrae **todos** los archivos del ZIP en la misma carpeta y ejecuta el instalador `.exe`; conserva junto a él los archivos `.bin` de datos del instalador. No tiene firma Authenticode. La publicación comercial requiere validar el equipo objetivo y preparar firma/actualizaciones; revisa `docs/THIRD-PARTY-NOTICES.md`.

## Primera configuración — edición completa
Las rutas de los motores y modelos incluidos se detectan automáticamente. Ollama arranca cuando hace falta en `127.0.0.1:11435`, usa el modelo incluido y se detiene al cerrar la app. No necesita instalar un servicio global ni modificar un Ollama que ya tengas.

1. Instala la edición completa y abre la aplicación.
2. Selecciona salida y micrófono y realiza la prueba de audio.
3. En Ajustes puedes pulsar **Comprobar configuración y Ollama**. Si conservas ajustes antiguos, pulsa **Usar motores incluidos**.
4. Graba la reunión y pulsa Terminar para generar la transcripción e informe local.

El perfil incluido usa CPU y un modelo de informes ligero, qwen3:1.7b. La velocidad depende del equipo; 8 GB de RAM es una orientación inicial, y 16 GB deja más margen. El tamaño final del instalador depende de sus modelos y puede superar 1 GB. Reserva espacio para aplicación, modelos, temporales y grabaciones. No se garantiza ejecución en cualquier CPU o cantidad de memoria.

Si prefieres motores propios, la configuración manual sigue disponible: whisper-cli y un modelo multilingüe .bin; Ollama con un modelo local y dirección HTTP loopback. Los motores incluidos no usan variantes cloud.

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
powershell -ExecutionPolicy Bypass -File scripts/Build-Windows.ps1 -IncludeAI
```
Requiere SDK .NET 10 e Inno Setup 6 para generar el instalador. GitHub Actions compila, ejecuta pruebas de audio/persistencia/contratos IA, construye e instala el `.exe`, abre y renderiza WPF en modo de prueba y verifica conversión de audio. El artefacto **LRMeetingAI-interface-preview** contiene capturas de la interfaz sin grabar ni simular resultados de modelos reales.

Las pruebas automatizadas usan un proceso Whisper de prueba y respuestas HTTP de Ollama controladas para verificar el contrato y los fallos; no demuestran calidad de modelos reales ni captura de hardware. Sigue `docs/PRUEBA-WINDOWS.md` para la aceptación en equipo real.

## Construir el paquete completo
`Prepare-Engines.ps1` descarga versiones fijadas de motores desde sus releases oficiales, verifica SHA256, obtiene el modelo Whisper desde una revisión inmutable y verifica el SHA256 LFS publicado. Ollama descarga y valida sus blobs de modelo; el manifiesto registra el digest del modelo. El paquete conserva licencias de motores y modelos. Se excluyen bibliotecas GPU del perfil CPU para reducir tamaño.

La compilación del paquete completo necesita Internet, varios GB de espacio temporal y un entorno Windows con Visual Studio/CRT redistribuible (como el runner usado por GitHub Actions). Después, el equipo de destino puede instalar y procesar reuniones sin conexión. La edición completa conserva los datos del usuario al desinstalar, como la edición anterior. El artefacto grande se conserva siete días en Actions; puede reconstruirse con una nueva ejecución.
