using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Traduce el estado de <see cref="PlcFanLogic"/> a las señales que leen los símbolos
    /// del diagrama.
    ///
    /// Es la única pieza que conoce a las dos partes, y va en un solo sentido: el diagrama
    /// pregunta, el PLC responde. Nunca al revés. Si el diagrama pudiera escribir en el PLC,
    /// dejaría de ser un reflejo de la lógica y pasaría a ser una segunda implementación de
    /// la misma lógica, que es justo lo que no se quiere mantener.
    ///
    /// C# puro, así que el diagrama entero se puede comprobar en EditMode recorriendo la
    /// secuencia del ventilador y mirando qué peldaños se encienden.
    /// </summary>
    public sealed class VdfEstadoLadder
    {
        public PlcFanLogic Plc;

        /// <summary>
        /// Cuánto dura el destello de un pulsador en el diagrama, en segundos.
        ///
        /// Los pulsadores son momentáneos: en el PLC real el contacto se cierra unas décimas
        /// y vuelve. Si el diagrama lo reflejara literalmente, el alumno nunca llegaría a ver
        /// el contacto cerrado. Se alarga a propósito, que es lo que hacen los visores de
        /// Ladder de verdad.
        /// </summary>
        public const float DuracionDestello = 0.45f;

        private float _reloj;
        private float _tStart = float.NegativeInfinity;
        private float _tStop = float.NegativeInfinity;
        private float _tSubir = float.NegativeInfinity;
        private float _tBajar = float.NegativeInfinity;

        public VdfEstadoLadder(PlcFanLogic plc = null)
        {
            Plc = plc;
        }

        /// <summary>Avanza el reloj interno de los destellos.</summary>
        public void Tick(float dt)
        {
            if (dt > 0f) _reloj += dt;
        }

        // --- Avisos de pulsación -----------------------------------------------------------
        //
        // Los llama quien acciona los pulsadores. No se deducen del PLC porque
        // PlcFanLogic trata los pulsadores por flanco: una llamada es una pulsación y no
        // queda rastro de ella en su estado, que es lo correcto para la lógica pero deja al
        // diagrama sin nada que enseñar.

        public void AvisarStart() { _tStart = _reloj; }
        public void AvisarStop() { _tStop = _reloj; }
        public void AvisarSubir() { _tSubir = _reloj; }
        public void AvisarBajar() { _tBajar = _reloj; }

        private bool Destellando(float instante)
        {
            return _reloj - instante <= DuracionDestello;
        }

        /// <summary>Valor de una señal ahora mismo.</summary>
        public bool Leer(VdfSenalPlc senal, int parametro = 0)
        {
            switch (senal)
            {
                case VdfSenalPlc.Siempre:
                    return true;

                case VdfSenalPlc.PulsadorStart: return Destellando(_tStart);
                case VdfSenalPlc.PulsadorStop: return Destellando(_tStop);
                case VdfSenalPlc.PulsadorSubir: return Destellando(_tSubir);
                case VdfSenalPlc.PulsadorBajar: return Destellando(_tBajar);
            }

            if (Plc == null) return false;

            switch (senal)
            {
                case VdfSenalPlc.Habilitado: return Plc.Habilitado;
                case VdfSenalPlc.Emergencia: return Plc.EmergenciaActiva;
                case VdfSenalPlc.NivelExacto: return Plc.NivelActivo == parametro;
                case VdfSenalPlc.NivelMayorIgual: return Plc.NivelActivo >= parametro;
                case VdfSenalPlc.CambioEnCurso: return Plc.CambioEnCurso;
                case VdfSenalPlc.SalidaS1: return Plc.SalidaS1;
                case VdfSenalPlc.SalidaS5: return Plc.SalidaS5;
                case VdfSenalPlc.SalidaS6: return Plc.SalidaS6;
                case VdfSenalPlc.SalidaV: return Plc.SalidaV(parametro);
                case VdfSenalPlc.InterruptorMarcha: return Plc.InterruptorMarcha;
                case VdfSenalPlc.InterruptorS5: return Plc.InterruptorS5;
                case VdfSenalPlc.InterruptorS6: return Plc.InterruptorS6;
                case VdfSenalPlc.MarchaBloqueada: return Plc.MarchaBloqueadaTrasEmergencia;
            }

            return false;
        }

        // --- Valores que se dibujan dentro de los bloques -----------------------------------

        /// <summary>Segundos que faltan para que se aplique el cambio de nivel.</summary>
        public float TiempoRestante
        {
            get { return Plc != null ? Plc.TiempoRestanteCambio : 0f; }
        }

        /// <summary>Texto del temporizador, como lo mostraría un visor de Ladder.</summary>
        public string TextoTemporizador()
        {
            if (Plc == null) return "0.0 / 3.0 s";

            float transcurrido = Plc.CambioEnCurso
                ? PlcFanLogic.RetardoCambioNivelSegundos - Plc.TiempoRestanteCambio
                : 0f;

            return transcurrido.ToString("0.0") + " / " +
                   PlcFanLogic.RetardoCambioNivelSegundos.ToString("0.0") + " s";
        }

        /// <summary>
        /// Texto del contador de niveles.
        ///
        /// Muestra el nivel PEDIDO, no el aplicado, porque eso es lo que vale el contador
        /// del Ladder original: la cuenta (MW2) sube en cuanto se pulsa, y lo que espera los
        /// 3 s es la salida VELOCIDAD_n, no la cuenta. Mientras el temporizador corre, el
        /// contador ya marca el destino y el motor está parado.
        /// </summary>
        public string TextoContador()
        {
            if (Plc == null) return "0 / " + PlcFanLogic.NivelMaximo;

            string s = Plc.NivelPendiente + " / " + PlcFanLogic.NivelMaximo;
            if (Plc.CambioEnCurso) s += "  (temporizando)";
            return s;
        }

        /// <summary>Fracción del retardo ya transcurrida, de 0 a 1. Para dibujar una barra.</summary>
        public float ProgresoTemporizador()
        {
            if (Plc == null || !Plc.CambioEnCurso) return 0f;

            float t = PlcFanLogic.RetardoCambioNivelSegundos;
            if (t <= 0f) return 1f;
            return Mathf.Clamp01((t - Plc.TiempoRestanteCambio) / t);
        }
    }
}
