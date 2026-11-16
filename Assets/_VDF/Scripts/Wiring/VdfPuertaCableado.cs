
namespace VDF
{
    /// <summary>
    /// Qué señales llegan de verdad, según cómo esté el tablero cableado.
    ///
    /// Por qué existe: hasta la fase 8 el cableado solo decidía una cosa, si el variador
    /// tenía tensión. Todo lo demás (la orden de marcha, las multi-velocidades y los cuatro
    /// pilotos) se aplicaba igual estuviera cableado o no. Medido en Play Mode recorriendo
    /// el catálogo de fallas, eso dejaba **dos de las seis fallas de la práctica 5 sin
    /// síntoma ninguno**:
    ///
    ///  - `SC_SIN_COMUN`: quitar el común de las entradas digitales daba 10/25/35/50 Hz,
    ///    exactamente igual que el módulo sano, cuando lo que dice el enunciado de la falla
    ///    es que «el variador no reacciona a ninguna de ellas».
    ///  - `V3_SIN_CABLE`: el piloto V3 se encendía igual sin su cable.
    ///
    /// Y de paso hacía que la práctica 3 no tuviera consecuencias: se podía dejar media
    /// regleta sin cablear y el módulo funcionaba igual.
    ///
    /// Es C# puro y sin estado, así que se puede probar sin escena y se calcula una vez por
    /// cambio de cableado, no por frame.
    /// </summary>
    public struct VdfPuertaCableado
    {
        /// <summary>Tensión en la entrada del variador.</summary>
        public bool Alimentado;

        /// <summary>
        /// Común de las entradas digitales (0 V a SC).
        ///
        /// Sin él **ninguna** entrada digital hace efecto, aunque su cable esté puesto: es
        /// el retorno de todas. Por eso multiplica a S1, S5 y S6 en vez de ser una señal más.
        /// </summary>
        public bool ComunDeEntradas;

        public bool S1;

        /// <summary>
        /// Orden de marcha por S4. Solo en la práctica del laboratorio (modo interruptores),
        /// donde Q0.5 va a S4 en vez de Q0.7 a S1.
        /// </summary>
        public bool S4;

        public bool S5;
        public bool S6;

        /// <summary>Pilotos V1 a V4, en el índice 0 a 3.</summary>
        public bool[] V;

        /// <summary>Las tres fases del motor.</summary>
        public bool Motor;

        /// <summary>
        /// Todo cableado. Es lo que se usa cuando la práctica no exige cableado correcto
        /// (las prácticas 1, 2, 4 y 5 arrancan con el tablero ya cableado) y en los tests de
        /// lógica, que no tienen por qué montar un grafo.
        /// </summary>
        public static VdfPuertaCableado Todo()
        {
            var p = new VdfPuertaCableado();
            p.Alimentado = true;
            p.ComunDeEntradas = true;
            p.S1 = true; p.S4 = true; p.S5 = true; p.S6 = true;
            p.Motor = true;
            p.V = new bool[] { true, true, true, true };
            return p;
        }

        /// <summary>
        /// Lee el grafo y dice qué llega. Los bornes son los de
        /// <see cref="VdfWiringSpec.Practica"/>; si se cambia el esquema hay que cambiarlos
        /// aquí también, y los tests lo avisan.
        /// </summary>
        public static VdfPuertaCableado Calcular(VdfWiringGraph grafo)
        {
            return Calcular(grafo, VdfMando.Activo);
        }

        /// <summary>Igual, pero con los bornes del cableado de ese modo de mando.</summary>
        public static VdfPuertaCableado Calcular(VdfWiringGraph grafo, VdfModoMando modo)
        {
            if (grafo == null) return Todo();
            if (modo == VdfModoMando.Interruptores) return CalcularLaboratorio(grafo);

            var p = new VdfPuertaCableado();

            p.Alimentado = grafo.MismaRed("BRK-L1", "R/L1")
                        && grafo.MismaRed("BRK-L2", "S/L2");

            p.ComunDeEntradas = grafo.MismaRed("GND24", "SC");

            p.S1 = p.ComunDeEntradas && grafo.MismaRed("PLC-Q0.7", "S1");
            p.S5 = p.ComunDeEntradas && grafo.MismaRed("PLC-Q0.6", "S5");
            p.S6 = p.ComunDeEntradas && grafo.MismaRed("PLC-Q0.5", "S6");

            p.Motor = grafo.MismaRed("U_MOT", "MOT-U")
                   && grafo.MismaRed("V_MOT", "MOT-V")
                   && grafo.MismaRed("W_MOT", "MOT-W");

            // Un piloto necesita su salida del PLC Y su retorno a L2. El común de las
            // salidas del PLC (VCC24 a 2L) alimenta el grupo entero.
            bool comunSalidas = grafo.MismaRed("VCC24", "PLC-2L");
            p.V = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                p.V[i] = comunSalidas
                      && grafo.MismaRed("PLC-Q0." + i, "V" + (i + 1) + "-A")
                      && grafo.MismaRed("S/L2", "V" + (i + 1) + "-B");
            }

            return p;
        }

        private static VdfPuertaCableado CalcularLaboratorio(VdfWiringGraph grafo)
        {
            var p = new VdfPuertaCableado();

            // F2 es BRK-L3; el borne del centro del breaker no se usa en el laboratorio.
            p.Alimentado = grafo.MismaRed("BRK-L1", "R/L1")
                        && grafo.MismaRed("BRK-L3", "S/L2");

            p.ComunDeEntradas = grafo.MismaRed("PLC-2L", "SC");

            p.S1 = false;
            p.S4 = p.ComunDeEntradas && grafo.MismaRed("PLC-Q0.5", "S4");
            p.S5 = p.ComunDeEntradas && grafo.MismaRed("PLC-Q0.6", "S5");
            p.S6 = p.ComunDeEntradas && grafo.MismaRed("PLC-Q0.7", "S6");

            p.Motor = grafo.MismaRed("U_MOT", "MOT-U")
                   && grafo.MismaRed("V_MOT", "MOT-V")
                   && grafo.MismaRed("W_MOT", "MOT-W");

            bool comunPilotos = grafo.MismaRed("BRK-L1", "PLC-1L");
            p.V = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                p.V[i] = comunPilotos
                      && grafo.MismaRed("PLC-Q0." + i, "V" + (i + 1) + "-A")
                      && grafo.MismaRed("S/L2", "V" + (i + 1) + "-B");
            }

            return p;
        }

        /// <summary>Piloto V1 a V4, por número y no por índice, que es como se habla de ellos.</summary>
        public bool Piloto(int numero)
        {
            if (V == null) return true;
            if (numero < 1 || numero > V.Length) return false;
            return V[numero - 1];
        }
    }
}
