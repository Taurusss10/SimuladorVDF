using System.Collections.Generic;
using System.Text;

namespace VDF
{
    /// <summary>
    /// Una red eléctrica que el cableado correcto debe formar: todos estos bornes
    /// tienen que quedar unidos entre sí, sin importar con qué cables ni en qué orden.
    /// </summary>
    public sealed class VdfWiringNet
    {
        public readonly string Nombre;
        public readonly string[] Bornes;

        public VdfWiringNet(string nombre, params string[] bornes)
        {
            Nombre = nombre;
            Bornes = bornes;
        }

        /// <summary>Cables mínimos para formar esta red (un árbol sobre sus bornes).</summary>
        public int CablesMinimos { get { return Bornes.Length - 1; } }
    }

    /// <summary>Resultado de comparar el cableado del alumno con el correcto.</summary>
    public sealed class VdfWiringReport
    {
        public readonly List<string> Faltantes = new List<string>();
        public readonly List<string> Sobrantes = new List<string>();

        public int CablesMinimos;
        public int CablesPuestos;

        public bool EsCorrecto { get { return Faltantes.Count == 0 && Sobrantes.Count == 0; } }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(EsCorrecto ? "Cableado correcto." : "Cableado incompleto o incorrecto.");
            sb.AppendLine("Cables puestos: " + CablesPuestos + "  (minimo necesario: " + CablesMinimos + ")");

