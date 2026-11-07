# Validación de la entrega inicial
- SDK utilizado: .NET 10.0.403, compilación cruzada desde Linux.
- `dotnet build -c Release`: correcto, cero advertencias y cero errores.
- `dotnet run --project tests/LRMeeting.AudioChecks -c Release`: 8 comprobaciones correctas (PCM16, PCM24, PCM8, float, float extensible, NaN y muestra incompleta). Estas pruebas solo validan el medidor.
- Publicación `win-x64 --self-contained true`: correcta. Se generó LRMeetingAI.exe con dependencias y runtime en artifacts/publish; no se ejecutó en este entorno.
- Inno Setup: definición y script disponibles, instalador no generado ni ejecutado.
- WPF, WASAPI, hardware y sincronización: pendientes de protocolo docs/PRUEBA-WINDOWS.md.
- No hay integración IA ni pruebas de transcripción: se mantiene la puerta de captura solicitada.
