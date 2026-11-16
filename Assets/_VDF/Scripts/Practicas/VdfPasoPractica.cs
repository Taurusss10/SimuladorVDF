using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Qué hay que comprobar para dar un paso por cumplido.
    ///
    /// Es un enum y no una función porque los pasos viven en un <see cref="VdfPractica"/>,
    /// que es un asset: Unity no serializa delegados. El precio es que añadir una condición
    /// nueva exige tocar este enum y <see cref="VdfEvaluadorPasos"/>; a cambio, el guion de
    /// las prácticas se edita sin compilar, que es lo que pide el enunciado.
    /// </summary>
    public enum VdfCondicion
    {
        /// <summary>Nunca se cumple sola: el alumno la marca al leer. Para pasos informativos.</summary>
        Manual,

        // --- Práctica 1: reconocimiento ---------------------------------------------------
        /// <summary>Se ha señalado el borne o componente cuyo Id está en <c>Objetivo</c>.</summary>
        ComponenteIdentificado,
        /// <summary>Se han señalado todos los componentes del grupo indicado en <c>Objetivo</c>.</summary>
        GrupoIdentificado,

        // --- Práctica 2: configuración ----------------------------------------------------
        /// <summary>El parámetro <c>Objetivo</c> vale <c>Valor</c>.</summary>
        ParametroEnValor,
        /// <summary>La configuración no tiene ningún problema crítico.</summary>
        ConfiguracionOperable,
        /// <summary>La configuración coincide con la de referencia: ni un aviso.</summary>
        ConfiguracionSinAvisos,

        // --- Práctica 3: cableado ---------------------------------------------------------
        /// <summary>Los dos bornes de <c>Objetivo</c> ("A|B") quedan al mismo potencial.</summary>
        BornesUnidos,
        /// <summary>El cableado completo de la práctica es correcto.</summary>
        CableadoCorrecto,
        /// <summary>No hay cortocircuitos ni cables sobrantes.</summary>
        SinCablesSobrantes,

        // --- Práctica 4: operación --------------------------------------------------------
        /// <summary>El variador tiene tensión.</summary>
        VariadorConTension,
        /// <summary>Se pulsó START y el PLC quedó habilitado. Es un LOGRO: no se deshace.</summary>
        SistemaArrancado,
        /// <summary>Se alcanzó el nivel de velocidad <c>Valor</c>. LOGRO.</summary>
        NivelAlcanzado,
        /// <summary>Tras llegar arriba, se bajó hasta el nivel 1. LOGRO.</summary>
        BajoHastaNivel1,
        /// <summary>Tras operar, se pulsó STOP. LOGRO.</summary>
        ParadoTrasOperar,

        // --- Práctica 5: seguridad y fallas -----------------------------------------------
        /// <summary>Se accionó la parada de emergencia. LOGRO.</summary>
        EmergenciaAccionada,
        /// <summary>Se rearmó la emergencia. LOGRO.</summary>
        EmergenciaRearmada,
        /// <summary>El variador está en falla ahora mismo.</summary>
        VariadorEnFalla,

        /// <summary>
        /// El variador llegó a estar en falla en algún momento. Es un LOGRO.
        ///
        /// Hace falta porque «observa el piloto de FALLA» es algo que ocurre y queda visto:
        /// si se evaluara en vivo, el paso se desmarcaría en cuanto el alumno rearma, que es
        /// justo lo que el paso siguiente le pide hacer.
        /// </summary>
        VariadorEstuvoEnFalla,

        /// <summary>La avería activa ya no está presente: el alumno la ha reparado.</summary>
        FallaReparada,

        /// <summary>El alumno ha señalado cuál cree que es la causa. Es un LOGRO.</summary>
        FallaDiagnosticada,

        // Las dos siguientes van AL FINAL a propósito: el enum se guarda como número en
        // las prácticas de la escena, y meterlas en medio cambiaría el significado de
        // todas las de después.

        VariadorInicializado,

        ParametroRevisado,

        EmergenciaConMotorGirando,

        /// <summary>
        /// La avería está reparada Y, después, el motor ha vuelto a llegar a la velocidad 4.
        /// Con FallaReparada a secas, «repara y comprueba las cuatro velocidades» se cumplía
        /// sin comprobar nada: «me deja continuar de manera muy permisiva».
        /// </summary>
        FallaReparadaYProbada
    }

    /// <summary>
    /// Un paso del guion de una práctica.
    ///
    /// Lleva cuatro textos porque los cuatro hacen falta y cumplen papeles distintos: la
    /// instrucción dice qué hacer, la pista ayuda sin resolver, el mensaje de error explica
    /// qué salió mal cuando el alumno se equivoca, y la advertencia avisa antes de hacer algo
    /// peligroso.
    /// </summary>
    [System.Serializable]
    public class VdfPasoPractica
    {
        [Tooltip("Qué hay que hacer. Es lo que se lee en el panel.")]
        [TextArea(2, 4)] public string Instruccion;

        [Tooltip("Ayuda que se ofrece si el alumno se atasca. No debe resolver el paso.")]
        [TextArea(2, 4)] public string Pista;

        [Tooltip("Qué se le dice cuando hace algo que contradice este paso.")]
        [TextArea(2, 4)] public string MensajeError;

        [Tooltip("Advertencia de seguridad. Si no está vacía, se muestra ANTES de dejar actuar.")]
        [TextArea(2, 4)] public string Advertencia;

        [Tooltip("Ficha que se enseña al CUMPLIR el paso: qué es lo que se acaba de " +
                 "encontrar y para qué sirve. Vacía si el paso no la necesita.")]
        [TextArea(2, 6)] public string Explicacion;

        public bool TieneExplicacion { get { return !string.IsNullOrEmpty(Explicacion); } }

        [Header("Condición")]
        public VdfCondicion Condicion = VdfCondicion.Manual;

        [Tooltip("Identificador sobre el que actúa la condición: un parámetro (E2-01), " +
                 "un borne, un grupo, o dos bornes separados por | .")]
        public string Objetivo;

        [Tooltip("Valor numérico que pide la condición, cuando lo necesita.")]
        public float Valor;

        [Tooltip("Tolerancia al comparar Valor. 0 usa la de por defecto del evaluador.")]
        public float Tolerancia;

        public bool EsLogro
        {
            get
            {
                switch (Condicion)
                {
                    case VdfCondicion.Manual:
                    case VdfCondicion.ComponenteIdentificado:
                    case VdfCondicion.GrupoIdentificado:
                    case VdfCondicion.SistemaArrancado:
                    case VdfCondicion.NivelAlcanzado:
                    case VdfCondicion.BajoHastaNivel1:
                    case VdfCondicion.ParadoTrasOperar:
                    case VdfCondicion.EmergenciaAccionada:
                    case VdfCondicion.EmergenciaRearmada:
                    case VdfCondicion.FallaDiagnosticada:
                    case VdfCondicion.VariadorEstuvoEnFalla:
                    case VdfCondicion.VariadorInicializado:
                    case VdfCondicion.EmergenciaConMotorGirando:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public bool TieneAdvertencia { get { return !string.IsNullOrEmpty(Advertencia); } }
    }
}
