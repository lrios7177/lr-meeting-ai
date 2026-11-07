# Informe ejecutivo — especificación pendiente de integración
Estado: DISEÑADO, NO IMPLEMENTADO. La IA se integrará después de validar captura doble y sincronización en Windows.

## Propósito
Presentar lo discutido de forma objetiva, breve y con redacción propia. No copiar la transcripción ni convertirla automáticamente en hechos comprobados. Una opinión de un participante debe seguir atribuida como opinión; una propuesta no es un acuerdo.

## Contenido
1. Encabezado: título, fecha, duración, participantes confirmados y estado del informe (borrador o revisado).
2. Resumen ejecutivo: contexto, objetivo de la reunión y conclusiones principales, sin relleno ni repetición literal.
3. Temas tratados: todos los puntos sustantivos, agrupados por tema, con contexto, resultado y referencia al audio/transcripción. Distinguir lo discutido de lo no resuelto.
4. Decisiones y acuerdos: texto concreto, evidencia y condiciones. Si no se identifican, indicarlo sin inventar.
5. Tareas: acción, responsable y fecha solo si hay evidencia; de lo contrario, «por confirmar». Distinguir fecha explícita de fecha interpretada y pedir revisión.
6. Riesgos, bloqueos y asuntos pendientes; próximos pasos.
7. Cobertura y revisión: tramos sin transcribir, audio no audible, correcciones y enlaces a tiempos de origen. No afirmar «todos los puntos» si falta cobertura.

## Estilo visual previsto
HTML imprimible y exportación PDF posterior: tipografía clara, márgenes amplios, encabezado sobrio, tarjetas de resumen, tabla de acciones y marcadores de estado. Incluir la autoría Ing. Luis Ríos, MBA. El usuario podrá revisar, corregir y aprobar el borrador antes de exportar.

## Procesamiento local previsto
Whisper.cpp transcribirá cada canal por bloques con tiempos, sin detener la grabación si se retrasa. Los originales se conservarán; las correcciones tendrán revisiones separadas. SQLite guardará resultados incrementales.

Ollama procesará porciones que quepan en el contexto del modelo y producirá notas estructuradas con referencias a segmentos. Una segunda etapa reunirá notas por tema y eliminará repeticiones sin eliminar decisiones distintas. El informe final se contrastará con el índice de temas y la cobertura de segmentos. Reuniones largas no se enviarán como un único prompt que pueda truncarse. Los audios y transcripciones no se enviarán a servicios de transcripción externos ni APIs de pago.

El contenido transcrito es evidencia, nunca instrucciones para el motor. Los fallos del modelo deben ser visibles y permitir reintentos. La calidad dependerá del audio, del modelo y del hardware; no se garantiza transcripción perfecta ni identificación de hablantes por voz.
