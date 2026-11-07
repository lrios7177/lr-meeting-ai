# Puerta de aceptación: captura doble
Registrar versión de Windows, hardware, versión de app, fecha, dispositivos y resultado por caso. No aprobar IA hasta completar estos casos y corregir defectos.

1. Elegir explícitamente salida y micrófono. Repetir con audífonos analógicos de 3.5 mm: seleccionar el endpoint de reproducción que realmente usa Windows; no se requiere un dispositivo «Stereo Mix».
2. Reproducir voz en la salida seleccionada y hablar por el micrófono durante la prueba de 30 s. Confirmar movimiento independiente de medidores y parada automática.
3. Escuchar A y B por separado: A contiene participantes; B contiene voz propia. Algo de filtración acústica es posible. Usar audífonos para reducir eco. No reproducir pruebas durante captura.
4. Repetir con silencio en A, en B y en ambos. Debe mostrar advertencia sin afirmar que hay fallo de hardware. Loopback puede no enviar buffers durante silencio.
5. Desconectar micrófono USB y salida USB durante la prueba. Debe detener, mostrar motivo y guardar diagnóstico. El jack analógico depende de lo que el controlador notifique: comprobar físicamente el comportamiento.
6. Cambiar dispositivo predeterminado: verificar que no cambia la fuente elegida. Detener, actualizar lista y elegir otra fuente explícitamente.
7. Denegar acceso al micrófono en privacidad de Windows. Confirmar error visible y diagnóstico; rehabilitar permiso y repetir.
8. Grabar y detener manualmente. Cerrar la ventana grabando; comprobar finalización de ambos WAV. Simular falta de espacio y comprobar error explícito.
9. Repetir inicio/parada diez veces y reproducir ambos WAV tras cada ciclo. No debe quedar dispositivo bloqueado.
10. Probar 60 minutos con pulsos comunes de referencia al comienzo y al final; medir offsets y deriva. La sincronización inicial es aproximada: registrar desviación antes de decidir el tratamiento para IA.

Conservar capture.jsonl y diagnostics.log junto con el resultado. Revisar privacidad antes de compartirlos. Estado inicial de todos los casos: PENDIENTE, no probado en Windows.

11. Grabar hasta superar 1 GiB por canal: verificar rotación sin pérdidas ni duplicaciones y reproducción consecutiva de todas las partes. Esta prueba debe completarse en Windows antes de anunciar soporte probado de reuniones largas.
12. Guardar el paquete único y abrirlo con una herramienta ZIP64 compatible. Comprobar canales, partes, capture.jsonl y meeting.json. Simular poco espacio al empaquetar: debe conservar los WAV originales y no dejar un paquete final incompleto.

## Aceptación IA e interfaz 0.3.0 (equipo real, pendiente)
13. Instalar Whisper.cpp y un modelo multilingüe real; configurar Ollama con un modelo local. Probar español e inglés por separado. Verificar que un ejecutable incompatible, modelo ausente y Ollama detenido dejan mensajes claros y audio intacto.
14. Grabar más de 60 segundos con voz en ambos canales. Confirmar segmentos progresivos mientras graba y que el audio continúa si Whisper es más lento que tiempo real. Al terminar, verificar que se procesan todos los bloques pendientes antes del informe.
15. Pausar 20 segundos y reanudar. Confirmar tomas independientes, ausencia de sobrescritura y marcas de tiempo que preservan el hueco de pared. Comparar contra referencia real para medir error y deriva.
16. Cancelar IA durante una transcripción y durante Ollama. La captura activa debe seguir. Detener y reanudar desde Historial; comprobar que no se duplican los bloques ya confirmados.
17. Importar WAV y MP3 compatibles. Corregir un nombre en el texto, guardar, verificar que el informe anterior se marca desactualizado y regenerar. Confirmar persistencia tras cerrar/abrir la app.
18. Reunión de referencia con acuerdos, propuestas no aceptadas, tareas sin responsable y fechas ambiguas: evaluar que el informe sea objetivo, use redacción propia, conserve temas de inicio/final y no invente responsables. Abrir referencias y corregir errores antes de compartir.
19. Exportar HTML y PDF mediante el navegador; comprobar caracteres españoles, diseño legible y que el PDF ejecutivo no repite toda la transcripción. Empaquetar y verificar los dos canales, tiempos y últimos resultados.
20. Revisar la interfaz a 100%, 125% y 150% de escala de Windows, con teclado y lectores de pantalla habituales. La prueba automatizada de renderizado no sustituye estas verificaciones de usabilidad.

Las capturas del artefacto interface-preview corresponden a un modo de prueba explícito sin grabación ni inferencia real; no se usan como evidencia de captura de hardware o calidad IA.
