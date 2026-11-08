# Corrección 0.6.1 · Controles nativos de ventana

Se sustituye la barra personalizada WindowChrome por SingleBorderWindow. Windows vuelve a proporcionar directamente minimizar, maximizar/restaurar, cerrar, Alt+F4 y el menú del sistema. El diseño del estudio y el logo original permanecen dentro del área de contenido.

La prueba anterior activaba eventos Click de WPF por código: no comprobaba cómo Windows reconocía el área bajo el puntero. La nueva prueba comprueba los estilos de ventana, consulta las coordenadas reales de sus botones, verifica WM_NCHITTEST mediante DwmDefWindowProc y el procedimiento de ventana y envía WM_SYSCOMMAND para minimizar, maximizar/restaurar y cerrar. También confirma que un cierre durante procesamiento queda solicitado hasta que pueda conservarse el estado.

La limpieza del Ollama iniciado por la aplicación se ejecuta fuera del hilo de la interfaz para que el plazo de terminación del proceso no bloquee la ventana. La captura se finaliza antes del cierre; la IA se cancela y puede reanudarse desde Historial.
