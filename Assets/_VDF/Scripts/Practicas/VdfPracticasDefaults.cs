using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    public static class VdfPracticasDefaults
    {
        /// <summary>Crea las cinco prácticas.</summary>
        public static List<VdfPractica> CrearTodas()
        {
            return new List<VdfPractica>
            {
                CrearReconocimiento(),
                CrearConfiguracion(),
                CrearCableado(),
                CrearOperacion(),
                CrearSeguridadYFallas()
            };
        }

        // =====================================================================================
        // Práctica 1 — Reconocimiento del módulo
        // =====================================================================================

        public static VdfPractica CrearReconocimiento()
        {
            var p = ScriptableObject.CreateInstance<VdfPractica>();
            p.Numero = 1;
            p.Titulo = "Reconocimiento del módulo";
            p.Descripcion =
                "Identificar los componentes del tablero y los terminales del variador. " +
                "No se energiza nada: es una práctica de observación.";
            p.EmpezarConfigurado = true;
            p.EmpezarCableado = false;

            // Reconocimiento: señalar un borne lo identifica, no lo cablea.
            p.PermiteCablear = false;
            p.BloqueaMarcha = true; // aquí el motor no se arranca: eso es la práctica 4
            p.PulsadoresSoloSenalan = true;

            p.Pasos = PasosReconocimiento(false);
            p.PasosConInterruptores = PasosReconocimiento(true);
            return p;
        }

        // La práctica del laboratorio (modo interruptores) cambia el cableado y la
        // configuración del variador, no solo con qué se manda: la marcha llega por S4 desde
        // Q0.5, S5 y S6 salen de Q0.6 y Q0.7, el común 2L va directo a SC, y el variador lleva
        // H1-01 = 14, H1-04 = 40 y E2-01 = 1,9 A. Por eso las prácticas 1 a 3 tienen también
        // un guion con interruptores. Ver VdfModoMando.

        private static List<VdfPasoPractica> PasosReconocimiento(bool lab)
        {
            return new List<VdfPasoPractica>
            {
                Manual("Mira el tablero entero. En los pasos siguientes vas a señalar sus " +
                       "piezas una a una: apúntalas y aprieta el gatillo (o haz clic). " +
                       "Cuando aciertes, aquí arriba te explico qué es cada una.",
                       pista: "Cuando estés listo, pulsa el botón verde de este panel."),

                Identificar("BRK-L1", "Señala el borne F1 del breaker tripolar: es su fase L1.",
                            "El breaker es el bloque gris de arriba a la izquierda, con la " +
                            "etiqueta KBS C32. Debajo tiene tres bornes: L1 es el rojo de la " +
                            "izquierda, rotulado F1.",
                            "BREAKER TRIPOLAR: el interruptor general del módulo. Corta las " +
                            "fases L1, L2 y L3 a la vez y salta solo si hay un cortocircuito " +
                            "o una sobrecarga. Todo el tablero se alimenta a través de él."),

                Identificar("R/L1", "Señala R/L1, la entrada de alimentación del variador.",
                            "Arriba al centro, junto a S/L2. Son solo dos: este V1000 es monofásico de entrada.",
                            "VARIADOR YASKAWA V1000: el aparato con teclado y pantalla del centro. " +
                            "Recibe la red de 220 V por R/L1 y S/L2, la rectifica y con ella " +
                            "fabrica una salida trifásica de frecuencia variable: así decide a qué " +
                            "velocidad gira el motor."),

                // Los nombres de grupo tienen que existir tal cual en el layout: hay un test
                // que lo comprueba, porque una errata aquí dejaría un paso imposible de
                // cumplir sin que nada avisara.
                Grupo("Indicadores de velocidad",
                      "Señala las borneras de los indicadores de velocidad V1 a V4: son ocho, " +
                      "dos debajo de cada piloto.",
                      "Son los pilotos verdes del centro, bajo el rótulo VELOCIDAD. Cada uno " +
                      "tiene dos borneras debajo.",
                      "INDICADORES V1 a V4: el PLC enciende uno por cada velocidad, así " +
                      "se ve de un vistazo en qué nivel está el motor."),

                lab
                ? Grupo(GrupoMandoLaboratorio, "Señala los tres interruptores del PLC, S4, S5 y S6, " +
                      "y el botón rojo de parada de emergencia.",
                      "Los interruptores están encima del PLC, en la caja ENTRADAS DEL PLC; el botón rojo grande, abajo a la " +
                      "derecha del tablero. START, STOP, SUBIR y BAJAR no se usan en esta práctica.",
                      "INTERRUPTORES DEL PLC: son sus entradas I0.0, I0.1 e I0.2. S4 es la orden de " +
                      "marcha: mientras esté abajo, el motor no arranca aunque el tablero tenga tensión. " +
                      "S5 y S6 eligen la velocidad como un número binario: ninguno, la 1; S5, la 2; " +
                      "S6, la 3; los dos, la 4. El botón rojo es la parada de emergencia: corta todo y " +
                      "se queda enclavado hasta que se rearma.")
                : Grupo("Mando", "Señala los cinco pulsadores de mando: START, STOP, SUBIR, BAJAR " +
                      "y el botón rojo de parada de emergencia.",
                      "START verde, STOP rojo y el botón rojo grande de emergencia están en la columna de la derecha; " +
                      "SUBIR y BAJAR, en el centro, junto a los indicadores.",
                      "PULSADORES DE MANDO: START arranca, STOP para, SUBIR y BAJAR cambian la " +
                      "velocidad, y el botón rojo grande es la parada de emergencia: corta todo y se " +
                      "queda enclavado hasta que se rearma. Van al PLC, que decide qué hace el variador."),

                lab
                ? Identificar("PLC-Q0.5", "Ahora el PLC, el bloque de la derecha: señala su salida Q0.5.",
                            "Está sobre la mesa, a la derecha del tablero, con los interruptores encima. Q0.5 es el primer borne del grupo 2L.",
                            "PLC SIEMENS S7-1200: el cerebro del módulo. Ejecuta el programa Ladder: " +
                            "lee los tres interruptores por sus entradas I0.0 a I0.2 y, según lo que manda " +
                            "el programa, activa sus salidas: Q0.0 a Q0.3 encienden los pilotos y Q0.5 a " +
                            "Q0.7 van al variador. Q0.5 le da la marcha por S4.")
                : Identificar("PLC-Q0.7", "Ahora el PLC, el bloque de la derecha: señala su salida Q0.7.",
                            "Está sobre la mesa, a la derecha del tablero. Q0.7 es el último borne de la fila del medio.",
                            "PLC SIEMENS S7-1200: el cerebro del módulo. Ejecuta el programa Ladder: " +
                            "lee los pulsadores por sus entradas I0.0 a I0.4 y, según lo que manda el " +
                            "programa, activa sus salidas Q0.0 a Q0.7. Q0.7 da la marcha al variador por S1."),

                lab
                ? Identificar("S4", "En la regleta, señala el terminal S4 del variador.",
                            "Fila 2 de la regleta, abajo: el cuarto de los S.",
                            "S1 a S6 son las ENTRADAS DIGITALES del variador. En esta práctica la marcha " +
                            "llega por S4: con H1-04 = 40, S4 es la marcha adelante, y mientras el PLC la " +
                            "active, el motor gira. S1 queda como reset de falla (H1-01 = 14).")
                : Identificar("S1", "En la regleta, señala el terminal S1 del variador.",
                            "Fila 2 de la regleta, abajo: el primero de los S.",
                            "S1 a S6 son las ENTRADAS DIGITALES del variador. Con H1-01 = 40, " +
                            "S1 es la marcha adelante: mientras el PLC la active, el motor gira."),

                Identificar("SC", "Señala el terminal SC, el común de las entradas digitales.",
                            "También en la fila 2, después de S6.",
                            "SC es el COMÚN de las entradas S1 a S6. Sin SC conectado al PLC, " +
                            "ninguna entrada funciona, aunque las demás estén bien cableadas."),

                Identificar("U_MOT", "Señala la salida a motor U.",
                            "Bajo el variador, con V y W al lado.",
                            "U, V y W: la SALIDA TRIFÁSICA del variador hacia el motor. Si se " +
                            "intercambian dos de ellas, el motor gira al revés."),

                Identificar("MOT-U", "Y por último el motor: señala su borne U.",
                            "El motor está sobre la mesa, a la izquierda. Sus bornes U, V y W van encima.",
                            (lab ? "MOTOR TRIFÁSICO de 0,25 kW y 1,2 A según su placa (en el laboratorio el variador se programó con 1,9 A por una anomalía del módulo; lo verás en la práctica 2). "
                                 : "MOTOR TRIFÁSICO de 0,25 kW y unos 1,2 A. ") +
                            "No tiene control propio: gira " +
                            "a la velocidad que le marca la frecuencia que recibe del variador por " +
                            "U, V y W."),

                Manual("Mira los tres pilotos de la izquierda: FUNC. en verde, FALLA y E en " +
                       "azul. FALLA se enciende cuando el variador detecta una avería.",
                       pista: "Están en la columna izquierda, cada uno con su par de borneras. " +
                              "Cuando los tengas ubicados, pulsa el botón verde de este panel.")
            };
        }

        // =====================================================================================
        // Práctica 2 — Configuración del variador
        // =====================================================================================

        public static VdfPractica CrearConfiguracion()
        {
            var p = ScriptableObject.CreateInstance<VdfPractica>();
            p.Numero = 2;
            p.Titulo = "Configuración del variador";
            p.Descripcion =
                "Programar el V1000 desde su teclado con los parámetros de la práctica. " +
                "Se empieza desde valores de fábrica.";
            p.EmpezarConfigurado = false;
            p.EmpezarCableado = true;

            p.PermiteCablear = false;
            p.BloqueaMarcha = true; // aquí el motor no se arranca: eso es la práctica 4

            p.Pasos = PasosConfiguracion(false);
            p.PasosConInterruptores = PasosConfiguracion(true);
            return p;
        }

        private static List<VdfPasoPractica> PasosConfiguracion(bool lab)
        {
            var pasos = new List<VdfPasoPractica>
            {
                new VdfPasoPractica
                {
                    Instruccion = "Inicializa el variador desde su teclado: escribe A1-03 = 2220. " +
                                  "Quedará con los valores de fábrica y en secuencia de 2 hilos.",
                    Pista = "En el teclado del V1000: ESC hasta ver F00.00, ▲ hasta -PAr-, ENTER " +
                            "(sale A1-03), ENTER otra vez para editar. ▲ y ▼ cambian el dígito que " +
                            "parpadea, RESET pasa al dígito siguiente. Cuando ponga 2220, ENTER.",
                    Advertencia = "Ojo: al inicializar se pierde la configuración que tuviera el equipo. " +
                                  "A1-03 no guarda un valor, es una orden: solo admite 1110, 2220 o 3330, " +
                                  "y con otro número el teclado muestra oPE y no cambia nada.",
                    Condicion = VdfCondicion.VariadorInicializado
                },

                lab
                ? Parametro("E2-01", 1.9f, "Programa la corriente nominal del motor: E2-01 = 1.9 A.",
                          "En -PAr-, ▲ y ▼ recorren los parámetros hasta E2-01; ENTER para editar, " +
                          "▲▼ y RESET para el valor, ENTER para guardar. Lo normal es poner la corriente de la placa " +
                          "del motor, que dice 1,2 A. En el módulo del laboratorio, con 1,2 A el variador daba un " +
                          "error de poca corriente por anomalías del equipo, y hubo que subirla a 1,9 A para que funcionara.")
                : Parametro("E2-01", 1.2f, "Programa la corriente nominal del motor: E2-01 = 1.2 A.",
                          "En -PAr-, ▲ y ▼ recorren los parámetros hasta E2-01; ENTER para editar, " +
                          "▲▼ y RESET para el valor, ENTER para guardar. Es el dato de la placa del motor."),

                Parametro("E2-11", 0.25f, "Programa la potencia del motor: E2-11 = 0.25 kW.",
                          "Igual que antes: busca E2-11 con ▲▼ en -PAr- y ENTER. Ojo: la potencia del " +
                          "MOTOR, que no es la del variador (0,55 kW)."),

                Revisar("L1-01", 1f, "Entra a ver la protección térmica del motor (ENTER sobre L1-01) y confirma que vale 1.",
                          "Viene así de fábrica. Entra a verlo igualmente: saber qué trae el equipo "
                          + "de serie es parte del trabajo."),

                Revisar("E1-04", 60f, "Entra a ver la frecuencia máxima de salida (ENTER sobre E1-04) y confirma que vale 60 Hz.",
                          "Viene así de fábrica. Es la frecuencia MÁXIMA, no la nominal: ninguna "
                          + "velocidad puede pasar de ella."),

                Parametro("E1-05", 220f, "Fija el voltaje máximo de salida: E1-05 = 220 V.",
                          "Coincide con la tensión de la red del módulo."),

                Parametro("b1-01", 0f, "Haz que la referencia de frecuencia venga del operador: b1-01 = 0.",
                          "Con b1-01 = 1 la velocidad 1 se tomaría de la entrada analógica A1, que no está cableada."),

                Revisar("b1-02", 1f, "Entra a ver b1-02 (ENTER sobre él) y confirma que la orden de marcha viene de los terminales: b1-02 = 1.",
                          lab ? "Viene así de fábrica. Con otro valor, la marcha que el PLC manda por S4 no "
                                + "arrancaría el motor."
                              : "Viene así de fábrica. Con otro valor, las salidas del PLC hacia S1 no "
                                + "arrancarían el motor."),

                Parametro("d1-01", 10f, "Programa la velocidad 1: d1-01 = 10 Hz.", "Cuatro dígitos y punto decimal."),
                Parametro("d1-02", 25f, "Programa la velocidad 2: d1-02 = 25 Hz.", null),
                Parametro("d1-03", 35f, "Programa la velocidad 3: d1-03 = 35 Hz.", null),
                Parametro("d1-04", 50f, "Programa la velocidad 4: d1-04 = 50 Hz.", null),

            };

            if (lab)
            {
                // En el laboratorio la marcha va por S4: se intercambian las funciones de fábrica
                // de S1 (40, marcha) y S4 (14, reset de falla).
                pasos.Add(Parametro("H1-01", 14f, "Deja S1 como reset de falla: H1-01 = 14.",
                          "De fábrica S1 es la marcha (40). En esta práctica la marcha llega por S4, " +
                          "así que S1 pasa a ser el reset de falla, que es lo que S4 traía de fábrica.",
                          error: "H1-01 tiene que valer 14: la marcha de esta práctica va por S4."));
                pasos.Add(Parametro("H1-04", 40f, "Pon la marcha adelante en S4: H1-04 = 40.",
                          "De fábrica S4 es el reset de falla (14). El PLC manda la marcha por Q0.5, que " +
                          "va cableada a S4: con 40, S4 arranca el motor mientras Q0.5 esté activa.",
                          error: "Con H1-04 distinto de 40, S4 no da orden de marcha y el motor no arrancará."));
            }
            else
            {
                pasos.Add(Revisar("H1-01", 40f, "Entra a ver H1-01 (ENTER sobre él) y confirma que S1 tiene la marcha adelante de 2 hilos: H1-01 = 40.",
                          "Viene así de fábrica. Si estuviera en 1 la configuración sería incorrecta: "
                          + "1 es LOCAL/REMOTE, que NO arranca el motor. Para este modelo debe ser 40.",
                          error: "Con H1-01 distinto de 40, S1 no da orden de marcha y el motor no arrancará."));
            }

            pasos.AddRange(new List<VdfPasoPractica>
            {

                Revisar("H1-05", 3f, "Entra a ver H1-05 (ENTER sobre él) y confirma que S5 es la multi-velocidad 1: H1-05 = 3.",
                          "Viene así de fábrica. Si inviertes H1-05 y H1-06, las velocidades 2 y 3 "
                          + "salen cambiadas."),

                Revisar("H1-06", 4f, "Entra a ver H1-06 (ENTER sobre él) y confirma que S6 es la multi-velocidad 2: H1-06 = 4.",
                          "También viene de fábrica. Para salir sin cambiar nada, ESC."),

                new VdfPasoPractica
                {
                    Instruccion = "Comprueba que no queda ningún aviso de configuración.",
                    Pista = "Si algo falta, justo debajo de este paso sale qué parámetro es y qué efecto tendría.",
                    Condicion = VdfCondicion.ConfiguracionSinAvisos
                }
            });

            var fichas = new Dictionary<string, string>
            {
                { "E2-01", lab ? "E2-01, CORRIENTE NOMINAL DEL MOTOR: con ella el variador calcula la protección térmica. Por defecto se pone la de la placa del motor (1,2 A); en esta práctica va a 1,9 A por una anomalía del módulo del laboratorio. Ponerla más alta que la real deja al motor menos protegido, así que en otro equipo se usa la de la placa."
                               : "E2-01, CORRIENTE NOMINAL DEL MOTOR: con ella el variador calcula la protección térmica. Si se pone más alta que la real, el motor se puede quemar sin que el variador lo detecte." },
                { "H1-04", "H1-04, FUNCIÓN DE S4: con 40, S4 es la marcha adelante de 2 hilos. En esta práctica el PLC la activa por Q0.5 mientras el interruptor S4 esté arriba." },
                { "E2-11", "E2-11, POTENCIA DEL MOTOR: el variador la usa para ajustar su control al motor que tiene conectado. Es la del motor (0,25 kW), no la del variador (0,55 kW)." },
                { "L1-01", "L1-01, PROTECCIÓN TÉRMICA: con 1 el variador vigila que el motor no se caliente de más y lo para si hace falta (falla oL1)." },
                { "E1-04", "E1-04, FRECUENCIA MÁXIMA: ninguna velocidad puede pasar de aquí. Si d1-04 pidiera 70 Hz, el motor se quedaría en 60." },
                { "E1-05", "E1-05, VOLTAJE MÁXIMO DE SALIDA: el que el variador da al motor a la frecuencia máxima. Coincide con la tensión del motor, 220 V." },
                { "b1-01", "b1-01, DE DÓNDE VIENE LA VELOCIDAD: con 0, de los parámetros d1-xx guardados en el variador; con 1, de un potenciómetro en la entrada A1." },
                { "b1-02", lab ? "b1-02, DE DÓNDE VIENE LA ORDEN DE MARCHA: con 1, de los terminales (el PLC por S4); con 0, de la tecla RUN del teclado."
                               : "b1-02, DE DÓNDE VIENE LA ORDEN DE MARCHA: con 1, de los terminales (el PLC por S1); con 0, de la tecla RUN del teclado." },
                { "d1-01", "d1-01 a d1-04, LAS CUATRO VELOCIDADES: el PLC elige cuál activando S5 y S6. Ninguna: d1-01; solo S5: d1-02; solo S6: d1-03; las dos: d1-04." },
                { "H1-01", lab ? "H1-01, FUNCIÓN DE S1: con 14, S1 es el reset de falla. Aquí no se cablea; la marcha va por S4."
                               : "H1-01, FUNCIÓN DE S1: con 40, S1 es la marcha adelante de 2 hilos: mientras el PLC la active, el motor gira." },
                { "H1-05", "H1-05 y H1-06, FUNCIÓN DE S5 Y S6: 3 y 4 son las multi-velocidades 1 y 2, las que combinadas eligen entre d1-01 y d1-04." },
            };
            foreach (var paso in pasos)
            {
                string texto;
                if (paso != null && paso.Objetivo != null && fichas.TryGetValue(paso.Objetivo, out texto))
                    paso.Explicacion = texto;
            }

            return pasos;
        }

        // =====================================================================================
        // Práctica 3 — Cableado
        // =====================================================================================

        public static VdfPractica CrearCableado()
        {
            var p = ScriptableObject.CreateInstance<VdfPractica>();
            p.Numero = 3;
            p.Titulo = "Cableado del módulo";
            p.Descripcion =
                "Conectar con jumpers la alimentación, las salidas del PLC hacia el variador " +
                "y hacia los indicadores, y la salida a motor.";
            p.EmpezarConfigurado = true;
            p.EmpezarCableado = false;

            p.Pasos = PasosCableado(false);
            p.PasosConInterruptores = PasosCableado(true);
            return p;
        }

        private static List<VdfPasoPractica> PasosCableado(bool lab)
        {
            var pasos = new List<VdfPasoPractica>
            {
                Manual("Antes de cablear, comprueba que el breaker está abierto. En el simulador " +
                       "ya lo está: no hay que tocar nada, pulsa LISTO.",
                       pista: "Aquí no hay que mover nada: en el simulador el breaker ya está " +
                              "abierto y el tablero no recibe tensión hasta que el cableado esté " +
                              "completo. En el equipo real sí hay que comprobarlo. Pulsa el " +
                              "botón verde de este panel para seguir.",
                       advertencia: "Nunca se manipulan bornes con el tablero energizado."),

                Unir("BRK-L1|R/L1", "Lleva la fase L1 del breaker a la entrada R/L1 del variador.",
                     "Coge una punta de cable con el gatillo LATERAL (el del dedo medio), llévala " +
                     "al borne que brilla y suéltala: encaja sola. Luego la otra punta al otro borne. " +
                     "Si te equivocas, cógela y suéltala en el aire."),

                lab
                ? Unir("BRK-L3|S/L2", "Lleva F2, el borne derecho de la salida del breaker, a la entrada S/L2.",
                     "El V1000 de este módulo es monofásico de entrada: solo R/L1 y S/L2. El borne del " +
                     "centro del breaker no se usa: la práctica sale de F1 y F2.")
                : Unir("BRK-L2|S/L2", "Lleva la fase L2 del breaker a la entrada S/L2.",
                     "El V1000 de este módulo es monofásico de entrada: solo R/L1 y S/L2."),

            };

            if (lab)
            {
                pasos.Add(Unir("PLC-2L|SC", "Une el común 2L de las salidas del PLC con el común SC de las entradas del variador.",
                     "Las salidas Q0.5 a Q0.7 son contactos de relé: cada una cierra el circuito entre " +
                     "su borne y 2L. Con 2L unido a SC, al cerrarse Q0.5 queda S4 unido a SC, y eso es " +
                     "lo que el variador entiende como entrada activa."));
                pasos.Add(Unir("PLC-Q0.5|S4", "Conecta la salida Q0.5 del PLC a la entrada S4 del variador.",
                     "Q0.5 es la marcha: el programa la activa mientras el interruptor S4 esté arriba. En el " +
                     "variador, S4 la recibe como orden de marcha porque en la práctica 2 pusiste H1-04 = 40."));
                pasos.Add(Unir("PLC-Q0.6|S5", "Conecta la salida Q0.6 del PLC a la entrada S5 (multi-velocidad 1).",
                     "Q0.6 copia el interruptor S5, y Q0.7 el S6."));
                pasos.Add(Unir("PLC-Q0.7|S6", "Conecta la salida Q0.7 del PLC a la entrada S6 (multi-velocidad 2).", null));
                pasos.Add(Unir("BRK-L1|PLC-1L", "Lleva la fase L1 (F1) al común 1L del PLC.",
                     "1L alimenta Q0.0 a Q0.3, las salidas de los pilotos. Los pilotos son de 220 V: " +
                     "reciben la fase por el PLC y vuelven por F2."));
                pasos.Add(Unir("PLC-Q0.0|V1-A", "Conecta la salida Q0.0 del PLC al indicador V1.",
                     "Por el borne positivo del piloto. El negativo va a F2, como los de V2 a V4. " +
                     "Q0.1, Q0.2 y Q0.3 van igual a V2, V3 y V4: el último paso te los pide."));
            }
            else
            {
                pasos.AddRange(new List<VdfPasoPractica>
                {
                Unir("VCC24|PLC-2L", "Lleva +24 V al común 2L de las salidas del PLC.",
                     "Es el borne 2L del PLC, no el 1L: 2L alimenta Q0.5, Q0.6 y Q0.7, que van a " +
                     "las entradas S del variador. 1L va a fase, para los indicadores."),

                Unir("GND24|SC", "Lleva GND (el 0 V de la fuente de 24 V) al común SC de las entradas digitales del variador.",
                     "Sin el común, ninguna entrada S puede cerrar circuito."),

                Unir("PLC-Q0.7|S1", "Conecta la salida Q0.7 del PLC a la entrada S1 del variador.",
                     "Q0.7 es la salida que el programa del PLC activa para marchar. En el variador, " +
                     "S1 la recibe como orden de marcha porque en la práctica 2 pusiste H1-01 = 40."),

                Unir("PLC-Q0.6|S5", "Conecta la salida Q0.6 del PLC a la entrada S5 (multi-velocidad 1).",
                     "Las tres entradas del variador salen del grupo 2L, que es el que está "
                     + "a 24 V; el grupo 1L alimenta los indicadores a 220 V."),
                Unir("PLC-Q0.5|S6", "Conecta la salida Q0.5 del PLC a la entrada S6 (multi-velocidad 2).", null),

                Unir("PLC-Q0.0|V1-A", "Conecta la salida Q0.0 del PLC al indicador V1.",
                     "Los indicadores son de 220 V y van al común 1L, que está a fase.")
                });
            }

            pasos.AddRange(new List<VdfPasoPractica>
            {
                Unir("U_MOT|MOT-U", "Conecta la salida U del variador al motor.", null),
                Unir("V_MOT|MOT-V", "Conecta la salida V del variador al motor.", null),
                Unir("W_MOT|MOT-W", "Conecta la salida W del variador al motor.",
                     "Si inviertes dos fases, el motor girará al revés. No es peligroso, pero se nota."),

                new VdfPasoPractica
                {
                    Instruccion = "Completa el resto del cableado hasta que no falte ninguna unión.",
                    Pista = "Justo debajo de este paso sale cuántas uniones faltan y cuál es la primera.",
                    Condicion = VdfCondicion.CableadoCorrecto,
                    MensajeError = "Revisa las uniones que el panel marca como faltantes."
                },

                new VdfPasoPractica
                {
                    Instruccion = "Comprueba que no has dejado ningún cable de más.",
                    Pista = "Un cable entre dos redes distintas es un cortocircuito y se marca como tal.",
                    Condicion = VdfCondicion.SinCablesSobrantes,
                    MensajeError = "Hay cables que unen redes que no deben tocarse."
                }
            });

            return pasos;
        }

        // =====================================================================================
        // Práctica 4 — Operación
        // =====================================================================================

        public static VdfPractica CrearOperacion()
        {
            var p = ScriptableObject.CreateInstance<VdfPractica>();
            p.Numero = 4;
            p.Titulo = "Operación del módulo";
            p.Descripcion =
                "Arrancar el sistema, recorrer las cuatro velocidades viendo el retardo de 3 s " +
                "del PLC en el diagrama Ladder, volver a la 1 y parar.";
            p.EmpezarConfigurado = true;
            p.EmpezarCableado = true;

            // Se opera con los pulsadores, no se cablea: mismo motivo que en la práctica 2.
            p.PermiteCablear = false;

            p.Pasos = new List<VdfPasoPractica>
            {
                new VdfPasoPractica
                {
                    Instruccion = "Comprueba que el variador tiene tensión: su pantalla tiene que estar encendida.",
                    Pista = "Si la pantalla del variador está encendida, tiene tensión. " +
                            "Este paso se marca solo.",
                    Condicion = VdfCondicion.VariadorConTension,
                    Advertencia = "A partir de aquí el tablero está energizado: no toques los bornes."
                },

                new VdfPasoPractica
                {
                    Instruccion = "Pulsa START, el botón verde del tablero, para habilitar el sistema. No la tecla RUN del variador.",
                    Pista = "START habilita, pero no arranca el motor todavía: eso lo hace subir velocidad.",
                    Condicion = VdfCondicion.SistemaArrancado
                },

                Nivel(1, "Pulsa SUBIR para pasar a la velocidad 1 (10 Hz).",
                      "El cambio tarda unos 3 s. 10 Hz (d1-01): S1 = 1, S5 = 0, S6 = 0. Puedes " +
                      "seguirlo en el diagrama Ladder."),

                Nivel(2, "Sube a la velocidad 2 (25 Hz).",
                      "25 Hz (d1-02): S1 = 1, S5 = 1, S6 = 0. En el tablero se enciende V2."),

                Nivel(3, "Sube a la velocidad 3 (35 Hz).",
                      "35 Hz (d1-03): S1 = 1, S5 = 0, S6 = 1."),

                Nivel(4, "Sube a la velocidad 4 (50 Hz).",
                      "50 Hz (d1-04): S1 = 1, S5 = 1, S6 = 1."),

                new VdfPasoPractica
                {
                    Instruccion = "Baja hasta la velocidad 1, una pulsación por nivel.",
                    Pista = "Ojo: una pulsación más desde la velocidad 1 para el motor, porque la cuenta llega a cero.",
                    Condicion = VdfCondicion.BajoHastaNivel1
                },

                new VdfPasoPractica
                {
                    Instruccion = "Pulsa STOP para parar el motor y reiniciar la selección.",
                    Pista = "Tras STOP hay que volver a pulsar START para poder operar otra vez.",
                    Condicion = VdfCondicion.ParadoTrasOperar
                }
            };

            p.PasosConInterruptores = PasosOperacionConInterruptores();
            return p;
        }

        /// <summary>
        /// La práctica 4 operada con los tres interruptores junto al PLC en vez de con los
        /// pulsadores (ver <see cref="VdfModoMando"/>). Mismas condiciones y mismo orden:
        /// solo cambia con qué se manda. MARCHA arriba hace de START y abajo de STOP; S5 y S6
        /// eligen la velocidad con la tabla del variador.
        /// </summary>
        public static List<VdfPasoPractica> PasosOperacionConInterruptores()
        {
            return new List<VdfPasoPractica>
            {
                new VdfPasoPractica
                {
                    Instruccion = "Comprueba que el variador tiene tensión: su pantalla tiene que estar encendida.",
                    Pista = "Si la pantalla del variador está encendida, tiene tensión. " +
                            "Este paso se marca solo.",
                    Condicion = VdfCondicion.VariadorConTension,
                    Advertencia = "A partir de aquí el tablero está energizado: no toques los bornes."
                },

                new VdfPasoPractica
                {
                    Instruccion = "Sube el interruptor S4, en la caja ENTRADAS DEL PLC, encima del PLC. Deja S5 y S6 abajo.",
                    Pista = "El interruptor S4 es la entrada I0.0. Arriba, el PLC activa Q0.5 y el variador recibe la " +
                            "marcha por S4. No uses la tecla RUN del variador.",
                    Condicion = VdfCondicion.SistemaArrancado
                },

                Nivel(1, "El motor arranca hacia la velocidad 1 (10 Hz). Espera a que se encienda el piloto V1.",
                      "El motor arranca en el acto, con la rampa del variador; el piloto V1 tarda 3 s, " +
                      "lo que tarda su temporizador en el Ladder. 10 Hz (d1-01): S4 = 1, S5 = 0, S6 = 0."),

                Nivel(2, "Sube S5 para pasar a la velocidad 2 (25 Hz).",
                      "25 Hz (d1-02): S4 = 1, S5 = 1, S6 = 0. El motor cambia de velocidad sin pararse; " +
                      "el piloto V2 se enciende 3 s después."),

                Nivel(3, "Baja S5 y sube S6: velocidad 3 (35 Hz).",
                      "35 Hz (d1-03): S4 = 1, S5 = 0, S6 = 1. El piloto V3 vuelve a tardar 3 s."),

                Nivel(4, "Sube también S5: con los dos arriba, velocidad 4 (50 Hz).",
                      "50 Hz (d1-04): S4 = 1, S5 = 1, S6 = 1."),

                new VdfPasoPractica
                {
                    Instruccion = "Vuelve a la velocidad 1: baja S5 y S6.",
                    Pista = "Con los dos abajo, el variador toma d1-01. El piloto V1 vuelve a tardar 3 s.",
                    Condicion = VdfCondicion.BajoHastaNivel1
                },

                new VdfPasoPractica
                {
                    Instruccion = "Baja S4 para parar el motor.",
                    Pista = "S4 abajo abre Q0.5: el variador pierde la marcha y frena con su rampa. " +
                            "Los pilotos se apagan en el acto.",
                    Condicion = VdfCondicion.ParadoTrasOperar
                }
            };
        }

        // =====================================================================================
        // Práctica 5 — Seguridad y fallas
        // =====================================================================================

        public static VdfPractica CrearSeguridadYFallas()
        {
            var p = ScriptableObject.CreateInstance<VdfPractica>();
            p.Numero = 5;
            p.Titulo = "Seguridad y diagnóstico de fallas";
            p.Descripcion =
                "Trabajar con el módulo energizado de forma segura, usar la parada de " +
                "emergencia y diagnosticar una avería a partir de lo que se observa.";
            p.EmpezarConfigurado = true;
            p.EmpezarCableado = true;

            p.Pasos = PasosSeguridadYFallas(false);
            p.PasosConInterruptores = PasosSeguridadYFallas(true);
            return p;
        }

        /// <summary>
        /// El guion de la práctica 5. Es el mismo con pulsadores y con interruptores; solo
        /// cambian las frases que dicen con qué se arranca y se para.
        /// </summary>
        private static List<VdfPasoPractica> PasosSeguridadYFallas(bool interruptores)
        {
            string arrancar = interruptores
                ? "arranca el sistema con el interruptor S4, encima del PLC."
                : "arranca el sistema con START y SUBIR, los botones del tablero.";
            string comoArranca = interruptores
                ? "S4 arriba arranca el motor en la velocidad 1; su piloto se enciende a los 3 s."
                : "START habilita; subir velocidad es lo que arranca el motor.";
            string antesDeEmergencia = interruptores
                ? "Primero S4 arriba, y espera a que el motor gire. "
                : "Primero START y SUBIR, y espera a que el motor gire. ";
            string noBastaStart = interruptores
                ? "Por eso no basta con volver a subir S4."
                : "Por eso no basta con volver a pulsar START.";
            string trasRearmar = interruptores
                ? "Rearmar libera el enclavamiento, pero el sistema queda en reposo: " +
                  "baja S4 y vuelve a subirlo."
                : "Rearmar libera el enclavamiento, pero el sistema queda en reposo: " +
                  "hay que pulsar START otra vez.";
            string operar = interruptores
                ? "Opera con él: S4 arriba y recorre las velocidades con S5 y S6 hasta la 4."
                : "Opera con él: START, SUBIR hasta la 4 y BAJAR.";
            string pararParaParametro = interruptores
                ? "Si es un parámetro: para el motor bajando S4 y cámbialo en el teclado. "
                : "Si es un parámetro: para el motor con STOP y cámbialo en el teclado. ";

            return new List<VdfPasoPractica>
            {
                // --- Seguridad antes de tocar nada -----------------------------------------
                Manual("Antes de energizar, comprueba que no hay nadie manipulando bornes y " +
                       "que el motor puede girar libremente. Aquí basta con mirar: pulsa LISTO.",
                       pista: "En el equipo real esto no es un trámite: el motor arranca solo " +
                              "en cuanto el PLC da la orden. Aquí basta con mirar; pulsa el " +
                              "botón verde de este panel para seguir.",
                       advertencia: "NUNCA se manipulan bornes con el tablero energizado. " +
                                    "En el equipo real se abre el breaker antes de tocar un cable."),

                Manual("Localiza la parada de emergencia antes de necesitarla, y comprueba " +
                       "que la alcanzas sin moverte del puesto.",
                       pista: "Es el botón rojo grande de la columna derecha.",
                       advertencia: "Si algo va mal, la parada de emergencia es lo primero: primero se corta, " +
                                    "después se diagnostica."),

                new VdfPasoPractica
                {
                    Instruccion = "Comprueba que el variador tiene tensión (pantalla encendida) y " + arrancar,
                    Pista = comoArranca,
                    Condicion = VdfCondicion.SistemaArrancado,
                    Advertencia = "A partir de aquí el tablero está energizado."
                },

                // --- Parada de emergencia ---------------------------------------------------
                new VdfPasoPractica
                {
                    Instruccion = "Con el motor girando, acciona la parada de emergencia.",
                    Pista = antesDeEmergencia + "Fíjate en que " +
                            "la frecuencia cae a cero de golpe, sin rampa: la parada de emergencia no desacelera, corta.",
                    Condicion = VdfCondicion.EmergenciaConMotorGirando
                },

                Manual("Observa el piloto de FALLA del tablero y el indicador ALM del operador " +
                       "del variador. Cuando los hayas visto, pulsa LISTO.",
                       pista: "El variador queda en estado de falla, no simplemente parado. " +
                              noBastaStart),

                new VdfPasoPractica
                {
                    Instruccion = "Rearma la parada de emergencia y vuelve a dejar el sistema listo.",
                    Pista = trasRearmar,
                    Condicion = VdfCondicion.EmergenciaRearmada,
                    Advertencia = "Antes de rearmar, asegúrate de que la causa que te hizo " +
                                  "parar ya no está."
                },

                // --- Diagnóstico -------------------------------------------------------------
                Manual("A partir de aquí el módulo tiene una avería provocada. " + operar +
                       " Fíjate en el motor, en los indicadores V1 a V4 y en " +
                       "la pantalla del variador. Todavía no cambies nada: solo observa qué falla. " +
                       "Cuando lo tengas, pulsa LISTO.",
                       pista: "Compara lo que ves con lo que debería pasar según la tabla de " +
                              "la práctica. La diferencia es la pista."),

                new VdfPasoPractica
                {
                    Instruccion = "Elige la causa de la avería en el panel «¿Cuál es la causa?», a tu derecha: apunta a una de las seis y aprieta el gatillo.",
                    Pista = "Piensa en qué parte de la cadena falla: ¿el PLC manda? ¿el " +
                            "variador recibe? ¿el variador entiende lo que recibe?",
                    Condicion = VdfCondicion.FallaDiagnosticada,
                    MensajeError = "Esa no es la causa. Vuelve a observar el síntoma."
                },

                new VdfPasoPractica
                {
                    Instruccion = "Repara la avería y comprueba que el módulo vuelve a " +
                                  "funcionar en las cuatro velocidades.",
                    Pista = "Si era un parámetro, corrígelo desde el teclado; si era un " +
                            "cable, vuelve a conectarlo.",
                    Condicion = VdfCondicion.FallaReparadaYProbada,
                    Advertencia = pararParaParametro +
                                  "Si es un cable: en el equipo real se abre el breaker primero; en el " +
                                  "simulador el breaker no se acciona y puedes tocarlo con el tablero encendido."
                },

                new VdfPasoPractica
                {
                    Instruccion = "Comprueba que no queda ningún aviso de configuración.",
                    Pista = "Si algo falta, justo debajo de este paso sale qué parámetro es y qué efecto tendría.",
                    Condicion = VdfCondicion.ConfiguracionSinAvisos
                }
            };
        }

        // =====================================================================================
        // Atajos de construcción
        // =====================================================================================

        private static VdfPasoPractica Manual(string instruccion, string pista = null,
                                              string advertencia = null)
        {
            return new VdfPasoPractica
            {
                Instruccion = instruccion,
                Pista = pista,
                Advertencia = advertencia,
                Condicion = VdfCondicion.Manual
            };
        }

        private static VdfPasoPractica Identificar(string id, string instruccion, string pista,
                                                  string explicacion = null)
        {
            return new VdfPasoPractica
            {
                Instruccion = instruccion,
                Pista = pista,
                Explicacion = explicacion,
                Condicion = VdfCondicion.ComponenteIdentificado,
                Objetivo = id,
                MensajeError = "Ese no es el componente que se pide."
            };
        }

        /// <summary>Grupo de la práctica 1 del laboratorio: los tres interruptores y la parada de emergencia.</summary>
        public const string GrupoMandoLaboratorio = "Mando del laboratorio";

        private static VdfPasoPractica Grupo(string grupo, string instruccion, string pista,
                                             string explicacion = null)
        {
            return new VdfPasoPractica
            {
                Instruccion = instruccion,
                Pista = pista,
                Explicacion = explicacion,
                Condicion = VdfCondicion.GrupoIdentificado,
                Objetivo = grupo
            };
        }

        /// <summary>
        /// Un paso de «entra a ver X y confirma»: exige haber entrado en el parámetro con
        /// ENTER y que valga lo que debe. Ver <see cref="VdfCondicion.ParametroRevisado"/>.
        /// </summary>
        private static VdfPasoPractica Revisar(string id, float valor, string instruccion,
                                               string pista, string error = null)
        {
            var paso = Parametro(id, valor, instruccion, pista, error);
            paso.Condicion = VdfCondicion.ParametroRevisado;
            return paso;
        }

        private static VdfPasoPractica Parametro(string id, float valor, string instruccion,
                                                 string pista, string error = null)
        {
            return new VdfPasoPractica
            {
                Instruccion = instruccion,
                Pista = pista,
                MensajeError = error ?? ("El parámetro " + id + " todavía no vale lo que se pide."),
                Condicion = VdfCondicion.ParametroEnValor,
                Objetivo = id,
                Valor = valor
            };
        }

        private static VdfPasoPractica Unir(string bornes, string instruccion, string pista)
        {
            return new VdfPasoPractica
            {
                Instruccion = instruccion,
                Pista = pista,
                MensajeError = "Esos dos bornes todavía no quedan unidos.",
                Condicion = VdfCondicion.BornesUnidos,
                Objetivo = bornes
            };
        }

        private static VdfPasoPractica Nivel(int nivel, string instruccion, string pista)
        {
            return new VdfPasoPractica
            {
                Instruccion = instruccion,
                Pista = pista,
                Condicion = VdfCondicion.NivelAlcanzado,
                Valor = nivel
            };
        }
    }
}
