# Informe ejecutivo 0.3.0 — implementación y revisión
Estado: integrado mediante Ollama local; pendiente de evaluación con modelos y reuniones reales en el equipo de destino.

## Flujo
Con la opción automática activada: fin de reunión → terminar transcripción pendiente de Whisper.cpp → consultar Ollama → guardar informe en SQLite → exportar report.html/report.json y transcript.txt/transcript.json. La falta de motores, una salida inválida o una cancelación conservan audio y segmentos completados y muestran un estado reanudable.

## Contenido
Resumen ejecutivo con redacción propia; temas, acuerdos, acciones y responsables/fechas cuando sean explícitos; asuntos pendientes, riesgos y cobertura. Los puntos estructurados incluyen referencias a IDs reales de segmentos. No se identifica automáticamente a personas por su voz ni se asigna un responsable por simple proximidad de frases. El modelo recibe instrucciones para distinguir propuestas y acuerdos, pero esto debe revisarse.

La ventana muestra resumen, contadores y acciones. El HTML exportado contiene todos los temas y evidencias, diseño responsive, impresión A4 y botón para imprimir/guardar PDF desde el navegador. La transcripción de referencia se despliega por separado dentro del HTML; se excluye de la impresión ejecutiva para que el informe no sea una repetición literal del audio.

## Reuniones largas
Se analizan lotes acotados. Las notas de cada lote se conservan y el resumen se consolida jerárquicamente. No se envía toda una reunión prolongada en un solo prompt ni se presenta una salida truncada como completa. «Todos los temas» describe el objetivo de cobertura, no una garantía de ausencia de omisiones. Revisar tramos inaudibles, silencios, errores de reconocimiento y posibles interpretaciones incorrectas.

## Correcciones
Editar texto en Transcripción y guardar conserva el original y registra revisiones. Los informes anteriores permanecen en SQLite y aparecen como desactualizados si hay correcciones posteriores. Regenerar incorpora el texto revisado. Toda salida IA permanece como borrador; esta versión no incorpora firma/aprobación electrónica del acta.
