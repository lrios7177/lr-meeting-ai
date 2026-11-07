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