            if (Faltantes.Count > 0)
            {
                sb.AppendLine("FALTA:");
                for (int i = 0; i < Faltantes.Count; i++) sb.AppendLine("  - " + Faltantes[i]);
            }
            if (Sobrantes.Count > 0)
            {
                sb.AppendLine("SOBRA / MAL:");
                for (int i = 0; i < Sobrantes.Count; i++) sb.AppendLine("  - " + Sobrantes[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// El cableado correcto de la práctica, expresado como datos.
    ///
    /// ⚠️ Transcrito de la sección "Cableado correcto (aproximado)" del enunciado, que
    /// el propio enunciado marca como aproximada. Falta en ella el cableado de los
    /// pulsadores (START/STOP/SUBIR/BAJAR/PE) hacia las ENTRADAS del PLC: el enunciado
    /// solo describe las salidas. Ver "Preguntas pendientes".
    /// </summary>
    public static class VdfWiringSpec
    {
        /// <summary>
        /// Asignación de salidas del PLC, **leída de los esquemas de conexión del manual del
        /// módulo** (sección 3.4, página 18), no deducida.
        ///
        /// El S7-1200 1214C AC/DC/RLY agrupa Q0.0–Q0.4 bajo el común 1L y Q0.5–Q1.1 bajo 2L.
        /// 1L va a fase, porque los indicadores son de 220 V; 2L va a +24 V, porque de ahí
        /// salen las señales hacia las entradas digitales del variador.
        ///
        /// Hasta que llegó el manual, S1/S5/S6 estaban puestos en Q0.4/Q0.5/Q0.6 por
        /// deducción. El esquema real los pone en **Q0.7 / Q0.6 / Q0.5**, y además Q0.4
        /// queda libre: cae en el grupo de 1L, que está a 220 V, así que no puede alimentar
        /// una entrada de 24 V del variador. La deducción fallaba justo ahí.
        /// </summary>
        public static List<VdfWiringNet> Practica()
        {
            return Practica(VdfMando.Activo);
        }

        /// <summary>El cableado correcto según el modo de mando.</summary>
        public static List<VdfWiringNet> Practica(VdfModoMando modo)
        {
            return modo == VdfModoMando.Interruptores ? Laboratorio() : ConPulsadores();
        }

        public static List<VdfWiringNet> Laboratorio()
        {
            return new List<VdfWiringNet>
            {
                new VdfWiringNet("Fase L1 (F1 -> variador y común 1L del PLC)",
                    "BRK-L1", "R/L1", "PLC-1L"),
                new VdfWiringNet("Fase L2 (F2 -> variador y retorno de los indicadores)",
                    "BRK-L3", "S/L2", "V1-B", "V2-B", "V3-B", "V4-B"),

                new VdfWiringNet("Motor fase U", "U_MOT", "MOT-U"),
                new VdfWiringNet("Motor fase V", "V_MOT", "MOT-V"),
                new VdfWiringNet("Motor fase W", "W_MOT", "MOT-W"),

                new VdfWiringNet("Común 2L del PLC al común SC de las entradas", "PLC-2L", "SC"),

                new VdfWiringNet("Orden de marcha: PLC Q0.5 -> S4", "PLC-Q0.5", "S4"),
                new VdfWiringNet("Multi-velocidad 1: PLC Q0.6 -> S5", "PLC-Q0.6", "S5"),
                new VdfWiringNet("Multi-velocidad 2: PLC Q0.7 -> S6", "PLC-Q0.7", "S6"),

                new VdfWiringNet("Indicador V1", "PLC-Q0.0", "V1-A"),
                new VdfWiringNet("Indicador V2", "PLC-Q0.1", "V2-A"),
                new VdfWiringNet("Indicador V3", "PLC-Q0.2", "V3-A"),
                new VdfWiringNet("Indicador V4", "PLC-Q0.3", "V4-A")
            };
        }

        /// <summary>El cableado de siempre, el del modo pulsadores.</summary>
        public static List<VdfWiringNet> ConPulsadores()
        {
            return new List<VdfWiringNet>
            {
                // --- Potencia -------------------------------------------------------------
                new VdfWiringNet("Fase L1 (breaker -> variador y común 1L del PLC)",
                    "BRK-L1", "R/L1", "PLC-1L"),
                new VdfWiringNet("Fase L2 (breaker -> variador y retorno de los indicadores)",
                    "BRK-L2", "S/L2", "V1-B", "V2-B", "V3-B", "V4-B"),

                // --- Salida a motor -------------------------------------------------------
                new VdfWiringNet("Motor fase U", "U_MOT", "MOT-U"),
                new VdfWiringNet("Motor fase V", "V_MOT", "MOT-V"),
                new VdfWiringNet("Motor fase W", "W_MOT", "MOT-W"),

                // --- Control 24 V ---------------------------------------------------------
                new VdfWiringNet("+24 V al común de salidas 2L del PLC", "VCC24", "PLC-2L"),
                new VdfWiringNet("0 V al común de entradas digitales SC", "GND24", "SC"),

                // --- Salidas del PLC hacia el variador ------------------------------------
                new VdfWiringNet("Orden de marcha: PLC -> S1", "PLC-Q0.7", "S1"),
                new VdfWiringNet("Multi-velocidad 1: PLC -> S5", "PLC-Q0.6", "S5"),
                new VdfWiringNet("Multi-velocidad 2: PLC -> S6", "PLC-Q0.5", "S6"),

                // --- Salidas del PLC hacia los indicadores --------------------------------
                new VdfWiringNet("Indicador V1", "PLC-Q0.0", "V1-A"),
                new VdfWiringNet("Indicador V2", "PLC-Q0.1", "V2-A"),
                new VdfWiringNet("Indicador V3", "PLC-Q0.2", "V3-A"),
                new VdfWiringNet("Indicador V4", "PLC-Q0.3", "V4-A")
            };
        }

        /// <summary>Número mínimo de cables para dejar la práctica cableada.</summary>
        public static int CablesMinimos(List<VdfWiringNet> redes)
        {
            int total = 0;
            for (int i = 0; i < redes.Count; i++) total += redes[i].CablesMinimos;
            return total;
        }

        /// <summary>
        /// Compara el cableado hecho con el correcto y explica qué falta y qué sobra.
        ///
        /// No compara cable a cable: agrupa por redes. Un alumno que una
        /// BRK-L2 → S/L2 → V1-B y otro que una BRK-L2 → V1-B → S/L2 obtienen
        /// los dos "correcto", porque eléctricamente lo son.
        /// </summary>
        public static VdfWiringReport Validar(VdfWiringGraph grafo, List<VdfWiringNet> redes)
        {
            var informe = new VdfWiringReport();
            informe.CablesPuestos = grafo.Cuenta;
            informe.CablesMinimos = CablesMinimos(redes);

            // Mapa borne -> nombre de la red a la que debe pertenecer.
            var redDe = new Dictionary<string, string>();
            for (int i = 0; i < redes.Count; i++)
                for (int j = 0; j < redes[i].Bornes.Length; j++)
                    redDe[redes[i].Bornes[j]] = redes[i].Nombre;

            // --- 1. ¿Falta algo? Cada red debe estar completa. ----------------------------
            for (int i = 0; i < redes.Count; i++)
            {
                var red = redes[i];
                string referencia = red.Bornes[0];

                for (int j = 1; j < red.Bornes.Length; j++)
                {
                    if (!grafo.MismaRed(referencia, red.Bornes[j]))
                    {
                        informe.Faltantes.Add(red.Nombre + ": " + referencia + " no llega a " + red.Bornes[j]);
                    }
                }
            }

            // --- 2. ¿Sobra algo? Cables que unen redes distintas o bornes ajenos. ---------
            var conexiones = grafo.Conexiones;
            for (int i = 0; i < conexiones.Count; i++)
            {
                var c = conexiones[i];

                string redA, redB;
                bool tieneA = redDe.TryGetValue(c.A, out redA);
                bool tieneB = redDe.TryGetValue(c.B, out redB);

                if (!tieneA || !tieneB)
                {
                    string ajeno = !tieneA ? c.A : c.B;
                    informe.Sobrantes.Add(c + ": " + ajeno + " no interviene en el cableado de esta práctica");
                    continue;
                }

                if (redA != redB)
                {
                    informe.Sobrantes.Add("CORTOCIRCUITO " + c + ": une \"" + redA + "\" con \"" + redB + "\"");
                }
            }

            return informe;
        }

        public static VdfWiringReport Validar(VdfWiringGraph grafo)
        {
            return Validar(grafo, Practica());
        }

        /// <summary>
        /// Cablea correctamente de golpe. Sirve para los tests, para el modo
        /// "saltar cableado" y como referencia de lo que el alumno debe lograr.
        /// </summary>
        public static void AplicarCableadoCorrecto(VdfWiringGraph grafo, List<VdfWiringNet> redes = null)
        {
            if (redes == null) redes = Practica();

            for (int i = 0; i < redes.Count; i++)
            {
                var red = redes[i];
                // Cadena simple: borne 0 con 1, 1 con 2, ... Forma la red con el minimo de cables.
                for (int j = 0; j + 1 < red.Bornes.Length; j++)
                    grafo.Conectar(red.Bornes[j], red.Bornes[j + 1]);
            }
        }
    }
}
