using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    public static class VdfPanelLayoutDefaults
    {
        // --- Paleta ---------------------------------------------------------------------
        private static readonly Color Verde = new Color(0.16f, 0.72f, 0.26f);
        private static readonly Color Rojo = new Color(0.85f, 0.16f, 0.14f);
        private static readonly Color Ambar = new Color(0.95f, 0.68f, 0.08f);
        private static readonly Color Negro = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color Azul = new Color(0.15f, 0.38f, 0.78f);
        private static readonly Color Amarillo = new Color(0.93f, 0.85f, 0.10f);
        private static readonly Color GrisOscuro = new Color(0.28f, 0.29f, 0.31f);
        private static readonly Color Marfil = new Color(0.88f, 0.87f, 0.83f);
        private static readonly Color BorneRojo = new Color(0.78f, 0.14f, 0.12f);
        private static readonly Color BorneNegro = new Color(0.10f, 0.10f, 0.11f);
        private static readonly Color BorneGris = new Color(0.55f, 0.56f, 0.58f);
        private static readonly Color BorneVerde = new Color(0.14f, 0.55f, 0.22f);

        /// <summary>
        /// El azul de la serigrafía del módulo real. Todos los rótulos del tablero van en
        /// este color sobre placa blanca, no en negro sobre chapa (ver la foto en
        /// Docs/Referencia/fotos).
        /// </summary>
        private static readonly Color Serigrafia = new Color(0.11f, 0.24f, 0.62f);

        /// <summary>
        /// Los pilotos de FALLA y de E del módulo real son AZULES, los dos. El de FALLA
        /// estaba en rojo y el de E en ámbar, que es lo que dice el enunciado; la foto del
        /// módulo manda sobre el enunciado y resuelve dos preguntas que quedaban abiertas.
        /// </summary>
        private static readonly Color PilotoAzul = new Color(0.15f, 0.35f, 0.80f);

        /// <summary>El rojo con el que están impresos los nombres al pie del tablero.</summary>
        private static readonly Color Rotulo = new Color(0.74f, 0.13f, 0.16f);

        public const float AnchoTablero = 0.500f;

        /// <summary>
        /// Alto de la lámina.
        ///
        /// Estaba en 0,38 m, que era la suma estimada de las piezas. El flexómetro da 30 cm
        /// justos. Los 8 cm que sobraban estaban todos por debajo de la regleta, que era la
        /// única zona del tablero sin ninguna referencia con la que contrastar.
        /// </summary>
        public const float AltoTablero = 0.300f;

        /// <summary>El alto en milímetros, para traducir las medidas de la foto.</summary>
        private const float AltoMm = 300f;

        // --- Regleta inferior ------------------------------------------------------------

        /// <summary>Primer borne de cada fila, medido sobre la foto rectificada.</summary>
        private const float RegletaX0Fila1 = 0.0588f;
        private const float RegletaX0Fila2 = 0.0614f;

        /// <summary>
        /// Paso entre bornes de la regleta: 26,6 mm.
        ///
        /// Medido, no supuesto. Los quince bornes de la fila 1 van del milímetro 58,8 al
        /// 431,8, y 373 entre 14 huecos dan 26,64. Antes estaba en 25 mm «porque una banana
        /// de 4 mm se monta entre 20 y 25», que era razonable pero acumulaba 24 mm de error
        /// de punta a punta de la fila.
        /// </summary>
        private const float RegletaPaso = 0.0266f;

        /// <summary>Las dos filas, a 234 y 272 mm del borde superior.</summary>
        private const float RegletaFila1Y = 0.066f;
        private const float RegletaFila2Y = 0.028f;

        /// <summary>
        /// Lo que sobresale la plancha de la regleta: casi nada.
        ///
        /// Estaba en 8 mm y luego en 2, y en los dos casos tapaba la serigrafia del propio
        /// recuadro de la regleta, que va impresa en la lamina. En el modulo real no hay
        /// ninguna plancha levantada ahi: las bananas estan montadas directamente sobre el
        /// laminado. El bloque se conserva porque de el cuelgan la caja de colision y la
        /// excepcion del importador PBR.
        /// </summary>
        private const float RegletaGrosor = 0.0003f;

        /// <summary>Fila 1 de la regleta inferior, en el orden del manual del módulo.</summary>
        public static readonly string[] RegletaFila1 =
        {
            "R+", "R-", "S+", "S-", "IG", "P1", "P2", "PC", "A1", "A2", "+V", "AC", "AM", "AC", "MP"
        };

        /// <summary>
        /// Fila 2 de la regleta inferior: QUINCE bornes, no diecisiete.
        ///
        /// El manual del módulo mete U, V y W en esta fila. En el módulo real no están aquí:
        /// la salida a motor es un grupo aparte, justo debajo del variador, y así se ve en la
        /// foto. Contarlos dos veces era lo que estiraba la fila. El hueco sin rotular entre
        /// RP y MA sí existe: hay banana pero no lleva nombre serigrafiado.
        /// </summary>
        public static readonly string[] RegletaFila2 =
        {
            "S1", "S2", "S3", "S4", "S5", "S6", "SC", "HC", "H1", "H2", "RP", "", "MA", "MB", "MC"
        };

        public static VdfPanelLayout Crear()
        {
            var layout = ScriptableObject.CreateInstance<VdfPanelLayout>();
            layout.Ancho = AnchoTablero;
            layout.Alto = AltoTablero;
            layout.Grosor = 0.012f;
            // Placa blanca, como la del módulo real: era chapa metálica cepillada y no se
            // parecía en nada a la foto.
            layout.ColorTablero = new Color(0.985f, 0.985f, 0.99f);
            layout.Elementos = CrearElementos();
            return layout;
        }

        /// <summary>Milímetros medidos desde el borde izquierdo de la lámina, a metros.</summary>
        private static float X(float mm) { return mm * 0.001f; }

        /// <summary>
        /// Milímetros medidos desde el borde SUPERIOR, a metros contados desde el inferior,
        /// que es como trabaja el layout.
        ///
        /// Existe porque las medidas se leen de la foto de arriba abajo, y traducir cada una
        /// a mano era la manera segura de colar un error: así la cifra que aparece escrita en
        /// el código es literalmente la que se midió sobre la imagen rectificada.
        /// </summary>
        private static float Y(float mm) { return (AltoMm - mm) * 0.001f; }

        /// <summary>
        /// Una etiqueta serigrafiada, con su posición en milímetros medidos. Sin color, va en
        /// el azul de la serigrafía, que es el de casi todo el tablero.
        /// </summary>
        private static VdfPanelElement Etq(string id, string texto, float xMm, float yMm,
                                           float ancho, float alto, string grupo)
        {
            return Etq(id, texto, xMm, yMm, ancho, alto, grupo, Serigrafia);
        }

        private static VdfPanelElement Etq(string id, string texto, float xMm, float yMm,
                                           float ancho, float alto, string grupo, Color color)
        {
            return new VdfPanelElement(id, texto, VdfElementType.Etiqueta,
                X(xMm), Y(yMm), color, grupo)
            { Tamano = new Vector2(ancho, alto) };
        }

        public static List<VdfPanelElement> CrearElementos()
        {
            var e = new List<VdfPanelElement>();

            // =============================================================================
            // Columna izquierda: pilotos FUNC. / FALLA / E
            // =============================================================================
            // Los tres a x = 62 mm, separados unos 62 mm entre sí. El rótulo va en su cajita
            // A LA IZQUIERDA del piloto, no debajo, y el par de bornes a la derecha en
            // vertical: así está en el módulo real y así lo dan las medidas.
            AgregarPilotoConBornes(e, "FUNC", "FUNC.", Verde, 62f, 66f, "Pilotos de estado", 64.9f);
            AgregarPilotoConBornes(e, "FALLA", "FALLA", PilotoAzul, 62f, 126f, "Pilotos de estado", 124f);
            AgregarPilotoConBornes(e, "E", "E", PilotoAzul, 62f, 190f, "Pilotos de estado", 184.6f);

            // El módulo real lleva «220v» escrito a mano con marcador junto a cada cosa que
            // trabaja a esa tensión. Es parte de cómo se ve el tablero, no un adorno.
            e.Add(Etq("ETQ_220_FUNC", "220 V", 67f, 44f, 0.026f, 0.0058f, "Pilotos de estado"));
            e.Add(Etq("ETQ_220_FALLA", "220 V", 67f, 102.5f, 0.026f, 0.0058f, "Pilotos de estado"));
            e.Add(Etq("ETQ_220_E", "220 V", 67f, 166f, 0.026f, 0.0058f, "Pilotos de estado"));

            e.Add(new VdfPanelElement("BREAKER", "BREAKER 3P", VdfElementType.Bloque,
                X(148f), Y(95f), new Color(0.92f, 0.91f, 0.85f), "Alimentacion")
            { Tamano = new Vector2(0.0525f, 0.085f), MostrarEtiqueta = false });

            // No lleva ningun «220 V» sobre el magnetotermico: en esa franja la lamina
            // esta limpia. Era un invento heredado de cuando el rotulo del breaker estorbaba.

            // Salida del magnetotérmico: tres bornes a 26 mm, rotulados F1 y F2 en los
            // extremos, que es como está serigrafiado el módulo.
            e.Add(new VdfPanelElement("BRK-L1", "F1", VdfElementType.Borne,
                X(120f), Y(168f), BorneRojo, "Alimentacion") { EtiquetaArriba = true });
            e.Add(new VdfPanelElement("BRK-L2", "", VdfElementType.Borne,
                X(145f), Y(168f), BorneNegro, "Alimentacion") { MostrarEtiqueta = false });
            e.Add(new VdfPanelElement("BRK-L3", "F2", VdfElementType.Borne,
                X(171f), Y(168f), BorneRojo, "Alimentacion") { EtiquetaArriba = true });

            // Alimentación de control 24 V y tierra, las tres en la misma fila.
            e.Add(new VdfPanelElement("VCC24", "", VdfElementType.Borne,
                X(122f), Y(201f), BorneRojo, "Mando 24V") { MostrarEtiqueta = false });
            e.Add(new VdfPanelElement("GND24", "", VdfElementType.Borne,
                X(148f), Y(201f), BorneNegro, "Mando 24V") { MostrarEtiqueta = false });
            // Dentro del recuadro y ENCIMA de las bananas, que es donde esta. Puesto
            // debajo caia 28 mm mas abajo de la cuenta y se montaba con los nombres de la
            // regleta. Lleva segunda linea, «IN», bajo el «24V».
            e.Add(Etq("ETQ_24V", "VCC  24V  GND", 136f, 183.5f, 0.054f, 0.0068f, "Mando 24V"));
            e.Add(Etq("ETQ_24V_IN", "IN", 136f, 190.5f, 0.016f, 0.0105f, "Mando 24V"));

            // En el modulo real no hay banana verde ni rotulo «PE»: hay un tornillo a
            // (175,5 ; 186,5) y debajo el simbolo de tierra dibujado. Se conserva como borne
            // porque el cableado de la practica lo usa, pero en su sitio y sin el rotulo
            // inventado, que ademas caia encima del nombre de IG en la regleta.
            e.Add(new VdfPanelElement("TIERRA", "", VdfElementType.Borne,
                X(175.5f), Y(188f), BorneGris, "Tierra")
            { Tamano = new Vector2(0.008f, 0.008f), MostrarEtiqueta = false });

            // =============================================================================
            // Arriba al centro: entrada de alimentación del variador
            // =============================================================================
            e.Add(new VdfPanelElement("R/L1", "R/L1", VdfElementType.Borne,
                X(221.5f), Y(62f), BorneRojo, "Entrada variador")
            { EtiquetaArriba = true, SeparacionEtiqueta = 0.0108f });
            e.Add(new VdfPanelElement("S/L2", "S/L2", VdfElementType.Borne,
                X(247.5f), Y(62f), BorneRojo, "Entrada variador")
            { EtiquetaArriba = true, SeparacionEtiqueta = 0.0108f });

            // L/L3 está serigrafiado pero NO tiene borne: el agujero está tapado.
            //
            // No es un error del tablero. El variador del módulo es un CIMR-VCBA0003BAA, y su
            // placa de características dice «200V 1PHASE»: es monofásico de entrada, así que
            // la tercera fase no existe. Durante un tiempo se quitó la etiqueta entera del
            // simulador, que tampoco es lo que se ve: el rótulo está, lo que falta es la
            // banana.
            // El rotulo cuelga del propio tapon y no es una etiqueta suelta: asi sale
            // exactamente del mismo tamano y a la misma altura que los de R/L1 y S/L2.
            // Puesto aparte salia mas grande, mas alto y pisando el borde del recuadro.
            e.Add(new VdfPanelElement("TAPON_L3", "L/L3", VdfElementType.Bloque,
                X(273f), Y(62f), BorneGris, "Entrada variador")
            {
                Tamano = new Vector2(0.005f, 0.005f),
                Profundidad = 0.001f,
                EtiquetaArriba = true,
                SeparacionEtiqueta = 0.0108f
            });

            // El alto de la caja manda sobre el ancho: con 17 mm el titulo salia de 303 mm
            // cuando en el tablero mide 370, y se notaba porque es lo primero que se lee.
            // 366 mm de ancho y 18 de alto, medidos. Salia de 304 x 12.
            // El titulo del tablero mide 365 mm de tinta, de x = 67 a x = 432.
            //
            // No se puede clavar: el autoajuste de TMP salta entre tamanos discretos y aqui
            // no hay ninguno intermedio. Con una caja de 398 mm el texto sale de 303 y con
            // 424 salta a 404; 440 da tambien 404. Se deja en 404, un 11 % ancho, porque la
            // otra opcion se queda un 17 % corta y un titulo pequeno canta mas que uno
            // grande. Si algun dia hace falta exacto, habria que apagar el autoajuste y
            // calibrar el cuerpo a mano, como se hizo con las teclas del variador.
            e.Add(Etq("ETQ_TITULO", "MÓDULO VARIADOR DE FRECUENCIA",
                      249.5f, 19f, 0.4240f, 0.0300f, "Serigrafia"));

            // =============================================================================
            // Control de velocidad: pulsadores subir / bajar
            // =============================================================================
            // Los dos pulsadores van a los lados (x = 207 y 282) y sus bornes al centro, en
            // dos columnas verticales a x = 233 y 254. Los dos son verdes en el módulo real:
            // lo que los distingue es la flecha serigrafiada, no el color.
            AgregarPulsadorCentral(e, "SUBIR", "SUBIR", "▲", Verde, 207f, 90f, 233f,
                                   "Control de velocidad");
            AgregarPulsadorCentral(e, "BAJAR", "BAJAR", "▼", Verde, 282f, 90f, 254f,
                                   "Control de velocidad");

            e.Add(Etq("ETQ_CONTROL", "CONTROL DE VELOCIDAD", 245f, 112f, 0.1100f, 0.0065f,
                      "Control de velocidad"));

            // =============================================================================
            // Indicadores de velocidad V1..V4
            // =============================================================================
            // Van en CUADRADO 2×2, no en fila: V1 y V2 arriba (y = 137) y V3 y V4 abajo
            // (y = 188), en las mismas dos columnas que los pulsadores. Estaban puestos en
            // una fila de cuatro a 40 mm de paso, que no se parece a nada de lo que hay en el
            // tablero; es el error que más cambiaba el aspecto de la zona central.
            float[] vx = { 207f, 282f, 207f, 282f };
            float[] vy = { 137f, 137f, 188f, 188f };
            float[] vbx = { 233f, 254f, 233f, 254f };

            // Las dos filas NO tienen sus bornes a la misma distancia de la lampara: arriba
            // el rojo esta 10 mm por encima y abajo 15. Se usan las alturas medidas de cada
            // una en vez de un desplazamiento comun.
            float[] vyRojo = { 127f, 127f, 173f, 173f };
            float[] vyNegro = { 150f, 150f, 195f, 195f };
            float[] vyRotulo = { 138.5f, 138.5f, 181.3f, 181.3f };
            for (int i = 0; i < 4; i++)
            {
                string id = "V" + (i + 1);
                e.Add(new VdfPanelElement(id, id, VdfElementType.PilotoLed,
                    X(vx[i]), Y(vy[i]), Verde, "Indicadores de velocidad")
                { MostrarEtiqueta = false });

                // El par de bornes de cada indicador, en vertical sobre la columna central:
                // el rojo 10 mm por encima de la altura de la lámpara y el negro 13 por
                // debajo, que es como salen medidos.
                e.Add(new VdfPanelElement(id + "-A", id + " A", VdfElementType.Borne,
                    X(vbx[i]), Y(vyRojo[i]), BorneRojo, "Indicadores de velocidad")
                { MostrarEtiqueta = false });
                e.Add(new VdfPanelElement(id + "-B", id + " B", VdfElementType.Borne,
                    X(vbx[i]), Y(vyNegro[i]), BorneNegro, "Indicadores de velocidad")
                { MostrarEtiqueta = false });

                // El rótulo V1..V4 va junto a su columna de bornes, no bajo la lámpara.
                // Centrado en la columna y justo debajo del borne rojo, que es donde esta
                // en el modulo real. Puesto al lado se montaba con el embellecedor de la
                // lampara y ademas no se entendia a cual de los dos bornes nombraba.
                e.Add(Etq("ETQ_" + id, id, vbx[i], vyRotulo[i],
                          0.012f, 0.0060f, "Indicadores de velocidad"));
            }

            e.Add(Etq("ETQ_VELOC", "INDICADORES DE VELOCIDAD", 245f, 157f, 0.1250f, 0.0065f,
                      "Indicadores de velocidad"));
            e.Add(Etq("ETQ_220_VELOC", "220 V", 196f, 168f, 0.026f, 0.0058f, "Serigrafia"));

            e.Add(new VdfPanelElement("V1000", "V1000", VdfElementType.Variador,
                X(372f), Y(120f), Marfil, "Variador")
            {
                Tamano = new Vector2(0.0675f, 0.1275f),
                Profundidad = 0.052f,
                MostrarEtiqueta = false
            });

            // CUATRO bornes, no tres. Contando las manchas de la foto rectificada en el
            // sector del variador salen cuatro a la misma altura: 327, 354, 380 y 408. El
            // cuarto es la tierra del motor, que es lo que cabe esperar de una salida
            // trifasica con PE, y se habia pasado por alto porque el variador lo tapa a
            // medias.
            float[] xMotor = { 327f, 354f, 380f, 408f };
            string[] idMotor = { "U_MOT", "V_MOT", "W_MOT", "PE_MOT" };
            string[] etqMotor = { "U", "V", "W", "PE" };
            for (int i = 0; i < 4; i++)
            {
                e.Add(new VdfPanelElement(idMotor[i], etqMotor[i], VdfElementType.Borne,
                    X(xMotor[i]), Y(201f), i == 1 ? BorneRojo : BorneNegro, "Salida a motor")
                { MostrarEtiqueta = false });

                // Con 9 x 4,8 mm no llegaba a dibujarse ni un pixel de tinta.
                e.Add(Etq("ETQ_" + idMotor[i], etqMotor[i], xMotor[i], 190f,
                          0.013f, 0.0060f, "Salida a motor"));
            }

            // El titulo del grupo queda casi entero detras del variador, igual que en el
            // modulo real: de «SALIDA A MOTOR» solo asoman las dos primeras letras por la
            // izquierda. Lo que lleva la informacion son las tres bananas rotuladas.
            e.Add(Etq("ETQ_MOTOR", "SALIDA A MOTOR", 350f, 184f, 0.0500f, 0.0058f,
                      "Salida a motor"));

            AgregarMandoDerecha(e, "START", "START", VdfElementType.Pulsador, Verde, 442f, 66f, 0f, 63.4f);
            AgregarMandoDerecha(e, "STOP", "STOP", VdfElementType.Pulsador, Rojo, 442f, 125f, 0f, 119.7f);
            AgregarMandoDerecha(e, "PE", "PE", VdfElementType.PulsadorHongo, Rojo, 450f, 189f, 45f, 183.2f);

            e.Add(Etq("ETQ_24V_START", "24 V", 442f, 44f, 0.022f, 0.0058f, "Mando"));
            e.Add(Etq("ETQ_24V_STOP", "24 V", 442f, 103f, 0.022f, 0.0058f, "Mando"));

            // =============================================================================
            // Regleta inferior: dos filas de 15 bornes a 26,6 mm
            // =============================================================================
            e.Add(new VdfPanelElement("PLACA_REGLETA", "", VdfElementType.Bloque,
                X(246f), Y(246.5f), new Color(0.975f, 0.975f, 0.98f), "Regleta")
            { Tamano = new Vector2(0.392f, 0.077f), Profundidad = RegletaGrosor, MostrarEtiqueta = false });

            // La fila 1 empieza en rojo (R+) y la fila 2 en negro (S1): así alternan en el
            // módulo real, y es lo que permite leer de un vistazo cuál es cuál.
            AgregarFilaRegleta(e, RegletaFila1, RegletaX0Fila1, RegletaFila1Y,
                               "Regleta fila 1", "F1", true);
            AgregarFilaRegleta(e, RegletaFila2, RegletaX0Fila2, RegletaFila2Y,
                               "Regleta fila 2", "F2", false);

            e.Add(Etq("ETQ_ESTUDIANTES_T", "ESTUDIANTES", 190f, 282f, 0.050f, 0.0122f,
                      "Serigrafia", Rotulo));
            // La linea mide 318 mm en el tablero y salia de 118. Se queda en 291, un 8 %
            // corta: para los 318 completos haria falta una caja de 415 mm y, centrada
            // donde va el texto, se saldria de la lamina por la izquierda.
            e.Add(Etq("ETQ_ESTUDIANTES", "GABRIEL D. VEGA MENDEZ, DILAN A. LAMUS PABON, " +
                      "ANGEL D. RINCON BAUTISTA Y CRISTIAN A. PAZ LEON",
                      184f, 290f, 0.366f, 0.0088f, "Serigrafia", Rotulo));

            // Las tres lineas arrancan en el mismo margen izquierdo, x = 370, no van
            // centradas entre si: «DOCENTES» es corta y queda a la izquierda del bloque.
            e.Add(Etq("ETQ_DOCENTES_T", "DOCENTES", 386f, 282f, 0.036f, 0.0100f,
                      "Serigrafia", Rotulo));
            e.Add(Etq("ETQ_DOCENTE_1", "ING. LUIS E. NEIRA ROPERO", 410f, 287.5f,
                      0.090f, 0.0115f, "Serigrafia", Rotulo));
            // Ojo con el alto: a 295 mm del borde superior solo quedan 5 mm de lamina,
            // y el validador tumba cualquier caja que se salga.
            e.Add(Etq("ETQ_DOCENTE_2", "ING. JAIR E. ARAUJO VARGAS", 412f, 294.5f,
                      0.094f, 0.0108f, "Serigrafia", Rotulo));

            AgregarRecuadrosDeSeccion(e);

            return e;
        }

        // =====================================================================================
        // PLC Siemens S7-1200, externo al tablero
        // =====================================================================================

        /// <summary>
        /// CPU 1214C AC/DC/RLY, referencia 6ES7 214-1BE30-0XB0, leída en el esquema de la
        /// página 18 del manual del módulo: 110 × 100 × 75 mm de catálogo.
        /// </summary>
        public const float AnchoPlc = 0.110f;
        public const float AltoPlc = 0.100f;
        private const float PlcPlacaGrosor = 0.012f;

        /// <summary>
        /// Salidas del PLC agrupadas por su común, como en el 1214C AC/DC/RLY: las de relé van
        /// en dos grupos y el primero llega hasta Q0.4, no hasta Q0.3.
        ///
        /// La diferencia no es un detalle de dibujo: el grupo 1L está a 220 V para los pilotos
        /// y el 2L a 24 V para las entradas del variador, así que de qué lado caiga Q0.4
        /// decide si se puede usar para S1. Dibujada bajo 2L, el tablero contradecía al propio
        /// cableado correcto, que la deja fuera por estar a 220 V.
        /// </summary>
        public static readonly string[] PlcGrupo1L = { "1L", "Q0.0", "Q0.1", "Q0.2", "Q0.3", "Q0.4" };
        public static readonly string[] PlcGrupo2L = { "2L", "Q0.5", "Q0.6", "Q0.7" };

        public static readonly string[] PlcEntradas = { "1M", "I0.0", "I0.1", "I0.2", "I0.3", "I0.4" };

        /// <summary>Qué pulsador del tablero va a cada entrada.</summary>
        public static readonly string[] PlcEntradaDe = { "START", "STOP", "SUBIR", "BAJAR", "PE" };

        /// <summary>
        /// El PLC esta fuera del tablero, asi que es su propio layout. Se construye y se
        /// coloca por separado, pero comparte el mismo sistema de bornes e Ids, de modo
        /// que el grafo de cableado no distingue entre tablero y PLC: solo ve bornes.
        /// </summary>
        public static VdfPanelLayout CrearPlc()
        {
            var layout = ScriptableObject.CreateInstance<VdfPanelLayout>();
            layout.name = "VdfPlcLayout";
            layout.Ancho = AnchoPlc;
            layout.Alto = AltoPlc;
            layout.Grosor = 0.075f;
            layout.ColorTablero = new Color(0.86f, 0.86f, 0.84f);
            layout.Elementos = CrearElementosPlc();
            return layout;
        }

        public static List<VdfPanelElement> CrearElementosPlc()
        {
            var e = new List<VdfPanelElement>();

            e.Add(new VdfPanelElement("PLC_PLACA", "", VdfElementType.Bloque,
                AnchoPlc * 0.5f, 0.050f, new Color(0.70f, 0.71f, 0.70f), "PLC")
            { Tamano = new Vector2(0.104f, 0.082f), Profundidad = PlcPlacaGrosor, MostrarEtiqueta = false });

            e.Add(new VdfPanelElement("ETQ_PLC", "SIEMENS S7-1200  CPU 1214C", VdfElementType.Etiqueta,
                AnchoPlc * 0.5f, 0.093f, new Color(0.12f, 0.12f, 0.13f), "PLC")
            { Tamano = new Vector2(0.0900f, 0.0058f) });

            // Tres filas: las dos de salidas arriba y las entradas abajo, que es donde van
            // en el 1214C. Separadas 26 mm, el mismo paso que la regleta del tablero: en el
            // equipo real están mucho más juntas, pero con guantes de VR y sin visor para
            // probar, apretarlas es pedir que no se pueda enchufar nada (regla 5).
            AgregarFilaPlc(e, PlcGrupo1L, 0.074f);
            AgregarFilaPlc(e, PlcGrupo2L, 0.048f);
            AgregarFilaPlc(e, PlcEntradas, 0.022f);

            return e;
        }

        /// <summary>
        /// Reparte una fila de bornes a lo ancho de la placa del PLC.
        ///
        /// El paso se calcula, no se fija: con un paso constante de 20 mm, la fila de 1L
        /// —que tiene seis bornes, no cinco, porque el común agrupa hasta Q0.4— se salía de
        /// una placa que mide 110 mm. Lo cazo el test que comprueba que ningún borne cae
        /// fuera de su placa.
        /// </summary>
        private static void AgregarFilaPlc(List<VdfPanelElement> e, string[] etiquetas, float y)
        {
            const float margen = 0.010f;
            float util = AnchoPlc - margen * 2f;
            float paso = etiquetas.Length > 1 ? util / (etiquetas.Length - 1) : 0f;

            for (int i = 0; i < etiquetas.Length; i++)
            {
                float x = margen + i * paso;
                // Los comunes (1L, 2L, 1M) en rojo; las salidas y las entradas en negro.
                Color color = i == 0 ? BorneRojo : BorneNegro;

                e.Add(new VdfPanelElement("PLC-" + etiquetas[i], etiquetas[i], VdfElementType.Borne,
                    x, y, color, "PLC")
                { ZMontaje = -PlcPlacaGrosor });
            }
        }

        // ---------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Añade un recuadro alrededor de cada sección, midiendo lo que hay dentro.
        ///
        /// Puestos a mano se solapaban unos con otros y con los rótulos, y cada vez que algo
        /// se movía había que recolocarlos los ocho. Midéndolos, el recuadro siempre encierra
        /// justo a su grupo, y si mañana se mueve un piloto el recuadro se mueve con él.
        /// </summary>
        /// <summary>
        /// Los recuadros azules de la serigrafía, con las medidas que tienen en la foto.
        ///
        /// Antes se calculaban midiendo lo que había dentro de cada grupo, para que se
        /// movieran solos si se movía una pieza. Con las posiciones estimadas tenía sentido;
        /// con el tablero ya medido hace más mal que bien. Los grupos del centro se tocan
        /// en el módulo real, así que el cálculo daba tres cajas de anchos distintos y
        /// montadas unas sobre otras. En la foto, las dos de abajo comparten exactamente el
        /// mismo borde izquierdo y derecho y van apiladas con un milímetro de separación.
        /// </summary>
        private static void AgregarRecuadrosDeSeccion(List<VdfPanelElement> e)
        {
            // x0, y0, x1, y1 en milímetros medidos sobre la foto rectificada.
            // Medidos uno a uno sobre la foto rectificada, con una rejilla milimetrica
            // encima y a 2,6 aumentos. Los anteriores se habian leido de un realce de
            // contraste a tamano completo y estaban desplazados hasta 18 mm: el de entrada
            // empezaba once milimetros por encima de donde empieza, y los dos del centro
            // eran casi dos centimetros mas anchos de la cuenta.
            //
            // Detectarlos por software no funciono: las lineas son muy tenues y el detector
            // se enganchaba al texto serigrafiado y al embellecedor de las lamparas, que
            // tambien son oscuros y estan dentro de la ventana de busqueda.
            float[][] cajas =
            {
                // Los tres bornes de F1/F2 van a 119,6 / 145 / 171,3 y miden 14 mm, asi
                // que el de F1 llega hasta 112,6 y el de F2 hasta 178,3: el recuadro tiene
                // que abrirse a 112 y 182 para contenerlos. Con 116 la linea cruzaba el
                // borne de F1 por la mitad. Las dos medidas que se tomaron de ese borde,
                // 111,7 a rejilla y 115,8 en la revision, dejan sitio de sobra para el 112.
                new[] { 112f,  41f, 182f, 177f },   // magnetotermico y salida F1/F2
                // Lo mismo aqui: el borne de VCC esta en 122 y llega hasta 115.
                new[] { 112f, 179f, 168f, 210f },   // alimentacion de control 24 V
                new[] { 316f, 182f, 434f, 207f },   // salida a motor
                new[] { 200f,  42f, 292f,  67f },   // entrada del variador
                new[] { 188f,  68f, 303f, 114f },   // control de velocidad
                new[] { 188f, 115f, 303f, 202f },   // indicadores de velocidad
                new[] {  40f, 207f, 462f, 277f },   // regleta
                // La divisoria entre las dos filas. Va como un recuadro casi sin alto: sus
                // barras de arriba y de abajo caen una sobre otra y dan la linea, y las de
                // los lados miden medio milimetro y no se ven. Mas sencillo que anadir un
                // tipo de elemento nuevo para una sola raya.
                new[] {  40f, 242f, 462f, 242.5f }, // divisoria de la regleta
                // El simbolo de tierra bajo el tornillo: tres barras cada vez mas cortas.
                // Van como recuadros sin alto, igual que la divisoria.
                new[] { 168f, 193f, 182f, 193.4f }, // tierra, barra larga
                new[] { 170.5f, 196.5f, 179.5f, 196.9f }, // tierra, barra media
                new[] { 172.5f, 200f, 177.5f, 200.4f }, // tierra, barra corta
            };
            string[] ids = { "RC_Breaker", "RC_Veinticuatro", "RC_Motor", "RC_Entrada",
                             "RC_Control", "RC_Indicadores", "RC_Regleta", "RC_RegletaMedio",
                             "RC_Tierra1", "RC_Tierra2", "RC_Tierra3" };

            for (int i = 0; i < cajas.Length; i++)
            {
                var c = cajas[i];
                e.Add(new VdfPanelElement(ids[i], "", VdfElementType.Recuadro,
                    X((c[0] + c[2]) * 0.5f), Y((c[1] + c[3]) * 0.5f), Serigrafia, "Serigrafia")
                {
                    Tamano = new Vector2((c[2] - c[0]) * 0.001f, (c[3] - c[1]) * 0.001f),
                    Profundidad = 0.0006f,
                    MostrarEtiqueta = false
                });
            }
        }

        private static void AgregarPilotoConBornes(List<VdfPanelElement> e, string id,
                                                   string etiqueta, Color color,
                                                   float xMm, float yMm, string grupo,
                                                   float yRotuloMm = 0f)
        {
            // La cajita no va a la altura exacta de la lampara: en el tablero esta un poco
            // por encima, y cuanto mas abajo esta la pieza, mas. Se pasa medida.
            if (yRotuloMm <= 0f) yRotuloMm = yMm;
            e.Add(new VdfPanelElement(id, etiqueta, VdfElementType.PilotoLed,
                X(xMm), Y(yMm), color, grupo) { MostrarEtiqueta = false });

            // La cajita con el nombre, 39 mm a la izquierda y a la misma altura. Es el único
            // recuadro pequeño del tablero y se nota su falta: sin él el nombre del piloto
            // queda flotando sobre la placa.
            e.Add(new VdfPanelElement("RC_" + id, "", VdfElementType.Recuadro,
                X(xMm - 38f), Y(yRotuloMm), Serigrafia, "Serigrafia")
            {
                Tamano = new Vector2(0.030f, 0.012f),
                Profundidad = 0.0006f,
                MostrarEtiqueta = false
            });
            e.Add(Etq("ETQ_" + id, etiqueta, xMm - 38f, yRotuloMm, 0.026f, 0.0070f, grupo));

            // El par de bornes, en vertical 31 mm a la derecha: rojo arriba, negro abajo.
            e.Add(new VdfPanelElement(id + "-A", etiqueta + " A", VdfElementType.Borne,
                X(xMm + 31f), Y(yMm - 11f), BorneRojo, grupo) { MostrarEtiqueta = false });
            e.Add(new VdfPanelElement(id + "-B", etiqueta + " B", VdfElementType.Borne,
                X(xMm + 31f), Y(yMm + 11f), BorneNegro, grupo) { MostrarEtiqueta = false });
        }

        /// <summary>
        /// Un pulsador de la zona central. Sus bornes no van debajo sino en la columna del
        /// medio que comparten los dos pulsadores, con la flecha serigrafiada entre ellos.
        /// </summary>
        private static void AgregarPulsadorCentral(List<VdfPanelElement> e, string id,
                                                   string etiqueta, string flecha, Color color,
                                                   float xMm, float yMm, float xBornesMm,
                                                   string grupo)
        {
            e.Add(new VdfPanelElement(id, etiqueta, VdfElementType.Pulsador,
                X(xMm), Y(yMm), color, grupo) { MostrarEtiqueta = false });

            e.Add(new VdfPanelElement(id + "-A", etiqueta + " A", VdfElementType.Borne,
                X(xBornesMm), Y(yMm - 11f), BorneRojo, grupo) { MostrarEtiqueta = false });
            e.Add(new VdfPanelElement(id + "-B", etiqueta + " B", VdfElementType.Borne,
                X(xBornesMm), Y(yMm + 11f), BorneNegro, grupo) { MostrarEtiqueta = false });

            // En el tablero la flecha llena el hueco entre el borne rojo y el negro, unos
            // 12 mm. Con 9 era una mota y no se distinguia subir de bajar.
            e.Add(Etq("ETQ_" + id, flecha, xBornesMm, yMm, 0.011f, 0.0130f, grupo));
        }

        private static void AgregarMandoDerecha(List<VdfPanelElement> e, string id,
                                                string etiqueta, VdfElementType tipo,
                                                Color color, float xMm, float yMm,
                                                float anchoMm = 0f, float yRotuloMm = 0f)
        {
            if (yRotuloMm <= 0f) yRotuloMm = yMm;
            var mando = new VdfPanelElement(id, etiqueta, tipo, X(xMm), Y(yMm), color, "Mando")
            { MostrarEtiqueta = false };
            if (anchoMm > 0f) mando.Tamano = new Vector2(anchoMm * 0.001f, anchoMm * 0.001f);
            e.Add(mando);

            float xRotulo = 477.5f + (tipo == VdfElementType.PulsadorHongo ? 3.5f : 0f);

            e.Add(new VdfPanelElement("RC_" + id, "", VdfElementType.Recuadro,
                X(xRotulo), Y(yRotuloMm), Serigrafia, "Serigrafia")
            {
                Tamano = new Vector2(0.030f, 0.012f),
                Profundidad = 0.0006f,
                MostrarEtiqueta = false
            });
            e.Add(Etq("ETQ_" + id, etiqueta, xRotulo, yRotuloMm, 0.026f, 0.0070f, "Mando"));
        }

        /// <summary>
        /// Coloca una fila de la regleta. Ojo: la fila 1 repite la etiqueta "AC" dos veces,
        /// así que el Id lleva sufijo de fila e índice para seguir siendo único; el texto
        /// serigrafiado sí se repite, igual que en el equipo real.
        /// </summary>
        private static void AgregarFilaRegleta(List<VdfPanelElement> e, string[] etiquetas,
                                               float x0, float y, string grupo,
                                               string prefijoFila, bool empiezaEnRojo)
        {
            var usados = new Dictionary<string, int>();

            for (int i = 0; i < etiquetas.Length; i++)
            {
                string etiqueta = etiquetas[i];
                bool conNombre = !string.IsNullOrEmpty(etiqueta);

                string id;
                if (!conNombre)
                {
                    // El hueco sin rotular de la fila 2. Tiene banana, así que es un borne
                    // más y necesita Id propio.
                    id = prefijoFila + "-" + (i + 1);
                }
                else
                {
                    int veces;
                    usados.TryGetValue(etiqueta, out veces);
                    usados[etiqueta] = veces + 1;

                    id = etiqueta;
                    if (veces > 0 || EsEtiquetaAmbigua(etiqueta))
                        id = etiqueta + "_" + prefijoFila + (i + 1);
                }

                float x = x0 + i * RegletaPaso;

                // Rojo y negro alternados, como en el módulo real: la fila 1 arranca en rojo
                // (R+) y la 2 en negro (S1). Eso es lo que le dice al alumno de un vistazo
                // cuál es cuál.
                bool rojo = (i % 2 == 0) == empiezaEnRojo;

                e.Add(new VdfPanelElement(id, etiqueta, VdfElementType.Borne,
                    x, y, rojo ? BorneRojo : BorneNegro, grupo)
                {
                    // Montado sobre la plancha de la regleta, no sobre la placa desnuda.
                    ZMontaje = -RegletaGrosor,
                    MostrarEtiqueta = conNombre,
                    // El nombre va serigrafiado ENCIMA de la banana, a 20 mm medidos: la
                    // fila 1 tiene las bananas a 234 y los nombres a 214.
                    EtiquetaArriba = true,
                    SeparacionEtiqueta = 0.020f
                });
            }
        }

        /// <summary>
        /// Etiquetas que aparecen en más de un sitio del tablero y por tanto no sirven como
        /// Id por sí solas: "AC" sale dos veces en la fila 1 de la regleta.
        ///
        /// U, V y W estaban aquí porque el manual del módulo los lista en la regleta y a la
        /// vez en la salida a motor, y no se sabía si eran los mismos bornes o dos juegos
        /// distintos. La foto lo resuelve: son los mismos, y están bajo el variador.
        /// </summary>
        private static bool EsEtiquetaAmbigua(string etiqueta)
        {
            return etiqueta == "AC";
        }
    }
}
