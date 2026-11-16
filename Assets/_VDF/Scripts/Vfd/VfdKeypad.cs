using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>Las ocho teclas del operador digital del V1000.</summary>
    public enum VfdKey
    {
        Esc,
        Arriba,
        Abajo,
        LoRe,
        /// <summary>Tecla >/RESET: mueve el dígito en edición y resetea fallas.</summary>
        Reset,
        Enter,
        Run,
        Stop
    }

    /// <summary>Dónde está el usuario dentro del menú.</summary>
    public enum VfdKeypadState
    {
        /// <summary>Modo Drive: monitor de frecuencia.</summary>
        Monitor,
        /// <summary>Recorriendo los modos (-drv-, -PAr-, ...).</summary>
        SeleccionModo,
        /// <summary>Dentro de PAr, eligiendo qué parámetro.</summary>
        SeleccionParametro,
        /// <summary>Editando el valor dígito a dígito.</summary>
        Edicion,
        /// <summary>Mostrando un mensaje temporal (End, oPE, Err).</summary>
        Mensaje
    }

    /// <summary>
    /// Operador digital del V1000: pantalla de 5 dígitos y navegación por menús.
    ///
    /// C# puro, avanza con <see cref="Tick"/>. Toda la navegación se puede probar en
    /// EditMode pulsando teclas y leyendo <see cref="TextoPantalla"/>, sin escena.
    ///
    /// ⚠️ Alcance: de los modos del V1000 real solo están implementados **-drv-** (monitor)
    /// y **-PAr-** (parámetros), que son los que pide la práctica. Los otros tres aparecen
    /// en el recorrido de ▲/▼ para que la navegación se parezca a la del equipo, pero al
    /// entrar avisan de que no están simulados en vez de fingir que funcionan.
    /// </summary>
    public sealed class VfdKeypad
    {
        /// <summary>La pantalla del V1000 tiene 5 dígitos.</summary>
        public const int Digitos = 5;

        /// <summary>Cuánto se muestra "End" tras guardar, en segundos.</summary>
        public const float DuracionMensaje = 1.0f;

        /// <summary>Periodo de parpadeo del dígito en edición, en segundos.</summary>
        public const float PeriodoParpadeo = 0.5f;

        /// <summary>
        /// Modos del menú principal. Solo Drive y Parametros hacen algo; el resto están
        /// para que el recorrido con ▲/▼ se parezca al del equipo real.
        /// </summary>
        public static readonly string[] Modos = { "-drv-", "-PAr-", "StUP", "A.tUn", "vErF" };
        public const int ModoDrive = 0;
        public const int ModoParametros = 1;

        private readonly VfdModel _vfd;

        private int _modo = ModoDrive;
        private int _indiceParametro;
        private int _cursor;          // 0 = dígito más a la izquierda del campo en edición
        private int _anchoCampo;
        private long _valorEnEdicion; // valor * 10^decimales, como entero
        private float _tiempoMensaje;
        private float _reloj;
        private string _mensaje;
        private VfdKeypadState _estadoTrasMensaje;

        public VfdKeypadState Estado { get; private set; }

        /// <summary>Parámetros en los que se ha entrado con ENTER desde el último olvido.</summary>
        public readonly System.Collections.Generic.HashSet<string> ParametrosVistos =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>Borra la lista de parámetros vistos. Lo hace la práctica al empezar.</summary>
        public void OlvidarVistos() { ParametrosVistos.Clear(); }

        /// <summary>Índice del modo resaltado mientras se recorren con ▲/▼.</summary>
        public int ModoSeleccionado { get { return _modo; } }

        /// <summary>Parámetro sobre el que está el cursor dentro de PAr.</summary>
        public VfdParameterDef ParametroActual
        {
            get
            {
                var todos = VfdParameterTable.Todos;
                if (todos.Count == 0) return null;
                return todos[Mathf.Clamp(_indiceParametro, 0, todos.Count - 1)];
            }
        }

        public VfdKeypad(VfdModel vfd)
        {
            _vfd = vfd;
            Estado = VfdKeypadState.Monitor;
        }

        /// <summary>
        /// Devuelve el operador a reposo: pantalla de monitor, sin menu abierto y sin ninguna
        /// edicion a medias.
        ///
        /// Hace falta porque el teclado tiene estado propio que NO vive en el modelo del
        /// variador, y al cambiar de practica ese estado sobrevivia. El ataque que lo destapo
        /// es exactamente lo que hace un alumno: se pone a teclear un parametro, lo deja a
        /// medias sin pulsar ENTER, se aburre y abre otra practica. El operador seguia en
        /// modo edicion mostrando los digitos de la practica anterior, asi que la siguiente
        /// empezaba con el teclado en un estado que el alumno no habia pedido y que la guia
        /// no mencionaba. Y lo peor: la siguiente tecla que pulsara podia confirmar aquel
        /// valor a medias sobre el parametro equivocado.
        /// </summary>
        public void VolverAReposo()
        {
            Estado = VfdKeypadState.Monitor;
            _modo = ModoDrive;
            _indiceParametro = 0;
            _cursor = 0;
            _anchoCampo = 0;
            _valorEnEdicion = 0;
            _mensaje = null;
            _tiempoMensaje = 0f;
            _estadoTrasMensaje = VfdKeypadState.Monitor;
        }

        // =====================================================================================
        // Pantalla
        // =====================================================================================

        /// <summary>Lo que se lee en la pantalla LED ahora mismo, ya formateado a 5 dígitos.</summary>
        public string TextoPantalla
        {
            get
            {
                switch (Estado)
                {
                    case VfdKeypadState.Monitor:
                        return TextoMonitor();

                    case VfdKeypadState.SeleccionModo:
                        return Modos[_modo];

                    case VfdKeypadState.SeleccionParametro:
                        return ParametroActual != null ? ParametroActual.Id : "-----";

                    case VfdKeypadState.Edicion:
                        return TextoEdicion();

                    case VfdKeypadState.Mensaje:
                        return _mensaje;
                }
                return "-----";
            }
        }

        /// <summary>
        /// En modo Drive el V1000 muestra la frecuencia de salida precedida de F.
        /// Si hay falla, muestra el código en vez de la frecuencia.
        /// </summary>
        private string TextoMonitor()
        {
            if (_vfd.Falla != VfdFault.Ninguna) return CodigoDeFalla(_vfd.Falla);
            return "F" + _vfd.FrecuenciaSalidaHz.ToString("00.00",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string CodigoDeFalla(VfdFault falla)
        {
            switch (falla)
            {
                case VfdFault.BajoVoltaje: return "Uv1";
                case VfdFault.SobrecargaMotor: return "oL1";
                case VfdFault.ParadaEmergencia: return "EF0";
                default: return "-----";
            }
        }

        private string TextoEdicion()
        {
            var def = ParametroActual;
            if (def == null) return "-----";

            string digitos = System.Math.Abs(_valorEnEdicion).ToString().PadLeft(_anchoCampo, '0');

            if (def.Decimales <= 0) return digitos;

            int corte = digitos.Length - def.Decimales;
            return digitos.Substring(0, corte) + "." + digitos.Substring(corte);
        }

        /// <summary>Índice del dígito que parpadea, contando solo dígitos (sin el punto). -1 si no hay.</summary>
        public int CursorEdicion { get { return Estado == VfdKeypadState.Edicion ? _cursor : -1; } }

        /// <summary>El dígito en edición está en su fase visible del parpadeo.</summary>
        public bool CursorVisible
        {
            get { return Estado != VfdKeypadState.Edicion || (_reloj % PeriodoParpadeo) < PeriodoParpadeo * 0.5f; }
        }

        // --- Indicadores del operador ------------------------------------------------------
        public bool LedAlm { get { return _vfd.LedAlm; } }
        public bool LedRev { get { return _vfd.LedRev; } }
        public bool LedDrv { get { return Estado == VfdKeypadState.Monitor && _vfd.Alimentado; } }
        public bool LedFout { get { return Estado == VfdKeypadState.Monitor; } }

        /// <summary>LO/RE encendido = modo LOCAL (manda el teclado).</summary>
        public bool LedLocal { get { return _vfd.ModoLocal; } }

        // =====================================================================================
        // Tiempo
        // =====================================================================================

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _reloj += dt;

            if (Estado != VfdKeypadState.Mensaje) return;

            _tiempoMensaje -= dt;
            if (_tiempoMensaje <= 0f)
            {
                Estado = _estadoTrasMensaje;
                _tiempoMensaje = 0f;
            }
        }

        private void MostrarMensaje(string texto, VfdKeypadState despues)
        {
            _mensaje = texto;
            if (despues == VfdKeypadState.Mensaje)
                despues = Estado == VfdKeypadState.Mensaje ? _estadoTrasMensaje : VfdKeypadState.Monitor;
            _estadoTrasMensaje = despues;
            _tiempoMensaje = DuracionMensaje;
            Estado = VfdKeypadState.Mensaje;
        }

        // =====================================================================================
        // Teclas
        // =====================================================================================

        public void Pulsar(VfdKey tecla)
        {
            // RUN, STOP y LO/RE funcionan desde cualquier pantalla, como en el equipo real.
            switch (tecla)
            {
                case VfdKey.Run: PulsarRun(); return;
                case VfdKey.Stop: PulsarStop(); return;
                case VfdKey.LoRe: PulsarLoRe(); return;
            }

            if (Estado == VfdKeypadState.Mensaje)
            {
                // Cualquier tecla corta el mensaje.
                Estado = _estadoTrasMensaje;
                _tiempoMensaje = 0f;
                return;
            }

            switch (Estado)
            {
                case VfdKeypadState.Monitor: EnMonitor(tecla); break;
                case VfdKeypadState.SeleccionModo: EnSeleccionModo(tecla); break;
                case VfdKeypadState.SeleccionParametro: EnSeleccionParametro(tecla); break;
                case VfdKeypadState.Edicion: EnEdicion(tecla); break;
            }
        }

        private void PulsarRun()
        {
            // El V1000 solo arranca desde el teclado si la orden de marcha es del operador.
            if (!_vfd.ModoLocal && _vfd.ValorEntero("b1-02") != 0)
            {
                MostrarMensaje("rE", Estado); // rE = la orden viene de REMOTE
                return;
            }
            _vfd.MarchaDesdeOperador = true;
        }

        private void PulsarStop()
        {
            _vfd.MarchaDesdeOperador = false;
        }

        private void PulsarLoRe()
        {
            // En el equipo real, LO/RE solo se admite con el variador parado.
            if (_vfd.Estado != VfdState.Detenido && _vfd.Estado != VfdState.Falla)
            {
                MostrarMensaje("oPE", Estado);
                return;
            }

            _vfd.ModoLocal = !_vfd.ModoLocal;
            _vfd.MarchaDesdeOperador = false;
        }

        private void EnMonitor(VfdKey tecla)
        {
            switch (tecla)
            {
                case VfdKey.Arriba:
                case VfdKey.Abajo:
                    // Salir del monitor al recorrido de modos.
                    _modo = ModoDrive;
                    Estado = VfdKeypadState.SeleccionModo;
                    AvanzarModo(tecla == VfdKey.Arriba ? 1 : -1);
                    break;

                case VfdKey.Reset:
                    // >/RESET borra la falla si la causa ya no está.
                    if (_vfd.Falla != VfdFault.Ninguna && !_vfd.ResetFalla())
                        MostrarMensaje("Err", VfdKeypadState.Monitor);
                    break;

                case VfdKey.Enter:
                case VfdKey.Esc:
                    break;
            }
        }

        private void EnSeleccionModo(VfdKey tecla)
        {
            switch (tecla)
            {
                case VfdKey.Arriba: AvanzarModo(1); break;
                case VfdKey.Abajo: AvanzarModo(-1); break;

                case VfdKey.Enter:
                    if (_modo == ModoDrive)
                    {
                        Estado = VfdKeypadState.Monitor;
                    }
                    else if (_modo == ModoParametros)
                    {
                        _indiceParametro = 0;
                        Estado = VfdKeypadState.SeleccionParametro;
                    }
                    else
                    {
                        // Modo real del equipo, pero no simulado: se dice, no se finge.
                        MostrarMensaje("n.SIM", VfdKeypadState.SeleccionModo);
                    }
                    break;

                case VfdKey.Esc:
                    Estado = VfdKeypadState.Monitor;
                    break;
            }
        }

        private void AvanzarModo(int paso)
        {
            _modo = ((_modo + paso) % Modos.Length + Modos.Length) % Modos.Length;
        }

        private void EnSeleccionParametro(VfdKey tecla)
        {
            var todos = VfdParameterTable.Todos;

            switch (tecla)
            {
                case VfdKey.Arriba:
                    _indiceParametro = (_indiceParametro + 1) % todos.Count;
                    break;

                case VfdKey.Abajo:
                    _indiceParametro = (_indiceParametro - 1 + todos.Count) % todos.Count;
                    break;

                case VfdKey.Enter:
                    IniciarEdicion();
                    break;

                case VfdKey.Esc:
                    _modo = ModoParametros;
                    Estado = VfdKeypadState.SeleccionModo;
                    break;
            }
        }

        /// <summary>
        /// Pasa el valor actual a un entero de ancho fijo para poder editarlo dígito a
        /// dígito, igual que el teclado real. El ancho sale del máximo del parámetro.
        /// </summary>
        private void IniciarEdicion()
        {
            var def = ParametroActual;
            if (def == null) return;

            long escala = Escala(def.Decimales);
            _valorEnEdicion = (long)Mathf.Round(_vfd.Valor(def.Id) * escala);

            long maximo = (long)Mathf.Round(def.Max * escala);
            _anchoCampo = Mathf.Clamp(maximo.ToString().Length, 1, Digitos);

            _cursor = _anchoCampo - 1; // el equipo empieza por el dígito menos significativo
            Estado = VfdKeypadState.Edicion;

            // Queda apuntado que el alumno ENTRÓ a ver este parámetro: lo piden los pasos de
            // «entra a ver X y confirma». Ver VdfCondicion.ParametroRevisado.
            ParametrosVistos.Add(def.Id);

            if (_vfd.Estado != VfdState.Detenido && _vfd.Estado != VfdState.Falla
                && !SeCambiaEnMarcha(def.Id))
            {
                MostrarMensaje("oPE", VfdKeypadState.SeleccionParametro);
                return;
            }
        }

        /// <summary>¿Se puede cambiar este parámetro con el motor girando?</summary>
        public static bool SeCambiaEnMarcha(string id)
        {
            return id != null && (id.StartsWith("d1-") || id.StartsWith("C1-"));
        }

        private static long Escala(int decimales)
        {
            long e = 1;
            for (int i = 0; i < decimales; i++) e *= 10;
            return e;
        }

        private void EnEdicion(VfdKey tecla)
        {
            switch (tecla)
            {
                case VfdKey.Reset:
                    // Mueve el dígito en edición, dando la vuelta al llegar al final.
                    _cursor = (_cursor + 1) % _anchoCampo;
                    break;

                case VfdKey.Arriba: CambiarDigito(+1); break;
                case VfdKey.Abajo: CambiarDigito(-1); break;

                case VfdKey.Enter: Guardar(); break;

                case VfdKey.Esc:
                    Estado = VfdKeypadState.SeleccionParametro;
                    break;
            }
        }

        /// <summary>
        /// Suma o resta 1 al dígito bajo el cursor, SIN arrastrar acarreo al de al lado.
        /// Es lo que hace el teclado real: cada dígito gira de 0 a 9 por separado.
        /// </summary>
        private void CambiarDigito(int delta)
        {
            int posicionDesdeLaDerecha = _anchoCampo - 1 - _cursor;
            long peso = 1;
            for (int i = 0; i < posicionDesdeLaDerecha; i++) peso *= 10;

            long digito = (_valorEnEdicion / peso) % 10;
            long nuevo = (digito + delta + 10) % 10;

            _valorEnEdicion += (nuevo - digito) * peso;
        }

        private void Guardar()
        {
            var def = ParametroActual;
            if (def == null) return;

            float valor = (float)_valorEnEdicion / Escala(def.Decimales);

            string error;
            if (!_vfd.EscribirParametro(def.Id, valor, out error))
            {
                // oPE = Operator Programming Error, como en el equipo real.
                MostrarMensaje("oPE", VfdKeypadState.Edicion);
                return;
            }

            MostrarMensaje("End", VfdKeypadState.SeleccionParametro);
        }

        // =====================================================================================
        // Utilidades para la vista y los tests
        // =====================================================================================

        /// <summary>Coloca el cursor directamente sobre un parámetro. Para la guía de prácticas.</summary>
        public bool IrAParametro(string id)
        {
            var todos = VfdParameterTable.Todos;
            for (int i = 0; i < todos.Count; i++)
            {
                if (todos[i].Id != id) continue;

                _indiceParametro = i;
                _modo = ModoParametros;
                Estado = VfdKeypadState.SeleccionParametro;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Teclea un valor completo en el parámetro actual usando solo las teclas reales.
        /// Sirve para comprobar en los tests que la navegación por dígitos basta para
        /// llegar a cualquier valor, sin atajos.
        /// </summary>
        public List<VfdKey> SecuenciaParaValor(float objetivo)
        {
            var secuencia = new List<VfdKey>();
            var def = ParametroActual;
            if (def == null || Estado != VfdKeypadState.Edicion) return secuencia;

            long escala = Escala(def.Decimales);
            long destino = (long)Mathf.Round(objetivo * escala);
            string deseado = destino.ToString().PadLeft(_anchoCampo, '0');

            for (int pos = 0; pos < _anchoCampo; pos++)
            {
                // Llevar el cursor a `pos`.
                while (_cursor != pos) { secuencia.Add(VfdKey.Reset); Pulsar(VfdKey.Reset); }

                int peso = 1;
                for (int i = 0; i < _anchoCampo - 1 - pos; i++) peso *= 10;

                int actual = (int)((_valorEnEdicion / peso) % 10);
                int quiere = deseado[pos] - '0';

                while (actual != quiere)
                {
                    secuencia.Add(VfdKey.Arriba);
                    Pulsar(VfdKey.Arriba);
                    actual = (actual + 1) % 10;
                }
            }

            return secuencia;
        }
    }
}
