using System.Collections.Generic;

namespace VDF
{
    /// <summary>
    /// De dónde sale el estado de un contacto o una bobina del diagrama.
    ///
    /// El diagrama es **de solo lectura**: no decide nada, solo refleja lo que
    /// <see cref="PlcFanLogic"/> está haciendo. Por eso cada símbolo declara qué señal mira,
    /// y nunca al revés. Si el diagrama pudiera influir en la lógica, dejaría de ser un
    /// diagrama y pasaría a ser una segunda implementación que mantener en sincronía.
    /// </summary>
    public enum VdfSenalPlc
    {
        /// <summary>Siempre cerrado. Para tramos de cable sin condición.</summary>
        Siempre,

        /// <summary>El sistema está habilitado (se pulsó START y no STOP).</summary>
        Habilitado,

        /// <summary>La parada de emergencia está enclavada.</summary>
        Emergencia,

        /// <summary>El nivel activo es exactamente <c>Parametro</c>.</summary>
        NivelExacto,

        /// <summary>El nivel activo es <c>Parametro</c> o mayor.</summary>
        NivelMayorIgual,

        /// <summary>Hay un cambio de nivel esperando a que venza el retardo.</summary>
        CambioEnCurso,

        /// <summary>Salida hacia el terminal S1 del variador.</summary>
        SalidaS1,
        /// <summary>Salida hacia S5: bit bajo de la multi-velocidad.</summary>
        SalidaS5,
        /// <summary>Salida hacia S6: bit alto de la multi-velocidad.</summary>
        SalidaS6,

        /// <summary>Indicador V<c>Parametro</c>.</summary>
        SalidaV,

        /// <summary>Pulsador START, mientras dura el destello de la pulsación.</summary>
        PulsadorStart,
        PulsadorStop,
        PulsadorSubir,
        PulsadorBajar,

        // Modo VdfModoMando.Interruptores: los interruptores son de posición mantenida, así
        // que no destellan: el contacto se queda cerrado mientras estén arriba.

        /// <summary>Interruptor MARCHA arriba.</summary>
        InterruptorMarcha,
        /// <summary>Interruptor S5 arriba.</summary>
        InterruptorS5,
        /// <summary>Interruptor S6 arriba.</summary>
        InterruptorS6,
        /// <summary>La marcha espera a que se baje y se vuelva a subir tras una emergencia.</summary>
        MarchaBloqueada
    }

    /// <summary>Qué símbolo se dibuja.</summary>
    public enum VdfLadderSimbolo
    {
        /// <summary>Contacto normalmente abierto: conduce cuando su señal es true.</summary>
        ContactoNA,
        /// <summary>Contacto normalmente cerrado: conduce cuando su señal es false.</summary>
        ContactoNC,
        /// <summary>Bobina de salida.</summary>
        Bobina,
        /// <summary>Temporizador a la conexión, con su cuenta.</summary>
        Temporizador,
        /// <summary>Contador, con su valor actual.</summary>
        Contador
    }

    /// <summary>Un símbolo del diagrama: un contacto, una bobina, un temporizador.</summary>
    [System.Serializable]
    public class VdfLadderElemento
    {
        public VdfLadderSimbolo Simbolo = VdfLadderSimbolo.ContactoNA;

        /// <summary>Rótulo que se dibuja encima, por ejemplo "START" o "S5".</summary>
        public string Etiqueta;

        public VdfSenalPlc Senal = VdfSenalPlc.Siempre;

        /// <summary>Nivel para las señales que lo necesitan.</summary>
        public int Parametro;

        public VdfLadderElemento() { }

        public VdfLadderElemento(VdfLadderSimbolo simbolo, string etiqueta,
                                 VdfSenalPlc senal, int parametro = 0)
        {
            Simbolo = simbolo;
            Etiqueta = etiqueta;
            Senal = senal;
            Parametro = parametro;
        }

        /// <summary>
        /// ¿Conduce este elemento con el estado dado?
        ///
        /// Un contacto normalmente cerrado conduce cuando su señal está en false: es lo que
        /// hace que un STOP cableado como NC pare el sistema al pulsarlo, y también lo que
        /// hace que un cable cortado lo pare. Esa es la razón de que en la industria el STOP
        /// se cablee así, y el diagrama tiene que enseñarlo tal cual.
        /// </summary>
        public bool Conduce(VdfEstadoLadder estado)
        {
            bool senal = estado.Leer(Senal, Parametro);

            switch (Simbolo)
            {
                case VdfLadderSimbolo.ContactoNC:
                    return !senal;
                default:
                    return senal;
            }
        }
    }

    /// <summary>
    /// Un peldaño del diagrama.
    ///
    /// Se modela como una serie de contactos con, opcionalmente, un grupo en paralelo sobre
    /// el primero. Con eso se cubre todo lo que la lógica del ventilador necesita, incluido
    /// el enclavamiento clásico START/STOP, que es exactamente «START en paralelo con el
    /// propio contacto de la bobina».
    /// </summary>
    [System.Serializable]
    public class VdfLadderPeldano
    {
        public string Titulo;

        /// <summary>Contactos en serie, de izquierda a derecha.</summary>
        public List<VdfLadderElemento> Serie = new List<VdfLadderElemento>();

        /// <summary>
        /// Contactos en paralelo con el PRIMERO de la serie. Vacío si no hay rama.
        /// </summary>
        public List<VdfLadderElemento> Paralelo = new List<VdfLadderElemento>();

        /// <summary>La salida del peldaño.</summary>
        public VdfLadderElemento Bobina;

        /// <summary>¿Llega corriente hasta la bobina?</summary>
        public bool Energizado(VdfEstadoLadder estado)
        {
            if (Serie == null || Serie.Count == 0) return false;

            // El primer tramo puede tener rama en paralelo: basta con que conduzca uno.
            bool paso = Serie[0].Conduce(estado);
            if (!paso && Paralelo != null)
            {
                for (int i = 0; i < Paralelo.Count; i++)
                    if (Paralelo[i].Conduce(estado)) { paso = true; break; }
            }
            if (!paso) return false;

            // El resto, en serie: tienen que conducir todos.
            for (int i = 1; i < Serie.Count; i++)
                if (!Serie[i].Conduce(estado)) return false;

            return true;
        }
    }
}
