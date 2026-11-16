namespace VDF
{
    /// <summary>Estados operativos del variador.</summary>
    public enum VfdState
    {
        Detenido,
        Acelerando,
        EnMarcha,
        Desacelerando,
        Falla
    }

    /// <summary>
    /// Fallas modeladas. El modelo eléctrico interno del variador está fuera de alcance,
    /// así que las fallas se disparan desde fuera (cableado, PE) en vez de deducirse
    /// de corrientes y tensiones simuladas.
    /// </summary>
    public enum VfdFault
    {
        Ninguna,
        /// <summary>Uv1: pérdida de alimentación en R/L1 - S/L2.</summary>
        BajoVoltaje,
        /// <summary>Corte por parada de emergencia.</summary>
        ParadaEmergencia,
        /// <summary>oL1: sobrecarga térmica del motor.</summary>
        SobrecargaMotor
    }

    /// <summary>Gravedad de un problema de configuración detectado.</summary>
    public enum VfdIssueSeverity
    {
        /// <summary>Impide que la práctica funcione (el motor no arranca o no gira bien).</summary>
        Critica,
        /// <summary>Funciona, pero con comportamiento incorrecto o inseguro.</summary>
        Advertencia,
        /// <summary>Desviación respecto a la configuración de referencia, sin efecto grave.</summary>
        Info
    }

    /// <summary>Problema de configuración detectado por <see cref="VfdModel.ValidarConfiguracion"/>.</summary>
    public readonly struct VfdConfigIssue
    {
        public readonly string ParametroId;
        public readonly VfdIssueSeverity Severidad;
        public readonly string Mensaje;

        public VfdConfigIssue(string parametroId, VfdIssueSeverity severidad, string mensaje)
        {
            ParametroId = parametroId;
            Severidad = severidad;
            Mensaje = mensaje;
        }

        public override string ToString()
        {
            return "[" + Severidad + "] " + ParametroId + ": " + Mensaje;
        }
    }

    /// <summary>
    /// Códigos de función de las entradas digitales multifunción (H1-01 .. H1-06),
    /// tal como los define el manual oficial del V1000 (SIEP C710606 18F).
    /// Solo se listan los que usa esta práctica.
    /// </summary>
    public static class VfdTerminalFunction
    {
        /// <summary>Secuencia de 3 hilos.</summary>
        public const int TresHilos = 0;
        /// <summary>Selección LOCAL/REMOTE. NO es una orden de marcha.</summary>
        public const int LocalRemote = 1;
        /// <summary>Multi-step speed reference 1 (bit bajo).</summary>
        public const int MultiVelocidad1 = 3;
        /// <summary>Multi-step speed reference 2 (bit alto).</summary>
        public const int MultiVelocidad2 = 4;
        /// <summary>Marcha adelante en secuencia de 2 hilos.</summary>
        public const int MarchaAdelante2Hilos = 40;
        /// <summary>Marcha atrás en secuencia de 2 hilos.</summary>
        public const int MarchaAtras2Hilos = 41;
    }
}
