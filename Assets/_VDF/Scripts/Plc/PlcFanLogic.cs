using UnityEngine;

namespace VDF
{
    public sealed class PlcFanLogic
    {
        /// <summary>Retardo entre que se pide un nivel y que se aplica (≈3 s según el enunciado).</summary>
        public const float RetardoCambioNivelSegundos = 3f;

        public const int NivelMinimo = 1;
        public const int NivelMaximo = 4;

        // --- Estado interno ---------------------------------------------------------------

        /// <summary>El sistema está habilitado (se pulsó START y no se ha pulsado STOP).</summary>
        public bool Habilitado { get; private set; }

        /// <summary>Nivel de velocidad realmente aplicado a las salidas. 0 = motor parado.</summary>
        public int NivelActivo { get; private set; }

        /// <summary>Nivel solicitado, aún dentro del retardo. Igual a <see cref="NivelActivo"/> si no hay cambio en curso.</summary>
        public int NivelPendiente { get; private set; }

        /// <summary>Segundos que faltan para que se aplique el nivel pendiente. 0 si no hay cambio en curso.</summary>
        public float TiempoRestanteCambio { get; private set; }

        /// <summary>Parada de emergencia enclavada. Se libera con <see cref="RearmarEmergencia"/>.</summary>
        public bool EmergenciaActiva { get; private set; }

        /// <summary>Hay un cambio de nivel esperando a que venza el retardo.</summary>
        public bool CambioEnCurso { get { return NivelPendiente != NivelActivo; } }

        public bool BajarDesdeNivel1Para { get; set; } = true;

        // --- Modo de mando ------------------------------------------------------------------

        /// <summary>
        /// Con qué se opera: los pulsadores del tablero (lo de siempre) o tres interruptores.
        /// En modo interruptores, START, STOP, SUBIR y BAJAR no hacen nada; la parada de
        /// emergencia sigue cortando igual, porque no depende del modo.
        /// </summary>
        public VdfModoMando Modo { get; private set; } = VdfModoMando.Pulsadores;

        public bool RetardoConInterruptores { get; set; } = true;

        /// <summary>Interruptor de marcha arriba (equivale a START; abajo, a STOP).</summary>
        public bool InterruptorMarcha { get; private set; }

        /// <summary>Interruptor S5 arriba: bit bajo de la velocidad.</summary>
        public bool InterruptorS5 { get; private set; }

        /// <summary>Interruptor S6 arriba: bit alto de la velocidad.</summary>
        public bool InterruptorS6 { get; private set; }

        /// <summary>
        /// Tras una emergencia, la marcha no vuelve sola aunque el interruptor siga arriba:
        /// hay que bajarlo y volverlo a subir. Es lo mismo que exige el modo pulsadores al
        /// pedir START otra vez, y es lo que pide cualquier norma de máquinas: rearmar la
        /// emergencia nunca puede arrancar el motor por sí solo.
        /// </summary>
        public bool MarchaBloqueadaTrasEmergencia { get; private set; }

        // --- Salidas del PLC ---------------------------------------------------------------

        /// <summary>
        /// Orden de marcha hacia el variador. Con pulsadores sale por Q0.7 hacia S1; con
        /// interruptores, por Q0.5 hacia S4 (ver <see cref="TerminalMarcha"/>).
        ///
        /// Con interruptores copia el interruptor de marcha sin retardo, como el peldaño
        /// I0.0 → Q0.5 del Ladder del laboratorio. La emergencia y el bloqueo tras ella los
        /// añade el simulador: el programa del laboratorio no tiene parada de emergencia.
        /// </summary>
        public bool SalidaS1
        {
            get
            {
                if (EmergenciaActiva) return false;
                if (Modo == VdfModoMando.Interruptores) return Habilitado;
                return NivelActivo >= NivelMinimo;
            }
        }

        /// <summary>Salida hacia S5: bit bajo de la selección multi-velocidad.</summary>
        public bool SalidaS5
        {
            get
            {
                // Laboratorio: I0.1 → Q0.6 directo, sin retardo y sin depender de la marcha.
                if (Modo == VdfModoMando.Interruptores) return !EmergenciaActiva && InterruptorS5;
                return SalidaS1 && (NivelActivo == 2 || NivelActivo == 4);
            }
        }

        /// <summary>Salida hacia S6: bit alto de la selección multi-velocidad.</summary>
        public bool SalidaS6
        {
            get
            {
                // Laboratorio: I0.2 → Q0.7 directo.
                if (Modo == VdfModoMando.Interruptores) return !EmergenciaActiva && InterruptorS6;
                return SalidaS1 && (NivelActivo == 3 || NivelActivo == 4);
            }
        }

