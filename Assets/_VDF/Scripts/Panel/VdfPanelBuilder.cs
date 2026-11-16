using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Construye el tablero en la escena a partir de un <see cref="VdfPanelLayout"/>.
    ///
    /// Convenio de ejes: la placa está en el plano XY con su esquina inferior izquierda
    /// en el origen local. La cara frontal está en z = 0 y el cuerpo de la placa va hacia
    /// z POSITIVO. Los componentes sobresalen hacia -Z, que es por donde mira el usuario.
    ///
    /// El sentido importa y es fácil equivocarse: una cámara identidad mira hacia +Z y
    /// tiene su "derecha" en +X. Solo desde ahí el texto de TextMeshPro se lee del
    /// derecho y la X del panel crece hacia la derecha del usuario. Si se construye al
    /// revés, el tablero sale en espejo: layout correcto, rótulos ilegibles.
    /// </summary>
    public static class VdfPanelBuilder
    {
        public const string NombreRaiz = "VDF_Tablero";
        public const string NombreMotor = "VDF_Motor";

        /// <summary>
        /// Las diez lineas del bloque tecnico de la placa del variador.
        ///
        /// Se arma con un char y no con el escape de salto de linea dentro de un
        /// literal, porque generar este
        /// archivo desde un script acaba metiendo saltos de linea de verdad dentro de la
        /// cadena y el codigo deja de compilar. Ya paso con el grabador de video.
        /// </summary>
        private static string TextoTecnico()
        {
            char nl = (char)10;
            return "Freq Reference" + nl + "FWD/REV Sel" + nl + "Output Freq" + nl
                 + "Output Current" + nl + "Selected Monitor" + nl + "Monitor" + nl
                 + "Verify" + nl + "Set Up Guide" + nl + "Parameter Set" + nl
                 + "Auto - Tuning";
        }

        /// <summary>Componente marcador para reencontrar los bornes desde otros sistemas (E4, E6).</summary>
        public static GameObject Construir(VdfPanelLayout layout, Transform padre = null)
        {
            if (layout == null)
            {
                Debug.LogError("[VDF] VdfPanelBuilder.Construir: el layout es null.");
                return null;
            }

            var raiz = new GameObject(NombreRaiz);
            if (padre != null) raiz.transform.SetParent(padre, false);

            ConstruirPlaca(layout, raiz.transform);

            var contenedor = new GameObject("Elementos");
            contenedor.transform.SetParent(raiz.transform, false);

            for (int i = 0; i < layout.Elementos.Count; i++)
                ConstruirElemento(layout, layout.Elementos[i], contenedor.transform);

            return raiz;
        }

        // =====================================================================================
        // Placa
        // =====================================================================================

        private static void ConstruirPlaca(VdfPanelLayout layout, Transform padre)
        {
            var placa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placa.name = "Placa";
            placa.transform.SetParent(padre, false);
            placa.transform.localScale = new Vector3(layout.Ancho, layout.Alto, layout.Grosor);
            placa.transform.localPosition = new Vector3(layout.Ancho * 0.5f, layout.Alto * 0.5f, layout.Grosor * 0.5f);
            Pintar(placa, layout.ColorTablero, 0.1f);
        }

        // =====================================================================================
        // Elementos
        // =====================================================================================

        private static void ConstruirElemento(VdfPanelLayout layout, VdfPanelElement el, Transform padre)
        {
            if (el == null) return;

            var nodo = new GameObject(el.Tipo + "_" + el.Id);
            nodo.transform.SetParent(padre, false);
            nodo.transform.localPosition = new Vector3(el.Posicion.x, el.Posicion.y, el.ZMontaje);

            switch (el.Tipo)
            {
                case VdfElementType.Borne:
                    ConstruirBorne(el, nodo.transform);
                    break;

                case VdfElementType.PilotoLed:
                    // Suavidad baja: con 0,35 el reflejo blanco cubria un tercio de la lente y
                    // el piloto se leia como una canica de cristal. En la foto es mate.
                    ConstruirCilindro(el, nodo.transform, "Piloto", el.Color, 0.16f, true);
                    break;

                case VdfElementType.Pulsador:
                    ConstruirCilindro(el, nodo.transform, "Pulsador", el.Color, 0.15f, false);
                    break;

                case VdfElementType.PulsadorHongo:
                    ConstruirHongo(el, nodo.transform);
                    break;

                case VdfElementType.Variador:
                    ConstruirVariador(el, nodo.transform);
                    break;

                case VdfElementType.Bloque:
                    ConstruirBloque(el, nodo.transform);
                    break;

                case VdfElementType.Etiqueta:
                    // Solo texto: lo pone el bloque de abajo.
                    break;

                case VdfElementType.Recuadro:
                    ConstruirRecuadro(el, nodo.transform);
                    break;
            }

            if (el.MostrarEtiqueta && !string.IsNullOrEmpty(el.Etiqueta))
                ConstruirEtiqueta(el, nodo.transform);

            // Marcador para que E4 (jumpers) y E6 (sockets XR) encuentren los bornes.
            if (el.EsConectable)
                MarcarComoBorne(nodo, el.Id, el.Etiqueta, el.Grupo, el.Profundidad);

            MarcarComoPulsador(el, nodo);
        }

        /// <summary>
        /// Si el elemento es uno de los pulsadores de mando del tablero, lo deja accionable.
        ///
        /// Los pulsadores estaban modelados desde el principio pero eran decorado: solo
        /// respondian al teclado. El alumno los ve, los intenta pulsar y no pasa nada.
        /// </summary>
        private static void MarcarComoPulsador(VdfPanelElement el, GameObject nodo)
        {
            if (el.Tipo != VdfElementType.Pulsador && el.Tipo != VdfElementType.PulsadorHongo) return;

            VdfAccionPulsador accion;
            switch (el.Id)
            {
                case "START": accion = VdfAccionPulsador.Start; break;
                case "STOP": accion = VdfAccionPulsador.Stop; break;
                case "SUBIR": accion = VdfAccionPulsador.Subir; break;
                case "BAJAR": accion = VdfAccionPulsador.Bajar; break;
                case "PE": accion = VdfAccionPulsador.Emergencia; break;
                default: return;          // otros pulsadores no mandan nada
            }

            var mando = nodo.AddComponent<VdfPulsadorTablero>();
            mando.Accion = accion;

            var blanco = nodo.AddComponent<SphereCollider>();
            blanco.isTrigger = true;
            blanco.radius = Mathf.Max(el.Tamano.x, el.Tamano.y) * 0.62f;
            blanco.center = new Vector3(0f, 0f, -el.Profundidad * 0.5f);
        }

        /// <summary>
        /// Convierte un nodo en bornera conectable: le pone el marcador y un blanco de
        /// clic esferico mas grande que el propio conector.
        ///
        /// Sin esto, acertar a un borne de 22 mm con el raton (o con un rayo de mando en
        /// VR) es incomodo: el collider del cilindro es justo del tamano de la pieza.
        /// El trigger de 36 mm hace que apuntar sea comodo sin que los bornes vecinos,
        /// separados 50-62 mm, se pisen.
        /// </summary>
        public static VdfTerminal MarcarComoBorne(GameObject nodo, string id, string etiqueta,
                                                  string grupo, float profundidad)
        {
            var marcador = nodo.AddComponent<VdfTerminal>();
            marcador.Id = id;
            marcador.Etiqueta = etiqueta;
            marcador.Grupo = grupo;
            marcador.PuntoConexionLocal = new Vector3(0f, 0f, -profundidad);

            var blanco = nodo.AddComponent<SphereCollider>();
            blanco.isTrigger = true;
            // 11 mm, no 18: con los bornes a 25 mm de paso un blanco de 18 tapa por
            // completo al vecino y el clic se lo queda el que no es. El reparto fino cuando
            // dos se tocan lo hace VdfJumperController.ElegirBorne.
            blanco.radius = 0.011f;
            blanco.center = new Vector3(0f, 0f, -profundidad * 0.5f);

            return marcador;
        }

        private static void ConstruirBorne(VdfPanelElement el, Transform padre)
        {
            // Cuerpo exterior del conector banana.
            var cuerpo = ConstruirCilindro(el, padre, "Cuerpo", el.Color, 0.14f, false);

            // Hueco oscuro en el centro, para que se lea como una bornera hembra.
            var hueco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hueco.name = "Hueco";
            hueco.transform.SetParent(padre, false);
            float d = el.Tamano.x * 0.45f;
            hueco.transform.localScale = new Vector3(d, el.Profundidad * 0.55f, d);
            hueco.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hueco.transform.localPosition = new Vector3(0f, 0f, -el.Profundidad * 0.72f);
            Pintar(hueco, new Color(0.05f, 0.05f, 0.06f), 0.0f);
            Object.DestroyImmediate(hueco.GetComponent<Collider>());

            if (cuerpo != null)
            {
                var col = cuerpo.GetComponent<Collider>();
                if (col != null) col.isTrigger = false;
            }
        }

        /// <summary>
        /// Un piloto o un pulsador: collar metálico, cuerpo y cabeza abombada.
        ///
        /// Antes era un cilindro con la cara plana, y eso es lo que hacía que el tablero se
        /// viera «de bloques»: en el módulo real ni los pilotos ni los pulsadores tienen la
        /// cara plana. El piloto es una lente que sobresale en cúpula y el pulsador una cabeza
        /// redondeada, los dos metidos en un aro metálico que los separa de la placa. Esa
        /// diferencia no se arregla con texturas: es geometría.
        /// </summary>
        private static GameObject ConstruirCilindro(VdfPanelElement el, Transform padre, string nombre,
                                                    Color color, float suavidad, bool emisivo)
        {
            float diametro = el.Tamano.x;
            float salida = el.Profundidad;

            // El collar: un aro metálico algo mayor que la cabeza, pegado a la placa.
            var collar = VdfMallas.Pieza(padre, "Collar", VdfMallas.Anillo(0.80f),
                new Vector3(0f, 0f, -0.0006f),
                new Vector3(diametro * 1.14f, diametro * 1.14f, 1f));
            Pintar(collar, new Color(0.46f, 0.47f, 0.49f), 0.55f);

            // El faldón, que da el grosor entre la placa y la cabeza.
            var faldon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            faldon.name = "Faldon";

            var colliderFaldon = faldon.GetComponent<Collider>();
            if (colliderFaldon != null)
            {
                if (Application.isPlaying) Object.Destroy(colliderFaldon);
                else Object.DestroyImmediate(colliderFaldon);
            }

            faldon.transform.SetParent(padre, false);
            faldon.transform.localScale = new Vector3(diametro, salida * 0.34f, diametro);
            faldon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            faldon.transform.localPosition = new Vector3(0f, 0f, -salida * 0.34f);
            Pintar(faldon, color, suavidad, emisivo);

            // Y la cabeza abombada, que es la pieza que se ve.
            //
            // Un piloto abomba más que un pulsador: es una lente, y por eso reparte la luz en
            // vez de devolver un disco plano de color.
            // La lente de un piloto abomba, pero poco: en la foto es casi un disco. Con
            // 0,42 salia una media esfera que, con el brillo especular encima, se leia como
            // una canica de colores y era la diferencia mas gorda al superponer la captura
            // sobre la foto.
            float curvatura = emisivo ? 0.20f : 0.16f;
            var cabeza = VdfMallas.Pieza(padre, nombre, VdfMallas.Domo(curvatura),
                new Vector3(0f, 0f, -salida * 0.68f),
                new Vector3(diametro, diametro, salida * 0.72f));
            Pintar(cabeza, color, suavidad, emisivo);
            return cabeza;
        }

        private static void ConstruirHongo(VdfPanelElement el, Transform padre)
        {
            // Base amarilla.
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseGo.name = "Base";
            baseGo.transform.SetParent(padre, false);
            float db = el.Tamano.x * 1.00f;
            baseGo.transform.localScale = new Vector3(db, el.Profundidad * 0.22f, db);
            baseGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            baseGo.transform.localPosition = new Vector3(0f, 0f, -el.Profundidad * 0.22f);
            Pintar(baseGo, new Color(0.93f, 0.85f, 0.10f), 0.2f);

            var cuello = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuello.name = "Cuello";
            cuello.transform.SetParent(padre, false);
            cuello.transform.localScale =
                new Vector3(el.Tamano.x * 0.70f, el.Profundidad * 0.16f, el.Tamano.x * 0.70f);
            cuello.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cuello.transform.localPosition = new Vector3(0f, 0f, -el.Profundidad * 0.34f);
            Pintar(cuello, new Color(0.42f, 0.43f, 0.45f), 0.5f);

            var seta = VdfMallas.Pieza(padre, "Seta", VdfMallas.Domo(0.32f),
                new Vector3(0f, 0f, -el.Profundidad * 0.48f),
                new Vector3(el.Tamano.x, el.Tamano.x, el.Profundidad * 0.62f));
            Pintar(seta, el.Color, 0.25f);
        }

        /// <summary>
        /// Rectángulo de línea fina, dibujado con cuatro barras pegadas a la placa.
        ///
        /// Se hace con geometría y no con una textura porque el tablero se construye por
        /// código a partir de medidas: así un recuadro sigue cuadrando si la sección que
        /// agrupa cambia de tamaño, que es justo lo que ha pasado dos veces ya.
        /// </summary>
        private static void ConstruirRecuadro(VdfPanelElement el, Transform padre)
        {
            float grosor = Mathf.Max(0.0012f, el.Profundidad);
            float w = el.Tamano.x, h = el.Tamano.y;

            // Esquinas redondeadas: en el modulo real no hay ni un recuadro en pico, y con
            // cuatro barras rectas la serigrafia se veia de otra epoca. El radio se acota al
            // lado corto para que las lineas finas (la divisoria de la regleta, el simbolo
            // de tierra) sigan saliendo rectas.
            float radio = Mathf.Min(0.007f, Mathf.Min(w, h) * 0.45f);

            var marco = VdfMallas.Pieza(padre, "Marco",
                VdfMallas.MarcoRedondeado(w, h, radio, grosor),
                new Vector3(0f, 0f, -0.0006f), Vector3.one);
            Pintar(marco, el.Color, 0.1f);
        }

        private static void CrearBarra(Transform padre, string nombre, Vector3 pos,
                                       Vector3 escala, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.localScale = escala;
            go.transform.localPosition = new Vector3(pos.x, pos.y, -0.0006f);

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            }
            Pintar(go, color, 0.1f);
        }

        private static void ConstruirBloque(VdfPanelElement el, Transform padre)
        {
            // El magnetotérmico no es un bloque: se construye entero aparte, porque su forma
            // es justo lo que lo hace reconocible.
            if (el.Id == "BREAKER") { ConstruirPalancasDelBreaker(el, padre); return; }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cuerpo";
            go.transform.SetParent(padre, false);
            go.transform.localScale = new Vector3(el.Tamano.x, el.Tamano.y, el.Profundidad);
            go.transform.localPosition = new Vector3(0f, 0f, -el.Profundidad * 0.5f);
            Pintar(go, el.Color, 0.2f);
        }

        /// <summary>
        /// El magnetotérmico de tres polos.
        ///
        /// Las cuatro versiones anteriores salieron de deducir la forma a partir de la foto
        /// del módulo, donde el aparato se ve de lado y medio tapado por los cables. Buscando
        /// una foto frontal de uno igual (Wikimedia Commons, ver FUENTES.md en
        /// Docs/Referencia/fotos) quedó claro lo que no se podía adivinar: **la silueta tiene
        /// forma de H**.
        ///
        ///   - Arriba y abajo, dos bloques de bornas que SOBRESALEN, cada uno partido en tres
        ///     por las muescas que separan los polos. Ahí van los tornillos.
        ///   - En medio, el cuerpo, más hundido y continuo, con la etiqueta y la ventana de
        ///     las palancas.
        ///   - Las palancas van unidas por una barra que cruza el aparato de lado a lado, no
        ///     por una barrita entre ellas.
        ///
        /// Un bloque liso con cosas pegadas encima nunca iba a parecerse, por mucho detalle
        /// que se le añadiera: lo que lo identifica es el escalonado de la silueta.
        /// </summary>
        private static void ConstruirPalancasDelBreaker(VdfPanelElement el, Transform padre)
        {
            const int polos = 3;
            float ancho = el.Tamano.x;
            float alto = el.Tamano.y;
            float fondo = el.Profundidad;
            float paso = ancho / polos;

            var canto = VdfMallas.CajaBiselada(0.07f);
            // Casi del mismo claro que la lamina, como en la foto: lo que da la forma no es
            // el color sino las sombras de los rebajes. Bajarlo a 0,84 lo hacia parecer una
            // pieza oscura pegada encima.
            Color gris = new Color(0.92f, 0.91f, 0.85f);
            Color grisSombra = new Color(gris.r * 0.86f, gris.g * 0.86f, gris.b * 0.85f);

            // --- El cuerpo central, que es el que queda atrás -----------------------------
            var centro = VdfMallas.Pieza(padre, "Cuerpo", canto,
                new Vector3(0f, 0f, -fondo * 0.38f),
                new Vector3(ancho, alto * 0.62f, fondo * 0.76f));
            Pintar(centro, gris, 0.18f);

            // --- Los dos bloques de bornas, que sobresalen --------------------------------
            // Cada uno partido en tres por las muescas entre polos: es lo que da la forma de
            // H y lo que hace que se lea como tres módulos pegados.
            // Los bloques solapan con el cuerpo: en el aparato real son la misma pieza, y
            // dejando hueco entre ellos se veían como seis cajas sueltas flotando.
            // Medido sobre el recorte de la foto: cada franja de bornas ocupa un 24 % del
            // alto, no un 30. El 6 % que sobraba se lo quitaba al cuerpo, que es donde van
            // la etiqueta y las palancas, y por eso no cabia nada en el centro.
            float altoBloque = alto * 0.24f;
            // Pegados: con 0,965 quedaba medio milimetro de aire entre modulo y modulo y a
            // un metro de distancia el aparato se leia como seis cajas sueltas flotando
            // sobre la placa en vez de como un bloque con muescas.
            float anchoModulo = paso * 1.0f;

            for (int lado = -1; lado <= 1; lado += 2)
            {
                float y = (alto * 0.5f - altoBloque * 0.50f) * lado;

                for (int i = 0; i < polos; i++)
                {
                    float x = -ancho * 0.5f + paso * (i + 0.5f);

                    var modulo = VdfMallas.Pieza(padre,
                        "Bloque_" + (i + 1) + (lado < 0 ? "_inf" : "_sup"), canto,
                        new Vector3(x, y, -fondo * 0.50f),
                        new Vector3(anchoModulo, altoBloque, fondo));
                    Pintar(modulo, gris, 0.18f);

                    // El tornillo, hundido en su pozo cuadrado.
                    var pozo = VdfMallas.Pieza(padre,
                        "Pozo_" + (i + 1) + (lado < 0 ? "_inf" : "_sup"), canto,
                        new Vector3(x, y, -fondo * 0.97f),
                        new Vector3(anchoModulo * 0.62f, altoBloque * 0.52f, fondo * 0.08f));
                    // El pozo bien oscuro: es la sombra lo que hace que se lea el rebaje.
                    Pintar(pozo, new Color(0.46f, 0.45f, 0.42f), 0.15f);

                    var tornillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tornillo.name = "Tornillo_" + (i + 1) + (lado < 0 ? "_inf" : "_sup");
                    tornillo.transform.SetParent(padre, false);
                    float radio = anchoModulo * 0.44f;
                    tornillo.transform.localScale = new Vector3(radio, fondo * 0.05f, radio);
                    tornillo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    tornillo.transform.localPosition = new Vector3(x, y, -fondo * 1.00f);
                    Pintar(tornillo, new Color(0.74f, 0.75f, 0.76f), 0.7f);

                    // La cruz de la cabeza. Sin ella el tornillo es un círculo gris y el ojo
                    // no lo lee como tornillo, que es de lo poco que se distingue a un metro.
                    for (int giro = 0; giro < 2; giro++)
                    {
                        var ranuraTornillo = VdfMallas.Pieza(padre,
                            "Ranura_" + (i + 1) + (lado < 0 ? "_inf" : "_sup") + "_" + giro, canto,
                            new Vector3(x, y, -fondo * 1.02f),
                            giro == 0
                                ? new Vector3(radio * 1.7f, radio * 0.26f, fondo * 0.02f)
                                : new Vector3(radio * 0.26f, radio * 1.7f, fondo * 0.02f));
                        Pintar(ranuraTornillo, new Color(0.42f, 0.43f, 0.44f), 0.5f);
                    }
                }
            }

            // --- Las juntas verticales entre polos, en el cuerpo central ------------------
            for (int i = 1; i < polos; i++)
            {
                var junta = VdfMallas.Pieza(padre, "Junta_" + i, canto,
                    new Vector3(-ancho * 0.5f + paso * i, 0f, -fondo * 0.77f),
                    new Vector3(0.0008f, alto * 0.50f, fondo * 0.02f));
                Pintar(junta, grisSombra, 0.1f);
            }

            // --- La banda de la etiqueta, con las tres ventanas de senalizacion ----------
            // Es lo que ocupa la mitad de arriba del cuerpo en el aparato real: una franja
            // clara con la marca y el calibre impresos y, debajo, tres ventanitas rojas, una
            // por polo, que es lo que indica que el polo esta cerrado.
            float yEtiqueta = alto * 0.125f;

            var banda = VdfMallas.Pieza(padre, "BandaEtiqueta", canto,
                new Vector3(0f, yEtiqueta, -fondo * 0.78f),
                new Vector3(ancho * 0.99f, alto * 0.27f, fondo * 0.03f));
            Pintar(banda, new Color(0.89f, 0.88f, 0.84f), 0.12f);

            CrearTexto("Etiqueta_Marca", padre, "MERNÖK",
                new Vector3(-ancho * 0.26f, yEtiqueta + alto * 0.075f, -fondo * 0.80f),
                new Vector2(ancho * 0.44f, 0.0060f), new Color(0.62f, 0.22f, 0.09f));

            CrearTexto("Etiqueta_Breaker", padre, "KBS  C32",
                new Vector3(-ancho * 0.30f, yEtiqueta - alto * 0.015f, -fondo * 0.80f),
                new Vector2(ancho * 0.36f, 0.0052f), new Color(0.16f, 0.16f, 0.17f));

            for (int i = 0; i < polos; i++)
            {
                float x = -ancho * 0.5f + paso * (i + 0.5f);
                var ventanita = VdfMallas.Pieza(padre, "Ventana_" + (i + 1), canto,
                    new Vector3(x, yEtiqueta - alto * 0.088f, -fondo * 0.80f),
                    new Vector3(paso * 0.34f, alto * 0.022f, fondo * 0.02f));
                Pintar(ventanita, new Color(0.62f, 0.11f, 0.09f), 0.35f);
            }

            // --- El canal de las palancas ------------------------------------------------
            // Las palancas iban pintadas de 0,90 sobre un cuerpo de 0,93: invisibles. En el
            // aparato real son de un gris bastante mas oscuro que la carcasa y es lo primero
            // que se ve, porque ademas sobresalen.
            float altoPalanca = alto * 0.20f;
            float yPalanca = -alto * 0.135f;

            var canal = VdfMallas.Pieza(padre, "CanalPalancas", canto,
                new Vector3(0f, yPalanca, -fondo * 0.74f),
                new Vector3(ancho * 0.92f, altoPalanca * 1.22f, fondo * 0.06f));
            Pintar(canal, new Color(0.30f, 0.30f, 0.30f), 0.10f);

            for (int i = 0; i < polos; i++)
            {
                float x = -ancho * 0.5f + paso * (i + 0.5f);
                var dedo = VdfMallas.Pieza(padre, "Palanca_" + (i + 1), canto,
                    new Vector3(x, yPalanca, -fondo * 0.86f),
                    new Vector3(paso * 0.74f, altoPalanca, fondo * 0.24f));
                Pintar(dedo, new Color(0.52f, 0.52f, 0.52f), 0.30f);
            }

            // La barra que une los tres polos cruza el aparato entero de lado a lado: es lo
            // que hace que abran y cierren a la vez, y se ve de un vistazo.
            var barra = VdfMallas.Pieza(padre, "BarraPalancas", canto,
                new Vector3(0f, yPalanca - altoPalanca * 0.62f, -fondo * 0.84f),
                new Vector3(ancho * 1.01f, alto * 0.030f, fondo * 0.18f));
            Pintar(barra, new Color(0.46f, 0.46f, 0.46f), 0.28f);
        }

        /// <summary>
        /// La carcasa del V1000 con el operador digital.
        ///
        /// Rehecho con la foto del variador del módulo delante
        /// (Docs/Referencia/fotos/v1000_detalle.png), porque lo que había no se parecía: la
        /// carcasa era gris en vez de beige, el operador no se distinguía del cuerpo y el
        /// aviso de seguridad era una banda amarilla enorme cuando en el equipo real es un
        /// bloque de texto pequeño.
        ///
        /// Lo que identifica a un V1000 de un vistazo, por orden: el **beige** de la carcasa,
        /// la placa clara del operador en la mitad de arriba, y debajo la etiqueta con
        /// **YASKAWA** y **V1000** en grande.
        /// </summary>
        private static void ConstruirVariador(VdfPanelElement el, Transform padre)
        {
            float ancho = el.Tamano.x;
            float alto = el.Tamano.y;
            float fondo = el.Profundidad;

            var canto = VdfMallas.CajaBiselada(0.05f);
            // Amarillo ocre. Medido sobre fotos_entrega1/v1000_frontal.jpg, que es la
            // foto frontal bien iluminada, corrigiendo el balance de blancos contra la
            // lamina del tablero: la franja de chasis da (0,82 0,74 0,46).
            //
            // OJO al volver a medir esto: la otra foto del variador,
            // fotos/v1000_detalle.png, esta tomada en sombra y da (0,66 0,61 0,44). Si se
            // normaliza contra su propia lamina, que tambien esta en sombra, la correccion
            // se pasa y sale un ocre apagado que no se parece al equipo. Vale la foto
            // frontal, no la de detalle.
            Color beige = new Color(0.82f, 0.74f, 0.46f);

            float altoBajo = alto * (107.6f / 127.5f);
            float anchoAlto = ancho * (57.6f / 67.5f);

            var cuerpo = VdfMallas.Pieza(padre, "Cuerpo", canto,
                new Vector3(0f, -(alto - altoBajo) * 0.5f, -fondo * 0.5f),
                new Vector3(ancho, altoBajo, fondo));

            // El hombro se mete 14 mm dentro del cuerpo.
            //
            // Apoyado justo encima, la union dejaba un surco. El canto matado de VdfMallas
            // va en fraccion del lado, asi que en una caja de 107 mm de alto el redondeo del
            // borde superior mide 5,4: con 4 mm de solape seguia asomando. Metiendolo 14 el
            // borde redondeado del cuerpo queda dentro del hombro y no se ve.
            float altoHombro = (alto - altoBajo) + 0.014f;
            var hombro = VdfMallas.Pieza(padre, "Hombro", canto,
                new Vector3(-(ancho - anchoAlto) * 0.5f, alto * 0.5f - altoHombro * 0.5f,
                            -fondo * 0.5f),
                new Vector3(anchoAlto, altoHombro, fondo));
            Pintar(hombro, beige, 0.22f);
            Pintar(cuerpo, beige, 0.22f);

            // --- El operador digital: una placa clara, encastrada en la mitad de arriba ----
            // La placa del operador ocupa casi TODO el ancho del variador y va centrada.
            // Con 0,72 y corrida 8 % a la izquierda, por la derecha asomaba una franja
            // amarilla del tamano de la propia placa y el equipo no se parecia: en el V1000
            // real solo se ve un filete de carcasa a cada lado.
            float anchoOperador = ancho * 0.85f;
            float altoOperador = alto * 0.34f;
            float yOperador = alto * 0.29f;

            var operador = VdfMallas.Pieza(padre, "FrontalOperador", canto,
                new Vector3(-ancho * 0.075f, yOperador, -fondo * 1.01f),
                new Vector3(anchoOperador, altoOperador, fondo * 0.05f));
            // Gris medio, (0,53 0,53 0,51) medido entre las teclas en la foto frontal.
            //
            // La placa del operador SI es gris y contrasta con el crema de la carcasa: son
            // dos piezas distintas tambien en el equipo real. Esto estuvo un rato pintado
            // del mismo beige que el chasis, por una lectura equivocada de la foto en
            // sombra, y el variador perdia el escalon que lo identifica.
            Pintar(operador, new Color(0.53f, 0.53f, 0.51f), 0.25f);

            // El bisel oscuro NO rodea solo a la ventana: es una franja que cruza la placa
            // de lado a lado, y sobre ella van la ventana roja a la izquierda y los cuatro
            // recuadros de ALM / REV / DRV / FOUT a la derecha.
            //
            // Asi es en la foto frontal, y es lo que hacia que el bloque de indicadores se
            // viera distinto: estaban escritos directamente sobre el gris de la placa, sin
            // la banda oscura debajo. Medido sobre v1000_frontal.jpg con la placa de x=165
            // a 600 y de y=500 a 820: la banda va del 1 % al 98 % del ancho y del 2,5 % al
            // 32,5 % del alto.
            float yBanda = yOperador + altoOperador * 0.5f - altoOperador * 0.175f;

            var marco = VdfMallas.Pieza(padre, "MarcoPantalla", canto,
                new Vector3(-ancho * 0.075f, yBanda, -fondo * 1.03f),
                new Vector3(anchoOperador * 0.97f, altoOperador * 0.30f, fondo * 0.02f));
            Pintar(marco, new Color(0.24f, 0.24f, 0.26f), 0.3f);

            // La ventana roja: el 51,7 % del ancho de la placa, centrada al 28 % desde la
            // izquierda, o sea pegada al lado izquierdo de la banda.
            float anchoVentana = anchoOperador * 0.517f;
            float xVentana = -ancho * 0.075f - anchoOperador * 0.5f + anchoOperador * 0.28f;
            float yVentana = yOperador + altoOperador * 0.5f - altoOperador * 0.156f;

            var pantalla = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pantalla.name = "AnclajePantalla";
            pantalla.transform.SetParent(padre, false);
            pantalla.transform.localScale =
                new Vector3(anchoVentana, altoOperador * 0.219f, fondo * 0.02f);
            pantalla.transform.localPosition =
                new Vector3(xVentana, yVentana, -fondo * 1.05f);
            // La ventana del display es ROJA, que es lo que se ve con el equipo apagado.
            Pintar(pantalla, new Color(0.34f, 0.09f, 0.10f), 0.65f);

            // Las teclas las monta VfdKeypadView sobre este anclaje.
            var teclado = new GameObject("AnclajeTeclado");
            teclado.transform.SetParent(padre, false);
            teclado.transform.localPosition =
                new Vector3(-ancho * 0.02f, yOperador - altoOperador * 0.20f, -fondo * 1.04f);

            float bordeOperador = -ancho * 0.075f + anchoOperador * 0.5f;
            float anchoTapa = anchoOperador * 0.16f;
            var tapa = VdfMallas.Pieza(padre, "TapaPuerto", canto,
                new Vector3(bordeOperador - anchoTapa * 0.5f - ancho * 0.018f,
                            yOperador - altoOperador * 0.02f, -fondo * 1.02f),
                new Vector3(anchoTapa, altoOperador * 0.50f, fondo * 0.04f));
            Pintar(tapa, new Color(0.60f, 0.60f, 0.59f), 0.25f);

            // --- La etiqueta de identificación, que es media carcasa ----------------------
            var placa = VdfMallas.Pieza(padre, "PlacaIdentificacion", canto,
                new Vector3(0f, -alto * 0.17f, -fondo * 1.005f),
                new Vector3(ancho * 0.94f, alto * 0.60f, fondo * 0.012f));
            // Gris algo mas oscuro que el del operador: (0,50 0,49 0,48) en la foto
            // frontal, contra el (0,53 0,53 0,51) de la placa de arriba.
            Pintar(placa, new Color(0.50f, 0.49f, 0.48f), 0.2f);

            // Todo lo que va sobre la placa se situa por FRACCIONES de la propia placa y
            // alineado como en la foto, que es a la IZQUIERDA.
            //
            // Estaba casi todo centrado, y eso solo ya hacia que la placa no se pareciera:
            // en el V1000 real YASKAWA, V1000 y la pegatina de inventario estan pegados al
            // lado izquierdo, y el hueco de la derecha lo ocupa el bloque de texto tecnico.
            // Medido sobre v1000_frontal.jpg con la placa de x=165 a 600 y de y=830 a 1410.
            float anchoPlaca = ancho * 0.94f;
            float altoPlaca = alto * 0.60f;
            float yPlaca = -alto * 0.17f;

            CrearTexto("Etiqueta_Marca", padre, "YASKAWA",
                new Vector3(anchoPlaca * -0.220f, yPlaca + altoPlaca * 0.428f, -fondo * 1.02f),
                new Vector2(anchoPlaca * 0.46f, altoPlaca * 0.085f),
                new Color(0.94f, 0.94f, 0.93f));

            CrearTexto("Etiqueta_V1000", padre, "V1000",
                new Vector3(anchoPlaca * -0.213f, yPlaca + altoPlaca * 0.316f, -fondo * 1.02f),
                new Vector2(anchoPlaca * 0.48f, altoPlaca * 0.145f),
                new Color(0.96f, 0.96f, 0.95f));

            // El bloque de texto tecnico de la derecha: la lista de lo que muestra el
            // display. No se lee a tamano real y no hace falta que se lea, pero sin el la
            // mitad derecha de la placa quedaba vacia y el equipo no se reconocia.
            CrearTexto("Etiqueta_Tecnica", padre,
                TextoTecnico(),
                new Vector3(anchoPlaca * 0.160f, yPlaca + altoPlaca * 0.147f, -fondo * 1.02f),
                new Vector2(anchoPlaca * 0.46f, altoPlaca * 0.230f),
                new Color(0.86f, 0.86f, 0.85f));

            // La pegatina amarilla del inventario, que en este módulo pone INV.5.
            var inventario = VdfMallas.Pieza(padre, "EtiquetaInventario", canto,
                new Vector3(anchoPlaca * -0.323f, yPlaca + altoPlaca * 0.126f, -fondo * 1.02f),
                new Vector3(anchoPlaca * 0.27f, altoPlaca * 0.095f, fondo * 0.008f));
            Pintar(inventario, new Color(0.96f, 0.88f, 0.42f), 0.2f);

            CrearTexto("Etiqueta_Inv", padre, "INV.5",
                new Vector3(anchoPlaca * -0.323f, yPlaca + altoPlaca * 0.126f, -fondo * 1.03f),
                new Vector2(anchoPlaca * 0.20f, altoPlaca * 0.065f),
                new Color(0.22f, 0.20f, 0.12f));

            // El aviso de seguridad: texto pequeño alineado a la izquierda, no una banda
            // centrada que cruza el equipo.
            CrearTexto("Etiqueta_Aviso", padre, "WARNING   Risk of electric shock",
                new Vector3(anchoPlaca * -0.060f, yPlaca - altoPlaca * 0.064f, -fondo * 1.02f),
                new Vector2(anchoPlaca * 0.78f, altoPlaca * 0.055f),
                new Color(0.88f, 0.88f, 0.87f));

            // Y la etiqueta amarilla de abajo, que en el módulo dice a qué motor va.
            var destino = VdfMallas.Pieza(padre, "EtiquetaDestino", canto,
                new Vector3(anchoPlaca * 0.017f, yPlaca - altoPlaca * 0.448f, -fondo * 1.01f),
                new Vector3(anchoPlaca * 0.86f, altoPlaca * 0.105f, fondo * 0.008f));
            Pintar(destino, new Color(0.96f, 0.90f, 0.52f), 0.2f);

            CrearTexto("Etiqueta_Destino", padre, "MOTOR ALINEADOR",
                new Vector3(anchoPlaca * 0.017f, yPlaca - altoPlaca * 0.448f, -fondo * 1.02f),
                new Vector2(anchoPlaca * 0.78f, altoPlaca * 0.060f),
                new Color(0.20f, 0.19f, 0.14f));

            // El tornillo de la tapa, arriba a la derecha.
            var tornillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tornillo.name = "TornilloTapa";
            tornillo.transform.SetParent(padre, false);
            float radio = ancho * 0.07f;
            tornillo.transform.localScale = new Vector3(radio, fondo * 0.02f, radio);
            tornillo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tornillo.transform.localPosition = new Vector3(ancho * 0.37f, alto * 0.085f, -fondo * 1.01f);
            Pintar(tornillo, new Color(0.72f, 0.72f, 0.70f), 0.6f);
        }

        private static void ConstruirEtiqueta(VdfPanelElement el, Transform padre)
        {
            // Las etiquetas serigrafiadas van sobre la placa; las de componente, debajo.
            bool esSerigrafia = el.Tipo == VdfElementType.Etiqueta;

            float lado = el.EtiquetaArriba ? 1f : -1f;
            float separacion = el.SeparacionEtiqueta > 0f
                ? el.SeparacionEtiqueta
                : el.Tamano.y * 0.5f + 0.006f + el.Profundidad * 0.15f;
            float desplazamientoY = esSerigrafia ? 0f : lado * separacion;

            // Caja de la etiqueta. Se ensanchó en la fase 2 (de 1,9x/52 mm/17 mm a
            // 2,1x/58 mm/21 mm) porque los rótulos no se leían a la distancia a la que se
            // trabaja de pie frente al tablero. Sigue por debajo de la separación entre
            // bornes (50–62 mm), así que no se pisan entre sí.
            Vector2 caja = esSerigrafia
                ? el.Tamano
                : new Vector2(Mathf.Max(el.Tamano.x * 1.6f, 0.022f), 0.009f);

            // El color lo decide el elemento, no el constructor. Todo el rótulo del tablero
            // iba en negro fijo, y el del módulo real está impreso en azul sobre placa blanca:
            // por mucho que el layout pidiera azul, salía negro igual.
            Color tinta = esSerigrafia ? el.Color : new Color(0.08f, 0.08f, 0.10f);

            CrearTexto("Etiqueta", padre, el.Etiqueta,
                new Vector3(0f, desplazamientoY, -0.0025f), caja, tinta);
        }

        // =====================================================================================
        // Utilidades
        // =====================================================================================

        private static TextMeshPro CrearTexto(string nombre, Transform padre, string texto,
                                              Vector3 posicionLocal, Vector2 caja, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicionLocal;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = texto;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            // Negrita: en serigrafía de 2–3 mm de trazo, el grosor pesa más que el tamaño.
            // Un rótulo fino se deshace en gris por el antialiasing mucho antes de volverse
            // pequeño, y eso ocurre tanto en el monitor como, peor, con la resolución por
            // ojo de una Quest.
            tmp.fontStyle = FontStyles.Bold;

            // El autoajuste evita tener que adivinar la relación entre fontSize y metros:
            // TMP encaja el texto dentro de la caja que le damos en unidades del mundo.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.02f;
            tmp.fontSizeMax = 1.2f;

            var rt = tmp.rectTransform;
            rt.sizeDelta = caja;
            rt.pivot = new Vector2(0.5f, 0.5f);

            return tmp;
        }

        /// <summary>Acceso al pintado desde otras partes del proyecto (la vista del teclado).</summary>
        public static void PintarPublico(GameObject go, Color color, float suavidad, bool emisivo = false)
        {
            Pintar(go, color, suavidad, emisivo);
        }

        private static readonly Dictionary<string, Material> _cacheMateriales = new Dictionary<string, Material>();

        private static void Pintar(GameObject go, Color color, float suavidad, bool emisivo = false)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.sharedMaterial = ObtenerMaterial(color, suavidad, emisivo);
        }

        /// <summary>
        /// Devuelve un material compartido por color. En el editor lo guarda como asset,
        /// para que el tablero siga pintado después de un domain reload en vez de quedarse
        /// en rosa.
        /// </summary>
        public static Material ObtenerMaterial(Color color, float suavidad, bool emisivo = false)
        {
            string clave = ColorUtility.ToHtmlStringRGB(color) + "_" + Mathf.RoundToInt(suavidad * 100f) + (emisivo ? "_E" : "");

            Material cacheado;
            if (_cacheMateriales.TryGetValue(clave, out cacheado) && cacheado != null)
                return cacheado;

#if UNITY_EDITOR
            string carpeta = "Assets/_VDF/Materials";
            string ruta = carpeta + "/VDF_" + clave + ".mat";

            var existente = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null)
            {
                _cacheMateriales[clave] = existente;
                return existente;
            }
