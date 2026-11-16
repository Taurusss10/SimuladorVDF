using System.Collections.Generic;

namespace VDF
{
    /// <summary>
    /// Todo lo que el evaluador necesita saber para juzgar un paso.
    ///
    /// Es C# puro y sin Unity a propósito, igual que <see cref="VfdModel"/> y
    /// <see cref="PlcFanLogic"/>: así el guion de las cinco prácticas se puede probar entero
    /// en EditMode, sin escena y sin Play Mode. Quien lo rellena desde la escena es
    /// <c>VdfPracticaRunner</c>.
    ///
    /// Los **logros** viven aquí y no en el evaluador porque son memoria del alumno, no
    /// estado del equipo: «haber subido hasta la velocidad 4» no se puede deducir mirando el
    /// variador en un instante dado.
    /// </summary>
    public sealed class VdfContextoPractica
    {
        // --- Estado del equipo --------------------------------------------------------------
        public VfdModel Vfd;
        public PlcFanLogic Plc;
        public VdfWiringGraph Grafo;
        public VdfWiringReport InformeCableado;

        // --- Reconocimiento (práctica 1) ----------------------------------------------------
        /// <summary>Bornes y componentes que el alumno ya ha señalado.</summary>
        public readonly HashSet<string> Identificados = new HashSet<string>();

        /// <summary>
        /// Qué componentes pertenecen a cada grupo del tablero. Lo rellena el runner desde el
        /// layout, para que la condición <see cref="VdfCondicion.GrupoIdentificado"/> no tenga
        /// que duplicar esa lista.
        /// </summary>
        public readonly Dictionary<string, List<string>> Grupos = new Dictionary<string, List<string>>();

        /// <summary>
        /// Avería provocada en esta sesión, si la hay. La práctica 5 la usa para saber qué
        /// tiene que diagnosticar y reparar el alumno.
        /// </summary>
        public VdfFallaDidactica FallaActiva;

        // --- Logros (práctica 4 y 5) --------------------------------------------------------
        public bool Arranco;
        public int NivelMaximoAlcanzado;
        public bool BajoHastaUno;
        public bool ParoDespuesDeOperar;
        public bool EmergenciaAccionada;
        public bool EmergenciaRearmada;
        public bool HuboFalla;

        public bool EmergenciaConMotorGirando;

        /// <summary>Velocidad más alta alcanzada desde que la avería quedó reparada.</summary>
        public int NivelMaximoTrasReparar;

        private float _frecuenciaPrevia;
        private bool _emergenciaPrevia;

        /// <summary>Se inicializó el variador desde el teclado durante la práctica.</summary>
        public bool VariadorInicializado;

        /// <summary>Parámetros en los que el alumno ha entrado con ENTER en el teclado.</summary>
        public readonly HashSet<string> ParametrosVistos = new HashSet<string>();

        /// <summary>
        /// Actualiza los logros a partir del estado actual. Hay que llamarlo cada frame: un
        /// logro que no se registre en el momento en que ocurre se pierde para siempre.
        /// </summary>
        public void ActualizarLogros()
        {
            // Se comprueba antes del return por Plc: una falla del variador puede darse sin
            // que haya PLC conectado (por ejemplo, disparada desde el teclado).
            if (Vfd != null && Vfd.Falla != VfdFault.Ninguna) HuboFalla = true;

            if (Plc == null) return;

            if (Plc.EmergenciaActiva && !_emergenciaPrevia && _frecuenciaPrevia > 1f)
                EmergenciaConMotorGirando = true;
            _emergenciaPrevia = Plc.EmergenciaActiva;
            _frecuenciaPrevia = Vfd != null ? Vfd.FrecuenciaSalidaHz : 0f;

            // Lo que se compruebe DESPUÉS de reparar. Mientras la avería siga, se reinicia.
            if (FallaActiva != null)
            {
                if (!FallaActiva.Reparada(Vfd, Grafo)) NivelMaximoTrasReparar = 0;
                else if (Plc.NivelActivo > NivelMaximoTrasReparar) NivelMaximoTrasReparar = Plc.NivelActivo;
            }

            if (Plc.Habilitado) Arranco = true;

            if (Plc.NivelActivo > NivelMaximoAlcanzado)
                NivelMaximoAlcanzado = Plc.NivelActivo;

            // Bajar solo cuenta si antes se llegó arriba del todo.
            if (NivelMaximoAlcanzado >= PlcFanLogic.NivelMaximo && Plc.Habilitado && Plc.NivelActivo == 1)
                BajoHastaUno = true;

            // Y parar solo cuenta si antes se estuvo operando.
            if (BajoHastaUno && !Plc.Habilitado && Plc.NivelActivo == 0)
                ParoDespuesDeOperar = true;

            if (Plc.EmergenciaActiva) EmergenciaAccionada = true;

            // Rearmar solo cuenta si antes se había accionado.
            if (EmergenciaAccionada && !Plc.EmergenciaActiva) EmergenciaRearmada = true;
        }

        /// <summary>Registra que el alumno ha señalado un componente.</summary>
        public void Identificar(string id)
        {
            if (!string.IsNullOrEmpty(id)) Identificados.Add(id);
        }

        /// <summary>Borra los logros y lo identificado, para volver a empezar la práctica.</summary>
        public void Reiniciar()
        {
            Identificados.Clear();
            Arranco = false;
            NivelMaximoAlcanzado = 0;
            BajoHastaUno = false;
            ParoDespuesDeOperar = false;
            EmergenciaAccionada = false;
            EmergenciaRearmada = false;
            HuboFalla = false;
            VariadorInicializado = false;
            EmergenciaConMotorGirando = false;
            NivelMaximoTrasReparar = 0;
            _frecuenciaPrevia = 0f;
            _emergenciaPrevia = false;
            ParametrosVistos.Clear();
        }
    }
}