        /// <summary>
        /// Terminal del variador al que llega la orden de marcha: S1 con pulsadores, S4 con
        /// interruptores (en el laboratorio Q0.5 va a S4 y se programa H1-04 = 40).
        /// </summary>
        public int TerminalMarcha { get { return Modo == VdfModoMando.Interruptores ? 4 : 1; } }

        /// <summary>Indicador luminoso V1..V4. Solo uno encendido a la vez, el del nivel activo.</summary>
        public bool SalidaV(int nivel)
        {
            if (nivel < NivelMinimo || nivel > NivelMaximo) return false;
            return !EmergenciaActiva && NivelActivo == nivel;
        }

        // =====================================================================================
        // Pulsadores (disparados por flanco: una llamada = una pulsación)
        // =====================================================================================

        public void PulsarStart()
        {
            if (Modo == VdfModoMando.Interruptores) return;
            if (EmergenciaActiva) return;
            Habilitado = true;
        }

        /// <summary>STOP: para el motor y reinicia la selección. Hay que volver a pulsar START.</summary>
        public void PulsarStop()
        {
            if (Modo == VdfModoMando.Interruptores) return;
            Parar();
        }

        /// <summary>Lo que hace STOP, sin mirar el modo. Lo usa también el interruptor de marcha.</summary>
        private void Parar()
        {
            Habilitado = false;
            NivelActivo = 0;
            NivelPendiente = 0;
            TiempoRestanteCambio = 0f;
        }

        public void PulsarSubir()
        {
            if (Modo == VdfModoMando.Interruptores) return;
            if (!Habilitado || EmergenciaActiva) return;

            int destino = Mathf.Min(NivelMaximo, Mathf.Max(NivelPendiente, 0) + 1);
            SolicitarNivel(destino);
        }

        public void PulsarBajar()
        {
            if (Modo == VdfModoMando.Interruptores) return;
            if (!Habilitado || EmergenciaActiva) return;
            if (NivelPendiente <= 0) return; // el motor ni siquiera ha arrancado

            int minimo = BajarDesdeNivel1Para ? 0 : NivelMinimo;
            int destino = Mathf.Max(minimo, NivelPendiente - 1);
            SolicitarNivel(destino);
        }

        /// <summary>Parada de emergencia: corta todo de inmediato y queda enclavada.</summary>
        public void PulsarEmergencia()
        {
            if (Modo == VdfModoMando.Interruptores && InterruptorMarcha)
                MarchaBloqueadaTrasEmergencia = true;
            EmergenciaActiva = true;
            Habilitado = false;
            NivelActivo = 0;
            NivelPendiente = 0;
            TiempoRestanteCambio = 0f;
        }

        public void RearmarEmergencia()
        {
            EmergenciaActiva = false;
        }

        public void Reiniciar()
        {
            Habilitado = false;
            EmergenciaActiva = false;
            NivelActivo = 0;
            NivelPendiente = 0;
            TiempoRestanteCambio = 0f;

            // Los interruptores vuelven abajo: si se quedaran arriba, el motor arrancaría
            // solo en cuanto alguien tocara cualquiera de ellos.
            InterruptorMarcha = false;
            InterruptorS5 = false;
            InterruptorS6 = false;
            MarchaBloqueadaTrasEmergencia = false;
        }

        // =====================================================================================
        // Interruptores (modo VdfModoMando.Interruptores)
        // =====================================================================================

        /// <summary>Cambia el modo de mando. Deja el PLC en reposo, con todo abajo.</summary>
        public void CambiarModo(VdfModoMando modo)
        {
            if (Modo == modo) return;
            Modo = modo;
            Reiniciar();
        }

        /// <summary>
        /// La velocidad que piden S5 y S6. Es la tabla del variador, la misma que el PLC
        /// escribe en modo pulsadores: ninguno → 1, solo S5 → 2, solo S6 → 3, los dos → 4.
        /// </summary>
        public static int NivelDeInterruptores(bool s5, bool s6)
        {
            if (s5 && s6) return 4;
            if (s6) return 3;
            if (s5) return 2;
            return 1;
        }

        /// <summary>Lee un interruptor.</summary>
        public bool Interruptor(VdfInterruptor cual)
        {
            switch (cual)
            {
                case VdfInterruptor.Marcha: return InterruptorMarcha;
                case VdfInterruptor.S5: return InterruptorS5;
                default: return InterruptorS6;
            }
        }