#endif

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader) { name = "VDF_" + clave };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", suavidad);
            mat.SetFloat("_Metallic", 0f);

            if (emisivo)
            {
                // El keyword se deja activo para que la simulacion pueda subir la emision,
                // pero se hornea NEGRO: un piloto en reposo esta apagado. Horneado a
                // color*0,35 el tablero nacia con los siete pilotos encendidos, mas claros
                // incluso que la lamina blanca, y en la foto son lentes oscuras y mates.
                // En ejecucion manda VdfSimulacion.Encender, que escribe por property block.
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_VDF/Materials"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_VDF", "Materials");
            UnityEditor.AssetDatabase.CreateAsset(mat, "Assets/_VDF/Materials/VDF_" + clave + ".mat");
#endif

            _cacheMateriales[clave] = mat;
            return mat;
        }

        // =====================================================================================
        // Motor
        // =====================================================================================

        /// <summary>
        /// Motor trifásico: cuerpo cilíndrico, eje visible y una marca en el eje para
        /// que se note el giro. Se coloca como objeto independiente, delante del tablero.
        /// </summary>
        public static GameObject ConstruirMotor(Vector3 posicion, Transform padre = null)
        {
            var raiz = new GameObject(NombreMotor);
            if (padre != null) raiz.transform.SetParent(padre, false);
            raiz.transform.localPosition = posicion;

            // Carcasa: cilindro tumbado, eje a lo largo de X.
            var carcasa = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            carcasa.name = "Carcasa";
            carcasa.transform.SetParent(raiz.transform, false);
            carcasa.transform.localScale = new Vector3(0.110f, 0.075f, 0.110f);
            carcasa.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Pintar(carcasa, new Color(0.22f, 0.36f, 0.52f), 0.35f);

            // Patas.
            var patas = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patas.name = "Bancada";
            patas.transform.SetParent(raiz.transform, false);
            patas.transform.localScale = new Vector3(0.150f, 0.016f, 0.090f);
            patas.transform.localPosition = new Vector3(0f, -0.062f, 0f);
            Pintar(patas, new Color(0.18f, 0.18f, 0.20f), 0.2f);

            var placa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placa.name = "PlacaCaracteristicas";
            placa.transform.SetParent(raiz.transform, false);
            placa.transform.localScale = new Vector3(0.105f, 0.050f, 0.002f);
            placa.transform.localPosition = new Vector3(0f, 0.004f, -0.0565f);
            var colPlaca = placa.GetComponent<Collider>();
            if (colPlaca != null)
            {
                if (Application.isPlaying) Object.Destroy(colPlaca); else Object.DestroyImmediate(colPlaca);
            }
            Pintar(placa, new Color(0.95f, 0.95f, 0.93f), 0.2f);

            CrearTexto("PlacaLinea1", raiz.transform, "0,25 kW   1,2 A",
                new Vector3(0f, 0.015f, -0.0580f), new Vector2(0.095f, 0.018f), new Color(0.08f, 0.08f, 0.10f));
            CrearTexto("PlacaLinea2", raiz.transform, "3~  220 V  60 Hz",
                new Vector3(0f, -0.007f, -0.0580f), new Vector2(0.095f, 0.016f), new Color(0.08f, 0.08f, 0.10f));

            // Caja de bornes.
            var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caja.name = "CajaBornes";
            caja.transform.SetParent(raiz.transform, false);
            caja.transform.localScale = new Vector3(0.055f, 0.030f, 0.050f);
            caja.transform.localPosition = new Vector3(0f, 0.062f, 0f);
            Pintar(caja, new Color(0.18f, 0.18f, 0.20f), 0.2f);

            // Eje giratorio: nodo separado para poder rotarlo desde la lógica.
            var eje = new GameObject("Eje");
            eje.transform.SetParent(raiz.transform, false);
            eje.transform.localPosition = new Vector3(0.105f, 0f, 0f);

            var ejeGeo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ejeGeo.name = "EjeGeometria";
            ejeGeo.transform.SetParent(eje.transform, false);
            ejeGeo.transform.localScale = new Vector3(0.018f, 0.035f, 0.018f);
            ejeGeo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Pintar(ejeGeo, new Color(0.72f, 0.73f, 0.75f), 0.75f);

            // Marca para que el giro se vea: un disco con una franja.
            var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disco.name = "Disco";
            disco.transform.SetParent(eje.transform, false);
            disco.transform.localScale = new Vector3(0.070f, 0.004f, 0.070f);
            disco.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            disco.transform.localPosition = new Vector3(0.036f, 0f, 0f);
            Pintar(disco, new Color(0.80f, 0.81f, 0.83f), 0.4f);

            var franja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            franja.name = "Marca";
            franja.transform.SetParent(eje.transform, false);
            franja.transform.localScale = new Vector3(0.006f, 0.064f, 0.008f);
            franja.transform.localPosition = new Vector3(0.040f, 0f, 0f);
            Pintar(franja, new Color(0.86f, 0.17f, 0.14f), 0.3f);

            // Bornes U/V/W sobre la caja de bornes: el cableado de la practica los usa.
            string[] fases = { "U", "V", "W" };
            Color[] colores =
            {
                new Color(0.78f, 0.14f, 0.12f),
                new Color(0.10f, 0.10f, 0.11f),
                new Color(0.55f, 0.56f, 0.58f)
            };
            for (int i = 0; i < 3; i++)
            {
                CrearBorneSuelto(raiz.transform, "MOT-" + fases[i], fases[i], "Motor",
                    new Vector3(-0.020f + i * 0.020f, 0.080f, -0.016f), colores[i]);
            }

            return raiz;
        }

        /// <summary>
        /// Crea una bornera fuera de un layout (por ejemplo sobre el motor), con la misma
        /// geometria, marcador y blanco de clic que las del tablero. Asi el grafo de
        /// cableado ve todos los bornes igual, esten donde esten.
        /// </summary>
        public static VdfTerminal CrearBorneSuelto(Transform padre, string id, string etiqueta,
                                                   string grupo, Vector3 posicionLocal, Color color)
        {
            const float diametro = 0.016f;
            const float profundidad = 0.012f;

            var nodo = new GameObject("Borne_" + id);
            nodo.transform.SetParent(padre, false);
            nodo.transform.localPosition = posicionLocal;

            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuerpo.name = "Cuerpo";
            cuerpo.transform.SetParent(nodo.transform, false);
            cuerpo.transform.localScale = new Vector3(diametro, profundidad * 0.5f, diametro);
            cuerpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cuerpo.transform.localPosition = new Vector3(0f, 0f, -profundidad * 0.5f);
            Pintar(cuerpo, color, 0.25f);

            CrearTexto("Etiqueta", nodo.transform, etiqueta,
                new Vector3(0f, -0.016f, -profundidad), new Vector2(0.018f, 0.010f),
                new Color(0.90f, 0.90f, 0.92f));

            return MarcarComoBorne(nodo, id, etiqueta, grupo, profundidad);
        }
    }
}
