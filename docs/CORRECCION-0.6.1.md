# Corrección 0.6.1 · Controles nativos de ventana

Se sustituye la barra personalizada WindowChrome por SingleBorderWindow. Windows vuelve a proporcionar directamente minimizar, maximizar/restaurar, cerrar, Alt+F4 y el menú del sistema. El diseño del estudio y el logo original permanecen dentro del área de contenido.

La prueba anterior activaba eventos Click de WPF por código: no ejercitaba los controles nativos de Windows. La nueva prueba exige los estilos de ventana y busca los botones reales por su AutomationId nativo, comprueba su disponibilidad y ejecuta InvokePattern desde un hilo separado. Verifica minimizar, maximizar/restaurar y el cierre solicitado durante procesamiento. No se utilizan los manejadores Click de la barra anterior.

La ventana inicial se limita al área de trabajo disponible, ajustando también sus mínimos cuando la resolución o la escala de Windows dejan menos espacio. Así la barra nativa no queda fuera del escritorio al iniciar.

La limpieza del Ollama iniciado por la aplicación se ejecuta fuera del hilo de la interfaz para que el plazo de terminación del proceso no bloquee la ventana. La captura se finaliza antes del cierre; la IA se cancela y puede reanudarse desde Historial.
