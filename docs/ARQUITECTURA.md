# Arquitectura 0.4.0
Autor: Ing. Luis Ríos, MBA.

- **LRMeeting.App**: WPF, navegación, estados de reunión, selección de dispositivos, historial, correcciones, ajustes y orquestación. Una instancia por sesión de Windows impide que dos ventanas gestionen la misma base y archivos activos.
- **LRMeeting.Audio**: NAudio y WASAPI, fuentes independientes, partes WAV alineadas a muestras, medidores, pruebas y conversión a PCM16 mono 16 kHz con resampling WDL. El callback no ejecuta IA; escribe WAV y JSONL. Sigue pendiente medir latencia real de disco y separar escritura en una cola si las pruebas de carga lo requieren.
- **LRMeeting.Core**: contratos, pipeline con checkpoints, adaptador CLI Whisper.cpp, cliente Ollama estrictamente local, mapeo temporal por callbacks y exportación de informe HTML/JSON/TXT.
- **LRMeeting.Storage**: SQLite con WAL, transacciones por bloque, originales inmutables, revisiones de texto, versiones de informes y empaquetado ZIP64. Esquema inicial identificado por user_version=1; futuras versiones requerirán migraciones explícitas.

## Ciclo de grabación
Cada reunión usa su propia carpeta; cada pausa/reanudación crea una toma `take-NNNN`. Cada toma guarda A/B por separado y `capture.jsonl`. Las partes cierran al alcanzar 30 segundos de muestras; una salida silenciosa de WASAPI puede emitir menos datos, por lo que se trata de tiempo de audio, no de pared. Un marcador `.recording` mantiene el último archivo fuera del reconocimiento mientras esté abierto. Al detener se cierra el último WAV y se retira el marcador. La desconexión interrumpe visiblemente; no se redirige automáticamente a otro dispositivo.

Un bucle de trabajo independiente descubre archivos cerrados y transcribe. Al terminar se procesan los pendientes y después Ollama genera el informe, si está activado. Cancelar IA mata el subproceso activo cuando corresponde y conserva los checkpoints. En caso de error o cierre de la app, Historial permite reanudar. No se reparan automáticamente WAV dañados por una terminación abrupta.

## Transcripción
Los archivos se convierten en bloques PCM de 30 segundos sin cargar toda la reunión en memoria. Se conserva una caché local que verifica tamaño y fecha del audio fuente. Los bloques no tienen solapamiento, por lo que no se crean duplicaciones por ventanas superpuestas; los IDs y transacciones evitan duplicar al reintentar. Una palabra o frase en el límite puede reconocerse peor. No se aplica una eliminación heurística de frases iguales que podría borrar repeticiones legítimas.

El proceso Whisper usa ArgumentList, lee stdout/stderr simultáneamente, tiene cancelación y timeout por bloque y valida timestamps del JSON. Se preservan origen, canal, posición y texto original. Los segmentos corregidos quedan en revisiones; un informe previo se marca desactualizado. A/B son canales, no una identificación biométrica de personas.

## Tiempo y sincronización
La captura registra tasa de bytes, offset en parte WAV, offset dentro del callback y llegada monotónica. El mapeo conserva pausas y huecos de callbacks en la transcripción. No son timestamps de muestras de hardware; no hay compensación de deriva. Para importación sin log se usa la línea temporal del archivo. Los logs antiguos sin tasas numéricas tienen límites adicionales y deben revisarse. No se promete sincronización perfecta.

## Ollama e informe
HTTP exclusivamente a localhost/127.0.0.1/::1, sin proxy ni redirecciones. Modelos cloud y metadatos remotos se rechazan. Las instrucciones separan explícitamente la transcripción como dato; no se ejecutan acciones a partir de contenido transcrito.

Los segmentos se agrupan según presupuesto de texto y se analizan por lotes. Cada afirmación estructurada exige evidencia con IDs presentes en su lote. El resumen ejecutivo se reduce jerárquicamente y el resto de puntos detallados se conserva. Los resultados por lote se guardan en caché por hash de modelo, endpoint y contenido, lo que permite reintentar y cambiar texto sin reutilizar notas incorrectas. No se publica salida truncada como informe completo. La referencia valida procedencia, no interpretación semántica: el borrador requiere revisión humana.

## Distribución y estado de aceptación
.NET autónomo win-x64 e Inno Setup por usuario; La edición completa incorpora Whisper/Ollama y modelos bajo AI, junto a CRT app-local. LocalOllamaHost inicia un servidor privado loopback en 11435 con el almacén de modelos incluido, deshabilita cloud y GPU para el perfil CPU y detiene solo el proceso de su propiedad. Configuraciones externas previas se conservan hasta elegir «Usar motores incluidos». GitHub Actions prueba instalación, arranque/render WPF, conversión de audio y desinstalación. Las pruebas de contratos IA usan fixtures controlados. La captura real y la calidad con modelos reales siguen pendientes de aceptación. A solicitud expresa del usuario se avanzó con IA mientras se mantiene visible esa condición pendiente.
