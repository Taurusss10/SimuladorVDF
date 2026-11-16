namespace VDF
{
    public enum VdfModoMando
    {
        /// <summary>START, STOP, SUBIR y BAJAR del tablero, con el contador del Ladder original.</summary>
        Pulsadores,

        /// <summary>Tres interruptores junto al PLC: marcha, S5 y S6.</summary>
        Interruptores
    }

    /// <summary>
    /// El modo de mando que rige ahora, para las piezas que no cuelgan de la simulación: la
    /// especificación del cableado, la configuración de referencia del variador y el
    /// catálogo de averías. Lo fija <see cref="VdfSimulacion.AplicarModoMando"/>.
    ///
    /// Es estático porque esas piezas son estáticas y se consultan desde muchos sitios
    /// (cableado de ratón y de visor, validación, averías). Los tests que lo cambian lo
    /// devuelven a <see cref="VdfModoMando.Pulsadores"/> al terminar.
    /// </summary>
    public static class VdfMando
    {
        public static VdfModoMando Activo = VdfModoMando.Pulsadores;

        /// <summary>¿Se está usando la práctica del laboratorio (interruptores)?</summary>
        public static bool Laboratorio { get { return Activo == VdfModoMando.Interruptores; } }
    }

    /// <summary>Cada uno de los tres interruptores del modo <see cref="VdfModoMando.Interruptores"/>.</summary>
    public enum VdfInterruptor
    {
        /// <summary>Arriba equivale a START; abajo, a STOP.</summary>
        Marcha,
        /// <summary>Bit bajo de la selección de velocidad.</summary>
        S5,
        /// <summary>Bit alto de la selección de velocidad.</summary>
        S6
    }
}
