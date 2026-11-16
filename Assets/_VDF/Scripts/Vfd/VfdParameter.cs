using System.Collections.Generic;

namespace VDF
{
    /// <summary>
    /// Definición de un parámetro del variador: rango, resolución y valor de fábrica.
    /// </summary>
    public sealed class VfdParameterDef
    {
        public readonly string Id;
        public readonly string Descripcion;
        public readonly string Unidad;
        public readonly float Min;
        public readonly float Max;
        public readonly int Decimales;
        public readonly float PorDefecto;

        /// <summary>Si no es null, el parámetro solo admite estos valores (enumerado).</summary>
        public readonly float[] ValoresDiscretos;

        /// <summary>
        /// true  = el valor de fábrica está confirmado contra la tabla verificada del enunciado.
        /// false = valor INFERIDO. Depende del modelo/capacidad del variador (selección o2-04)
        ///         y debe confirmarse contra la placa y el manual del equipo real.
        /// </summary>
        public readonly bool PorDefectoVerificado;

        public VfdParameterDef(string id, string descripcion, string unidad,
                               float min, float max, int decimales, float porDefecto,
                               bool porDefectoVerificado = true, float[] valoresDiscretos = null)
        {
            Id = id;
            Descripcion = descripcion;
            Unidad = unidad;
            Min = min;
            Max = max;
            Decimales = decimales;
            PorDefecto = porDefecto;
            PorDefectoVerificado = porDefectoVerificado;
            ValoresDiscretos = valoresDiscretos;
        }

        /// <summary>Comprueba si un valor es admisible; devuelve el motivo del rechazo si no lo es.</summary>
        public bool EsValido(float valor, out string motivo)
        {
            if (float.IsNaN(valor) || float.IsInfinity(valor))
            {
                motivo = "valor no numerico";
                return false;
            }

            if (ValoresDiscretos != null)
            {
                for (int i = 0; i < ValoresDiscretos.Length; i++)
                {
                    if (Aprox(ValoresDiscretos[i], valor))
                    {
                        motivo = null;
                        return true;
                    }
                }

                var textos = new string[ValoresDiscretos.Length];
                for (int i = 0; i < ValoresDiscretos.Length; i++)
                    textos[i] = Formatear(ValoresDiscretos[i]);

                motivo = Id + " solo admite los valores " + string.Join(", ", textos);
                return false;
            }

            if (valor < Min || valor > Max)
            {
                motivo = Id + " fuera de rango (" + Formatear(Min) + " .. " + Formatear(Max) + " " + Unidad + ")";
                return false;
            }

            motivo = null;
            return true;
        }

        /// <summary>Redondea a la resolución del parámetro (lo que el teclado permite teclear).</summary>
        public float Cuantizar(float valor)
        {
            float escala = 1f;
            for (int i = 0; i < Decimales; i++) escala *= 10f;
            return UnityEngine.Mathf.Round(valor * escala) / escala;
        }

