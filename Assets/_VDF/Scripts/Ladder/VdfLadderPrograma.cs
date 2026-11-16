using System.Collections.Generic;

namespace VDF
{
    public static class VdfLadderPrograma
    {
        /// <summary>Construye los peldaños del ejercicio del ventilador, operado con pulsadores.</summary>
        public static List<VdfLadderPeldano> Crear()
        {
            return Crear(VdfModoMando.Pulsadores);
        }

        /// <summary>
        /// Construye los peldaños según con qué se opere. Con pulsadores es el equivalente del
        /// programa original (contador, retardo que para el motor). Con interruptores es el
        /// programa con el que se hizo la práctica en el laboratorio, ver
        /// <see cref="CrearLaboratorio"/>.
        /// </summary>
        public static List<VdfLadderPeldano> Crear(VdfModoMando modo)
        {
            if (modo == VdfModoMando.Interruptores) return CrearLaboratorio();

            var p = new List<VdfLadderPeldano>();

            p.Add(new VdfLadderPeldano
            {
                Titulo = "Enclavamiento del sistema",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "START", VdfSenalPlc.PulsadorStart),
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNC, "STOP", VdfSenalPlc.PulsadorStop),
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNC, "PE", VdfSenalPlc.Emergencia)
                },
                Paralelo = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "HABILIT.", VdfSenalPlc.Habilitado)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, "HABILIT.", VdfSenalPlc.Habilitado)
            });

            // --- 2. Temporizador del cambio de nivel ---------------------------------------
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Retardo entre velocidades",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "CAMBIO", VdfSenalPlc.CambioEnCurso)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Temporizador, "TON  3 s",
                                               VdfSenalPlc.CambioEnCurso)
            });

            // --- 3. Contador de nivel -------------------------------------------------------
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Nivel de velocidad seleccionado",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "HABILIT.", VdfSenalPlc.Habilitado)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Contador, "CTU  1-4",
                                               VdfSenalPlc.NivelMayorIgual, 1)
            });

            // --- 4. Orden de marcha hacia el variador ---------------------------------------
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Marcha: nivel 1 o superior",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "NIVEL>=1",
                                          VdfSenalPlc.NivelMayorIgual, 1)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, "S1", VdfSenalPlc.SalidaS1)
            });

            // --- 5 y 6. Selección multi-velocidad -------------------------------------------
            //
            // La tabla del manual: ninguna -> d1-01, S5 -> d1-02, S6 -> d1-03, ambas -> d1-04.
            // Dibujado como dos ramas en paralelo queda a la vista por qué el nivel 4
            // enciende las dos salidas a la vez.
            p.Add(PeldanoMultiVelocidad("Multi-velocidad 1 (bit bajo)", "S5",
                                        VdfSenalPlc.SalidaS5, 2, 4));
            p.Add(PeldanoMultiVelocidad("Multi-velocidad 2 (bit alto)", "S6",
                                        VdfSenalPlc.SalidaS6, 3, 4));

            // --- 7 a 10. Indicadores V1..V4 --------------------------------------------------
            for (int n = 1; n <= PlcFanLogic.NivelMaximo; n++)
            {
                p.Add(new VdfLadderPeldano
                {
                    Titulo = "Indicador de velocidad " + n,
                    Serie = new List<VdfLadderElemento>
                    {
                        new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "NIVEL=" + n,
                                              VdfSenalPlc.NivelExacto, n),
                        new VdfLadderElemento(VdfLadderSimbolo.ContactoNC, "PE", VdfSenalPlc.Emergencia)
                    },
                    Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, "V" + n,
                                                   VdfSenalPlc.SalidaV, n)
                });
            }

            return p;
        }

        public static List<VdfLadderPeldano> CrearLaboratorio()
        {
            var p = new List<VdfLadderPeldano>();

            // --- 1. El temporizador de los pilotos -----------------------------------------
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Retardo de los pilotos (T#3S)",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "CAMBIO", VdfSenalPlc.CambioEnCurso)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Temporizador, "TON  3 s",
                                               VdfSenalPlc.CambioEnCurso)
            });

            // --- 2. La combinación de S5 y S6 ----------------------------------------------
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Velocidad que piden S5 y S6",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "S4 I0.0", VdfSenalPlc.InterruptorMarcha)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Contador, "SEL S5 S6",
                                               VdfSenalPlc.NivelMayorIgual, 1)
            });

            // --- 3 a 6. Pilotos V1 a V4, cada uno tras su TON ------------------------------
            for (int n = 1; n <= PlcFanLogic.NivelMaximo; n++)
            {
                bool s5 = n == 2 || n == 4;
                bool s6 = n == 3 || n == 4;
                p.Add(new VdfLadderPeldano
                {
                    Titulo = "VELOCIDAD_" + n + " (piloto V" + n + ")",
                    Serie = new List<VdfLadderElemento>
                    {
                        new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "S4 I0.0", VdfSenalPlc.InterruptorMarcha),
                        new VdfLadderElemento(s5 ? VdfLadderSimbolo.ContactoNA : VdfLadderSimbolo.ContactoNC,
                                              "S5 I0.1", VdfSenalPlc.InterruptorS5),
                        new VdfLadderElemento(s6 ? VdfLadderSimbolo.ContactoNA : VdfLadderSimbolo.ContactoNC,
                                              "S6 I0.2", VdfSenalPlc.InterruptorS6),
                        new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "T 3s", VdfSenalPlc.NivelExacto, n)
                    },
                    Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, "V" + n + " Q0." + (n - 1),
                                                   VdfSenalPlc.SalidaV, n)
                });
            }

            // --- 7. Marcha hacia el variador, por S4 ---------------------------------------
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Marcha: Q0.5 a S4 del variador",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "S4 I0.0", VdfSenalPlc.InterruptorMarcha),
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNC, "BLOQ.", VdfSenalPlc.MarchaBloqueada),
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNC, "PE", VdfSenalPlc.Emergencia)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, "H1-04 Q0.5", VdfSenalPlc.SalidaS1)
            });

            // --- 8 y 9. Multi-velocidad, sin retardo ---------------------------------------
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Multi-velocidad 1: Q0.6 a S5",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "S5 I0.1", VdfSenalPlc.InterruptorS5),
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNC, "PE", VdfSenalPlc.Emergencia)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, "H1-05 Q0.6", VdfSenalPlc.SalidaS5)
            });
            p.Add(new VdfLadderPeldano
            {
                Titulo = "Multi-velocidad 2: Q0.7 a S6",
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "S6 I0.2", VdfSenalPlc.InterruptorS6),
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNC, "PE", VdfSenalPlc.Emergencia)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, "H1-06 Q0.7", VdfSenalPlc.SalidaS6)
            });

            return p;
        }

        private static VdfLadderPeldano PeldanoMultiVelocidad(string titulo, string salida,
                                                              VdfSenalPlc senalSalida,
                                                              int nivelA, int nivelB)
        {
            return new VdfLadderPeldano
            {
                Titulo = titulo,
                Serie = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "NIVEL=" + nivelA,
                                          VdfSenalPlc.NivelExacto, nivelA)
                },
                Paralelo = new List<VdfLadderElemento>
                {
                    new VdfLadderElemento(VdfLadderSimbolo.ContactoNA, "NIVEL=" + nivelB,
                                          VdfSenalPlc.NivelExacto, nivelB)
                },
                Bobina = new VdfLadderElemento(VdfLadderSimbolo.Bobina, salida, senalSalida)
            };
        }

        /// <summary>
        /// Comprueba que lo que dibuja el diagrama coincide con lo que hace el PLC.
        ///
        /// Es la red de seguridad que de verdad importa en esta etapa: el diagrama es una
        /// SEGUNDA descripción de la misma lógica, y dos descripciones de lo mismo se
        /// separan en cuanto alguien toca una. Devuelve la lista de desacuerdos.
        /// </summary>
        public static List<string> Contrastar(List<VdfLadderPeldano> peldanos,
                                              VdfEstadoLadder estado)
        {
            var fallos = new List<string>();
            if (peldanos == null || estado == null || estado.Plc == null) return fallos;

            for (int i = 0; i < peldanos.Count; i++)
            {
                var pel = peldanos[i];
                if (pel == null || pel.Bobina == null) continue;

                // Solo se contrastan las bobinas de salida: el temporizador y el contador
                // no son booleanos y se comprueban aparte.
                if (pel.Bobina.Simbolo != VdfLadderSimbolo.Bobina) continue;

                bool segunDiagrama = pel.Energizado(estado);
                bool segunPlc = estado.Leer(pel.Bobina.Senal, pel.Bobina.Parametro);

                if (segunDiagrama != segunPlc)
                {
                    fallos.Add("Peldaño " + (i + 1) + " (" + pel.Titulo + "): el diagrama dice " +
                               segunDiagrama + " y el PLC dice " + segunPlc +
                               " para la bobina " + pel.Bobina.Etiqueta);
                }
            }

            return fallos;
        }
    }
}