        /// <summary>
        /// Pone un interruptor arriba (true) o abajo (false). Fuera del modo interruptores
        /// se apunta la posición pero no actúa.
        /// </summary>
        public void FijarInterruptor(VdfInterruptor cual, bool arriba)
        {
            switch (cual)
            {
                case VdfInterruptor.Marcha:
                    InterruptorMarcha = arriba;
                    if (!arriba) MarchaBloqueadaTrasEmergencia = false;
                    break;
                case VdfInterruptor.S5: InterruptorS5 = arriba; break;
                case VdfInterruptor.S6: InterruptorS6 = arriba; break;
            }

            EvaluarInterruptores();
        }

        /// <summary>Cambia de posición un interruptor.</summary>
        public void ConmutarInterruptor(VdfInterruptor cual)
        {
            FijarInterruptor(cual, !Interruptor(cual));
        }

        private void EvaluarInterruptores()
        {
            if (Modo != VdfModoMando.Interruptores) return;

            // Subir la marcha CON la emergencia puesta tampoco vale: al rearmar, tocar S5 o
            // S6 arrancaría el motor sin que nadie lo hubiera pedido.
            if (EmergenciaActiva)
            {
                if (InterruptorMarcha) MarchaBloqueadaTrasEmergencia = true;
                return;
            }

            if (!InterruptorMarcha || MarchaBloqueadaTrasEmergencia)
            {
                if (Habilitado || NivelPendiente != 0 || NivelActivo != 0) Parar();
                return;
            }

            Habilitado = true;
            int destino = NivelDeInterruptores(InterruptorS5, InterruptorS6);

            if (RetardoConInterruptores)
            {
                SolicitarNivel(destino);
            }
            else
            {
                NivelPendiente = destino;
                NivelActivo = destino;
                TiempoRestanteCambio = 0f;
            }
        }

        private void SolicitarNivel(int destino)
        {
            if (destino == NivelPendiente) return;

            NivelPendiente = destino;
            NivelActivo = 0;
            TiempoRestanteCambio = RetardoCambioNivelSegundos;
        }

        // =====================================================================================
        // Avance del tiempo
        // =====================================================================================

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (!CambioEnCurso)
            {
                TiempoRestanteCambio = 0f;
                return;
            }

            TiempoRestanteCambio -= dt;
            if (TiempoRestanteCambio <= 0f)
            {
                NivelActivo = NivelPendiente;
                TiempoRestanteCambio = 0f;
            }
        }

        // =====================================================================================
        // Conexión con el variador
        // =====================================================================================

        /// <summary>
        /// Vuelca las salidas del PLC sobre los terminales físicos del variador.
        ///
        /// Ojo: escribe los TERMINALES S1/S5/S6, no funciones. Qué haga el variador con
        /// ellos depende de H1-01/H1-05/H1-06, que es justo lo que la práctica enseña.
        /// Si el alumno configura mal esos parámetros, el PLC seguirá mandando lo mismo
        /// y el motor se comportará mal: el error aparece solo.
        /// </summary>
        public void AplicarA(VfdModel vfd)
        {
            AplicarA(vfd, VdfPuertaCableado.Todo());
        }

        /// <summary>
        /// Igual que el anterior, pero pasando por el cableado: una salida solo llega al
        /// variador si su cable está puesto y el común de entradas también.
        ///
        /// La parada de emergencia NO pasa por la puerta a propósito: corta por hardware,
        /// antes del PLC, y tiene que seguir funcionando con el tablero a medio cablear.
        /// </summary>
        public void AplicarA(VfdModel vfd, VdfPuertaCableado puerta)
        {
            if (vfd == null) return;

            if (EmergenciaActiva)
            {
                vfd.SetTerminal(1, false);
                vfd.SetTerminal(4, false);
                vfd.SetTerminal(5, false);
                vfd.SetTerminal(6, false);
                if (vfd.Falla != VfdFault.ParadaEmergencia)
                    vfd.ParadaEmergencia();
                return;
            }

            if (Modo == VdfModoMando.Interruptores)
            {
                // Laboratorio: la marcha sale por Q0.5 hacia S4. S1 se queda sin cable.
                vfd.SetTerminal(1, false);
                vfd.SetTerminal(4, SalidaS1 && puerta.S4);
            }
            else
            {
                vfd.SetTerminal(1, SalidaS1 && puerta.S1);
                vfd.SetTerminal(4, false);
            }
            vfd.SetTerminal(5, SalidaS5 && puerta.S5);
            vfd.SetTerminal(6, SalidaS6 && puerta.S6);
        }
    }
}