        public string Formatear(float valor)
        {
            return valor.ToString("F" + Decimales, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool Aprox(float a, float b)
        {
            return UnityEngine.Mathf.Abs(a - b) < 0.0005f;
        }
    }

    /// <summary>
    /// Tabla de parámetros del V1000 limitada a los que usa esta práctica.
    ///
    /// IMPORTANTE sobre la procedencia de los datos:
    ///  - Los VALORES CORRECTOS (ConfiguracionDeReferencia) vienen de la tabla del enunciado,
    ///    verificada contra el manual oficial SIEP C710606 18F.
    ///  - Los RANGOS y los VALORES DE FÁBRICA marcados con porDefectoVerificado:false son
    ///    INFERIDOS y deben contrastarse con el manual/placa del equipo real.
    /// </summary>
    public static class VfdParameterTable
    {
        private static readonly List<VfdParameterDef> _orden = new List<VfdParameterDef>();
        private static readonly Dictionary<string, VfdParameterDef> _porId = new Dictionary<string, VfdParameterDef>();

        static VfdParameterTable()
        {
            // --- Inicialización -------------------------------------------------------------
            Add(new VfdParameterDef("A1-03", "Inicializar parametros", "", 0, 3330, 0, 0,
                true, new float[] { 0, 1110, 2220, 3330 }));

            // --- Marcha y referencia --------------------------------------------------------
            // b1-01: 0 = operador (usa d1-xx), 1 = terminales (entrada analogica A1).
            Add(new VfdParameterDef("b1-01", "Seleccion de referencia de frecuencia", "", 0, 4, 0, 1,
                false, new float[] { 0, 1, 2, 3, 4 }));
            // b1-02: 0 = operador, 1 = terminales, 2 = serie, 3 = tarjeta opcional.
            Add(new VfdParameterDef("b1-02", "Seleccion de orden de marcha", "", 0, 3, 0, 1,
                false, new float[] { 0, 1, 2, 3 }));

            // --- Rampas ---------------------------------------------------------------------
            Add(new VfdParameterDef("C1-01", "Tiempo de aceleracion 1", "s", 0.0f, 6000.0f, 1, 10.0f));
            Add(new VfdParameterDef("C1-02", "Tiempo de desaceleracion 1", "s", 0.0f, 6000.0f, 1, 10.0f));

            // --- Velocidades preseleccionadas ----------------------------------------------
            Add(new VfdParameterDef("d1-01", "Referencia de frecuencia 1", "Hz", 0.0f, 400.0f, 2, 0.00f, false));
            Add(new VfdParameterDef("d1-02", "Referencia de frecuencia 2", "Hz", 0.0f, 400.0f, 2, 0.00f, false));
            Add(new VfdParameterDef("d1-03", "Referencia de frecuencia 3", "Hz", 0.0f, 400.0f, 2, 0.00f, false));
            Add(new VfdParameterDef("d1-04", "Referencia de frecuencia 4", "Hz", 0.0f, 400.0f, 2, 0.00f, false));

            // --- Curva V/f ------------------------------------------------------------------
            Add(new VfdParameterDef("E1-04", "Frecuencia maxima de salida", "Hz", 40.0f, 400.0f, 1, 60.0f));
            Add(new VfdParameterDef("E1-05", "Voltaje maximo de salida", "V", 0.0f, 255.0f, 1, 200.0f, false));

            // --- Datos del motor ------------------------------------------------------------
            // E2-01 de fabrica depende de la capacidad del variador: VALOR INFERIDO.
            Add(new VfdParameterDef("E2-01", "Corriente nominal del motor", "A", 0.01f, 20.00f, 2, 3.00f, false));
            Add(new VfdParameterDef("E2-11", "Potencia nominal del motor", "kW", 0.00f, 650.00f, 2, 0.55f, false));

            // --- Entradas digitales multifuncion --------------------------------------------
            Add(new VfdParameterDef("H1-01", "Funcion del terminal S1", "", 0, 99, 0, 40, true, FuncionesAdmitidas()));
            Add(new VfdParameterDef("H1-02", "Funcion del terminal S2", "", 0, 99, 0, 41, true, FuncionesAdmitidas()));
            Add(new VfdParameterDef("H1-03", "Funcion del terminal S3", "", 0, 99, 0, 24, false, FuncionesAdmitidas()));
            Add(new VfdParameterDef("H1-04", "Funcion del terminal S4", "", 0, 99, 0, 14, false, FuncionesAdmitidas()));
            Add(new VfdParameterDef("H1-05", "Funcion del terminal S5", "", 0, 99, 0, 3, true, FuncionesAdmitidas()));
            Add(new VfdParameterDef("H1-06", "Funcion del terminal S6", "", 0, 99, 0, 4, true, FuncionesAdmitidas()));

            // --- Proteccion -----------------------------------------------------------------
            // L1-01: 0 = deshabilitada, 1 = motor de uso general (estandar).
            Add(new VfdParameterDef("L1-01", "Proteccion termica del motor", "", 0, 4, 0, 1,
                true, new float[] { 0, 1, 2, 3, 4 }));
        }

        /// <summary>
        /// Conjunto de funciones de entrada digital que el simulador reconoce.
        /// El V1000 real admite muchas mas; aqui se limita a las de la practica para
        /// que el teclado no ofrezca valores cuyo efecto no este modelado.
        /// </summary>
        private static float[] FuncionesAdmitidas()
        {
            return new float[]
            {
                VfdTerminalFunction.TresHilos,
                VfdTerminalFunction.LocalRemote,
                VfdTerminalFunction.MultiVelocidad1,
                VfdTerminalFunction.MultiVelocidad2,
                14, // Reset de falla
                24, // Falla externa
                VfdTerminalFunction.MarchaAdelante2Hilos,
                VfdTerminalFunction.MarchaAtras2Hilos
            };
        }

        private static void Add(VfdParameterDef def)
        {
            _orden.Add(def);
            _porId[def.Id] = def;
        }

        /// <summary>Parámetros en el orden en que los recorre el teclado.</summary>
        public static IReadOnlyList<VfdParameterDef> Todos { get { return _orden; } }

        public static bool TryGet(string id, out VfdParameterDef def)
        {
            // Un id nulo es «no existe», no una excepcion. TryGetValue lanza
            // ArgumentNullException con clave nula, y por aqui pasan el teclado, los guiones
            // de las practicas y el catalogo de averias: cualquiera de esos caminos con un
            // id vacio tumbaba el fotograma en vez de devolver false.
            if (string.IsNullOrEmpty(id)) { def = null; return false; }

            return _porId.TryGetValue(id, out def);
        }

        public static VfdParameterDef Get(string id)
        {
            VfdParameterDef def;
            if (!_porId.TryGetValue(id, out def))
                throw new KeyNotFoundException("Parametro desconocido: " + id);
            return def;
        }

        /// <summary>
        /// Configuración correcta para la práctica del ventilador, según la tabla
        /// verificada contra el manual oficial SIEP C710606 18F.
        /// </summary>
        public static readonly Dictionary<string, float> ConfiguracionDeReferencia =
            new Dictionary<string, float>
            {
                { "E2-01", 1.2f },
                { "L1-01", 1f },
                { "E1-04", 60.0f },
                { "E1-05", 220.0f },
                { "E2-11", 0.25f },
                { "b1-01", 0f },
                { "b1-02", 1f },
                { "d1-01", 10.0f },
                { "d1-02", 25.0f },
                { "d1-03", 35.0f },
                { "d1-04", 50.0f },
                { "H1-01", 40f },
                { "H1-05", 3f },
                { "H1-06", 4f },
                { "C1-01", 10.0f },
                { "C1-02", 10.0f }
            };

        public static readonly Dictionary<string, float> ConfiguracionLaboratorio = CrearLaboratorio();

        private static Dictionary<string, float> CrearLaboratorio()
        {
            var d = new Dictionary<string, float>(ConfiguracionDeReferencia);
            d["E2-01"] = 1.9f;
            d["H1-01"] = 14f;
            d["H1-04"] = 40f;
            return d;
        }

        /// <summary>La configuración correcta según el modo de mando.</summary>
        public static Dictionary<string, float> Referencia(VdfModoMando modo)
        {
            return modo == VdfModoMando.Interruptores ? ConfiguracionLaboratorio : ConfiguracionDeReferencia;
        }

        /// <summary>La configuración correcta del modo que rige ahora (<see cref="VdfMando.Activo"/>).</summary>
        public static Dictionary<string, float> ReferenciaActiva
        {
            get { return Referencia(VdfMando.Activo); }
        }
    }
}
