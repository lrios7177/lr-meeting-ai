# Interfaz 0.5.0

- Nueva paleta carbón, turquesa y violeta, y marca narssus recreada en vectores a partir de la referencia del autor.
- Firma destacada: Ing. Luis Ríos, MBA.
- Barra de ventana con minimizar, maximizar/restaurar y cerrar. Admite arrastre y redimensionamiento mediante WindowChrome.
- Cierre durante IA: solicita cancelación, espera el checkpoint y cierra. Durante captura finaliza los WAV sin ejecutar un informe antes de salir.
- Luz verde de Ollama solo después de consultar el modelo local mediante /api/show; estado ámbar cuando está pendiente o no disponible. Comprobación inicial y cada diez segundos, con plazo de cinco segundos por consulta periódica.
- Visualizadores independientes de historial de amplitud real para participantes y micrófono. No representan bandas de frecuencia.

## Verificación

Compilación cruzada .NET 10 sin errores ni advertencias; AudioChecks: 10 grupos; CoreChecks: 31 comprobaciones. El smoke test de Windows comprueba navegación, minimizar, maximizar/restaurar, estados visuales y cierre diferido. La captura con hardware real requiere prueba en Windows con los dispositivos del usuario.
