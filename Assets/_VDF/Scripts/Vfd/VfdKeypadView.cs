using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VDF
{

    /// <summary>
    /// Vista del operador digital del V1000: pantalla LED de 5 dígitos, indicadores
    /// ALM / REV / DRV / FOUT y las ocho teclas, construidas proceduralmente sobre los
    /// anclajes que dejó <see cref="VdfPanelBuilder"/> en el cuerpo del variador.
    ///
    /// Toda la lógica vive en <see cref="VfdKeypad"/>, que es C# puro y está probado
    /// aparte. Esta clase solo dibuja y traduce clics en pulsaciones, de modo que el
    /// día que se añada interacción XR (E6) basta con llamar a <see cref="Pulsar"/>.
    /// </summary>
    public class VfdKeypadView : MonoBehaviour
    {
        [Header("Referencias")]
        public Camera Camara;

        [Header("Aspecto")]
        public Color ColorDisplay = new Color(0.25f, 1f, 0.45f);
        public Color ColorDisplayFalla = new Color(1f, 0.35f, 0.25f);

        /// <summary>El modelo del variador al que está conectado este teclado.</summary>
        public VfdModel Vfd { get; private set; }

        /// <summary>La lógica de navegación.</summary>
        public VfdKeypad Teclado { get; private set; }

        /// <summary>Un indicador del operador (ALM, REV, DRV, FOUT, LO/RE) y su rótulo.</summary>
        [System.Serializable]
        private struct Indicador
        {
            public string Nombre;
            public TextMeshPro Texto;
        }

        // ⚠️ Estas referencias TIENEN que ir serializadas.
        //
        // La vista se construye en Edit Mode y se guarda en la escena. Al entrar en Play
        // Mode, Unity reserializa: un campo privado SIN [SerializeField] vuelve a null y
        // Refrescar() se salía por el `if (_display == null) return`. Resultado: la lógica
        // decía F10.00, el modelo decía 10 Hz, los tests pasaban... y la pantalla seguía
        // mostrando el texto congelado de cuando se construyó. Compilaba y no daba error.
        [SerializeField] private TextMeshPro _display;
        [SerializeField] private VfdSieteSegmentos _segmentos;
        [SerializeField] private List<Indicador> _indicadores = new List<Indicador>();
        [SerializeField] private List<VfdKeyButton> _teclas = new List<VfdKeyButton>();

        /// <summary>Conecta la vista a un modelo. Hay que llamarlo antes de usarla.</summary>
        public void Conectar(VfdModel vfd)
        {
            Vfd = vfd;
            Teclado = new VfdKeypad(vfd);
        }

        private void Awake()
        {
            if (Camara == null) Camara = Camera.main;
            if (Vfd == null) Conectar(new VfdModel());

            RevincularTeclas();

            // Desde la fase 3 la pantalla son siete segmentos; el rótulo de texto solo queda
            // como respaldo para operadores construidos antes. Basta con tener uno de los dos.
            if ((_segmentos == null || !_segmentos.Construida) && _display == null)
            {
                Debug.LogWarning("[VDF] VfdKeypadView sin pantalla. Reconstruye el operador " +
                                 "con VfdKeypadView.Construir(), o llama a " +
                                 "InstalarSieteSegmentos() sobre el que ya existe.", this);
            }
        }

        /// <summary>
        /// Vuelve a localizar las teclas si las referencias llegaron vacías.
        ///
        /// Las teclas NO cuelgan de este GameObject: viven bajo el anclaje del teclado,
        /// dentro del cuerpo del variador. Esa referencia cruzada entre objetos distintos
        /// de la escena llegó a null al pasar de Edit Mode a Play Mode, mientras que las
        /// de la pantalla (que sí cuelgan del mismo sitio) sobrevivieron. No he confirmado
        /// la causa exacta; en vez de depender de ella, se vuelven a buscar al arrancar,
        /// que además hace la vista robusta si alguien duplica o reordena los objetos.
        /// </summary>
        private void RevincularTeclas()
        {
            bool faltan = _teclas == null || _teclas.Count == 0;
            if (!faltan)
            {
                for (int i = 0; i < _teclas.Count; i++)
                {
                    if (_teclas[i] == null) { faltan = true; break; }
                }
            }
            if (!faltan) return;

            Transform raiz = transform.parent != null ? transform.parent : transform;
            _teclas = new List<VfdKeyButton>(raiz.GetComponentsInChildren<VfdKeyButton>(true));
        }

        private void Update()
        {
            if (Teclado == null) return;

            Teclado.Tick(Time.deltaTime);
            Refrescar();

            var raton = Mouse.current;
            if (raton == null || !raton.leftButton.wasPressedThisFrame) return;
            if (Camara == null) return;

            RaycastHit hit;
            Ray rayo = Camara.ScreenPointToRay(raton.position.ReadValue());
            if (!Physics.Raycast(rayo, out hit, 50f, ~0, QueryTriggerInteraction.Collide)) return;

            var boton = hit.collider.GetComponentInParent<VfdKeyButton>();
            if (boton != null) Pulsar(boton.Tecla);
        }

        /// <summary>Punto de entrada único: lo usa el ratón y lo usará el dedo en VR (E6).</summary>
        public void Pulsar(VfdKey tecla)
        {
            if (Teclado == null) return;

            Teclado.Pulsar(tecla);

            if (_teclas != null)
            {
                for (int i = 0; i < _teclas.Count; i++)
                    if (_teclas[i] != null && _teclas[i].Tecla == tecla) _teclas[i].Animar();
            }

            Refrescar();
        }

        /// <summary>Vuelca el estado del teclado en la pantalla y los indicadores.</summary>
        public void Refrescar()
        {
            if (Teclado == null) return;

            string texto = Teclado.TextoPantalla;
            bool falla = Teclado.LedAlm;

            if (_segmentos != null && _segmentos.Construida)
            {
                // El parpadeo del dígito en edición lo calcula la lógica desde E5; hasta la
                // fase 3 nadie lo pintaba.
                _segmentos.Mostrar(texto, Teclado.CursorEdicion, Teclado.CursorVisible, falla);
            }
            else if (_display != null)
            {
                // Respaldo para operadores construidos antes de la fase 3.
                _display.text = texto;
                _display.color = falla ? ColorDisplayFalla : ColorDisplay;
            }

            PintarIndicador("ALM", Teclado.LedAlm);
            PintarIndicador("REV", Teclado.LedRev);
            PintarIndicador("DRV", Teclado.LedDrv);
            PintarIndicador("FOUT", Teclado.LedFout);
            PintarIndicador("LO/RE", Teclado.LedLocal);
        }

        private void PintarIndicador(string nombre, bool encendido)
        {
            for (int i = 0; i < _indicadores.Count; i++)
            {
                if (_indicadores[i].Nombre != nombre) continue;

                var t = _indicadores[i].Texto;
                if (t == null) return;

                t.color = encendido
                    ? ColorDisplay
                    : new Color(0.22f, 0.26f, 0.24f);   // apagado, pero visible como serigrafía
                return;
            }
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        /// <summary>
        /// Dónde va cada tecla en el frontal del operador, en unidades de celda.
        ///
        /// Ya no es una rejilla de 4×2 «cómoda para VR»: es la del V1000 real, que se ve en
        /// `Docs/Referencia/fotos_entrega1/v1000_frontal.jpg`. Seis teclas de navegación en
        /// dos filas de tres, y debajo RUN y STOP, que son más anchas y ocupan entre las dos
        /// lo mismo que las tres de arriba. Puestas en 4×2 el teclado no se parecía, y es lo
        /// primero que se mira de un variador.
        ///
        /// Columnas y filas en celdas; el ancho en múltiplos de celda.
        /// </summary>
        /// <summary>
        /// Una tecla, situada por FRACCIONES de la placa del operador.
        ///
        /// Antes iba por columnas y filas con un paso calculado a ojo sobre el ancho del
        /// variador, y el bloque quedaba corrido a la derecha: en el equipo real las teclas
        /// ocupan la mitad izquierda de la placa y a su derecha solo esta la tapa del
        /// puerto. Ahora cada tecla lleva donde esta MEDIDA en
        /// `Docs/Referencia/fotos_entrega1/v1000_frontal.jpg`, en tanto por uno de la placa
        /// contando desde su borde izquierdo y desde su borde superior.
        /// </summary>
        private struct Celda
        {
            public VfdKey Tecla;
            public float X;      // centro, fraccion del ancho de la placa desde la izquierda
            public float Y;      // centro, fraccion del alto  de la placa desde arriba
            public float Ancho;  // fraccion del ancho de la placa
            public float Alto;   // fraccion del alto  de la placa

            public Celda(VfdKey tecla, float x, float y, float ancho, float alto)
            {
                Tecla = tecla; X = x; Y = y; Ancho = ancho; Alto = alto;
            }
        }

        // Leido sobre la foto frontal, con la placa del operador de x=165 a 600 y de
        // y=500 a 820 pixeles.
        private static readonly Celda[] Disposicion =
        {
            new Celda(VfdKey.Esc,    0.115f, 0.461f, 0.161f, 0.203f),
            new Celda(VfdKey.Arriba, 0.301f, 0.461f, 0.156f, 0.203f),
            new Celda(VfdKey.LoRe,   0.523f, 0.461f, 0.218f, 0.203f),
            new Celda(VfdKey.Reset,  0.115f, 0.680f, 0.161f, 0.172f),
            new Celda(VfdKey.Abajo,  0.301f, 0.680f, 0.156f, 0.172f),
            new Celda(VfdKey.Enter,  0.523f, 0.680f, 0.218f, 0.172f),
            new Celda(VfdKey.Run,    0.167f, 0.875f, 0.264f, 0.156f),
            new Celda(VfdKey.Stop,   0.466f, 0.875f, 0.264f, 0.156f),
        };

        public static string EtiquetaDe(VfdKey tecla)
        {
            switch (tecla)
            {
                case VfdKey.Esc: return "ESC";
                case VfdKey.Arriba: return "▲";
                case VfdKey.Abajo: return "▼";
                case VfdKey.LoRe: return "LO\nRE";
                case VfdKey.Reset: return "RESET";
                case VfdKey.Enter: return "ENTER";
                case VfdKey.Run: return "RUN";
                case VfdKey.Stop: return "STOP";
            }
            return "?";
        }

        private static Color ColorDe(VfdKey tecla)
        {
            // Verde y rojo para RUN y STOP; el resto, gris oscuro. Estaban en 0,40, que al
            // lado de la foto se veía gris claro: en el equipo real son casi negras.
            if (tecla == VfdKey.Run) return new Color(0.13f, 0.52f, 0.28f);
            if (tecla == VfdKey.Stop) return new Color(0.74f, 0.16f, 0.14f);
            return new Color(0.24f, 0.24f, 0.26f);
        }

        /// <summary>
        /// Construye pantalla y teclas sobre los anclajes que el builder dejó dentro del
        /// cuerpo del V1000, y devuelve la vista lista para conectar a un modelo.
        /// </summary>
        public static VfdKeypadView Construir(Transform anclajePantalla, Transform anclajeTeclado)
        {
            if (anclajePantalla == null || anclajeTeclado == null)
            {
                Debug.LogError("[VDF] VfdKeypadView.Construir: faltan los anclajes del variador.");
                return null;
            }

            var raiz = new GameObject("VDF_Operador");
            raiz.transform.SetParent(anclajePantalla.parent, false);

            var vista = raiz.AddComponent<VfdKeypadView>();
            vista.ConstruirPantalla(anclajePantalla);
            vista.ConstruirTeclas(anclajeTeclado);
            return vista;
        }

        private void ConstruirPantalla(Transform anclaje)
        {
            // OJO: el anclaje de la pantalla es un cubo con ESCALA NO UNIFORME
            // (0.098 x 0.041 x 0.005). Colgar texto de él lo multiplica por esa escala y
            // el rótulo queda aplastado hasta ser invisible. Por eso se cuelga de un nodo
            // propio sin escalar, colocado donde está el anclaje.
            var soporte = new GameObject("Pantalla");
            soporte.transform.SetParent(anclaje.parent, false);
            soporte.transform.localPosition = anclaje.localPosition;
            soporte.transform.localRotation = anclaje.localRotation;

            // Los 5 dígitos, en siete segmentos de verdad (fase 3).
            InstalarSieteSegmentos(soporte.transform);

            // Indicadores: cuatro RECUADROS CLAROS sobre la banda oscura del display.
            //
            // En el V1000 real ALM, REV, DRV y FOUT no son texto suelto sobre el gris de la
            // placa: cada uno es un rectangulo gris claro con la palabra en oscuro dentro, y
            // los cuatro van en 2 x 2 sobre la misma banda oscura en la que esta la ventana
            // roja, a su derecha. Sin el recuadro y sin la banda debajo, el bloque se veia
            // completamente distinto por mucho que las palabras estuvieran bien puestas.
            //
            // Medido sobre v1000_frontal.jpg con la placa del operador de x=165 a 600 y de
            // y=500 a 820, en tanto por uno de la placa:
            //
            //     ALM   centro (62,6 %, 14,7 %)     REV   centro (79,0 %, 14,7 %)
            //     DRV   centro (62,6 %, 23,6 %)     FOUT  centro (79,0 %, 23,6 %)
            //     cada recuadro mide 13,1 % x 6,4 %
            Transform placaInd = anclaje.parent != null ? anclaje.parent.Find("FrontalOperador") : null;
            float anchoPl, altoPl, cxPl, cyPl;
            if (placaInd != null)
            {
                anchoPl = placaInd.localScale.x; altoPl = placaInd.localScale.y;
                cxPl = placaInd.localPosition.x; cyPl = placaInd.localPosition.y;
            }
            else
            {
                Transform cu = anclaje.parent != null ? anclaje.parent.Find("Cuerpo") : null;
                float aw = cu != null ? cu.localScale.x : 0.190f;
                anchoPl = aw * 0.85f; altoPl = aw * 0.64f;
                cxPl = -aw * 0.075f; cyPl = aw * 0.55f;
            }

            float izq = cxPl - anchoPl * 0.5f - soporte.transform.localPosition.x;
            float arr = cyPl + altoPl * 0.5f - soporte.transform.localPosition.y;

            float anchoCaja = anchoPl * 0.131f;
            float altoCaja = altoPl * 0.064f;
            var cantoInd = VdfMallas.CajaBiselada(0.12f, 1);

            string[] nombres = { "ALM", "REV", "DRV", "FOUT" };
            float[] fx = { 0.626f, 0.790f, 0.626f, 0.790f };
            float[] fy = { 0.147f, 0.147f, 0.236f, 0.236f };

            for (int i = 0; i < nombres.Length; i++)
            {
                float x = izq + anchoPl * fx[i];
                float y = arr - altoPl * fy[i];

                var nodo = new GameObject("Ind_" + nombres[i]);
                nodo.transform.SetParent(soporte.transform, false);
                nodo.transform.localPosition = new Vector3(x, y, -0.0035f);

                var recuadro = VdfMallas.Pieza(nodo.transform, "Recuadro", cantoInd,
                    Vector3.zero, new Vector3(anchoCaja, altoCaja, 0.0004f));
                VdfPanelBuilder.PintarPublico(recuadro, new Color(0.78f, 0.79f, 0.78f), 0.25f);

                var t = CrearTexto("Rotulo", nodo.transform, nombres[i],
                    new Vector3(0f, 0f, -0.0006f),
                    new Vector2(anchoCaja * 0.90f, altoCaja * 0.78f),
                    new Color(0.18f, 0.19f, 0.20f));
                _indicadores.Add(new Indicador { Nombre = nombres[i], Texto = t });
            }

            {
                float x = izq + anchoPl * 0.440f;
                float y = arr - altoPl * 0.390f;

                var nodo = new GameObject("Ind_LO/RE");
                nodo.transform.SetParent(soporte.transform, false);
                nodo.transform.localPosition = new Vector3(x, y, -0.0045f);

                float d = anchoPl * 0.030f;
                var punto = VdfMallas.Pieza(nodo.transform, "Punto", cantoInd,
                    Vector3.zero, new Vector3(d, d, 0.0004f));
                VdfPanelBuilder.PintarPublico(punto, new Color(0.30f, 0.31f, 0.30f), 0.25f);

                // El rotulo existe pero no se ve: es lo que PintarIndicador colorea cuando
                // el variador pasa a LOCAL, y asi el punto tiene a quien avisar.
                var t = CrearTexto("Rotulo", nodo.transform, "●",
                    new Vector3(0f, 0f, -0.0006f),
                    new Vector2(d * 0.95f, d * 0.95f),
                    new Color(0.30f, 0.31f, 0.30f));
                _indicadores.Add(new Indicador { Nombre = "LO/RE", Texto = t });
            }
        }

        /// <summary>
        /// Monta la pantalla de siete segmentos y apaga el rótulo de texto si lo había.
        ///
        /// Es público y se puede llamar sobre un operador ya construido: así se actualizan
        /// las escenas que se montaron antes de la fase 3 sin tener que rehacer el teclado
        /// entero, que arrastraría las referencias de las teclas y de los indicadores.
        /// </summary>
        /// <param name="soporte">
        /// Nodo sin escalar donde colgar la pantalla. Si se omite, se busca el hijo
        /// "Pantalla" del propio operador.
        /// </param>
        public VfdSieteSegmentos InstalarSieteSegmentos(Transform soporte = null)
        {
            if (soporte == null)
            {
                Transform raiz = transform.parent != null ? transform.parent : transform;
                soporte = BuscarHijo(raiz, "Pantalla");
            }
            if (soporte == null)
            {
                Debug.LogWarning("[VDF] No se encontró el nodo 'Pantalla' del operador.", this);
                return null;
            }

            var previa = soporte.GetComponentInChildren<VfdSieteSegmentos>(true);
            if (previa != null) DestroyImmediate(previa.gameObject);

            // La pantalla se mide a partir del ANCHO DEL VARIADOR, no con numeros fijos.
            //
            // Con 74 mm fijos, heredados de cuando el V1000 del simulador medía 190 mm de
            // ancho, al ponerlo a su tamano real (68 mm) los digitos salian DISPARADOS
            // fuera del equipo, flotando por encima del teclado. En el V1000 real el hueco
            // del display ocupa algo mas de la mitad del frente.
            float anchoDisplay = 0.074f;
            Transform cuerpo = BuscarHijo(transform.parent != null ? transform.parent : transform, "Cuerpo");
            // 0,40 del ancho, no 0,56: en la foto el display es una ventana roja que ocupa
            // poco mas de un tercio del frente, con los indicadores ALM/REV a su derecha.
            if (cuerpo != null) anchoDisplay = cuerpo.localScale.x * 0.40f;

            _segmentos = VfdSieteSegmentos.Construir(soporte, VfdKeypad.Digitos,
                                                     anchoDisplay,
                                                     anchoDisplay * 0.21f,
                                                     -anchoDisplay * 0.057f);
            _segmentos.ColorEncendido = ColorDisplay;
            _segmentos.ColorFalla = ColorDisplayFalla;

            // El rótulo de texto deja de pintarse, pero no se borra: sigue siendo el
            // respaldo que usa Refrescar() si algún día la pantalla no está construida.
            if (_display != null) _display.gameObject.SetActive(false);

            return _segmentos;
        }

        private static Transform BuscarHijo(Transform raiz, string nombre)
        {
            var todos = raiz.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < todos.Length; i++)
                if (todos[i].name == nombre) return todos[i];
            return null;
        }

        private void ConstruirTeclas(Transform anclaje)
        {
            // Las teclas se situan sobre la PLACA DEL OPERADOR, no sobre el ancho del
            // variador.
            //
            // Antes el bloque se calculaba con fracciones del ancho del equipo y un
            // desplazamiento a ojo, y quedaba corrido a la derecha: en el V1000 real las
            // tres columnas ocupan la mitad izquierda de la placa —ESC empieza a un 11 % del
            // borde y ENTER acaba al 63 %— y lo que hay a la derecha es la tapa del puerto.
            // Con la referencia equivocada, ademas, cualquier retoque del tamano de la placa
            // descolocaba el teclado otra vez.
            Transform placa = anclaje.parent != null ? anclaje.parent.Find("FrontalOperador") : null;

            float anchoPlaca, altoPlaca, centroPlacaX, centroPlacaY;
            if (placa != null)
            {
                anchoPlaca = placa.localScale.x;
                altoPlaca = placa.localScale.y;
                centroPlacaX = placa.localPosition.x;
                centroPlacaY = placa.localPosition.y;
            }
            else
            {
                // Sin placa no hay referencia: se cae a algo razonable en vez de reventar.
                Transform cuerpo = anclaje.parent != null ? anclaje.parent.Find("Cuerpo") : null;
                float anchoVariador = cuerpo != null ? cuerpo.localScale.x : 0.190f;
                anchoPlaca = anchoVariador * 0.85f;
                altoPlaca = anchoVariador * 0.64f;
                centroPlacaX = -anchoVariador * 0.075f;
                centroPlacaY = anchoVariador * 0.55f;
            }

            // Las teclas cuelgan del anclaje, asi que todo lo que se calcula en coordenadas
            // del variador hay que referirlo a el.
            float desX = centroPlacaX - anclaje.localPosition.x;
            float desY = centroPlacaY - anclaje.localPosition.y;

            float izquierda = desX - anchoPlaca * 0.5f;
            float arriba = desY + altoPlaca * 0.5f;

            var canto = VdfMallas.CajaBiselada(0.22f, 2);

            for (int i = 0; i < Disposicion.Length; i++)
            {
                Celda celda = Disposicion[i];
                VfdKey tecla = celda.Tecla;

                float ancho = anchoPlaca * celda.Ancho;
                float alto = altoPlaca * celda.Alto;
                float grosor = Mathf.Max(0.0012f, alto * 0.22f);

                float cx = izquierda + anchoPlaca * celda.X;
                float cy = arriba - altoPlaca * celda.Y;

                var nodo = new GameObject("Tecla_" + tecla);
                nodo.transform.SetParent(anclaje, false);
                nodo.transform.localPosition = new Vector3(cx, cy, 0f);

                // Rectangulo de cantos matados, no un cilindro: las teclas del V1000 son
                // rectangulares y con ocho botones redondos el teclado parecia el de una
                // calculadora.
                var capuchon = VdfMallas.Pieza(nodo.transform, "Capuchon", canto,
                    new Vector3(0f, 0f, -grosor * 0.5f),
                    new Vector3(ancho, alto, grosor));
                VdfPanelBuilder.PintarPublico(capuchon, ColorDe(tecla), 0.22f);

                // Blanco de pulsación generoso: con el dedo en VR hace falta margen.
                var blanco = nodo.AddComponent<BoxCollider>();
                blanco.isTrigger = true;
                blanco.size = new Vector3(ancho * 1.25f, alto * 1.35f, 0.012f);
                blanco.center = new Vector3(0f, 0f, -0.005f);

                CrearTexto("Etiqueta", nodo.transform, EtiquetaDe(tecla),
                    new Vector3(0f, 0f, -grosor - 0.0012f),
                    new Vector2(ancho * 0.90f, alto * 0.74f),
                    new Color(0.95f, 0.95f, 0.96f));

                var boton = nodo.AddComponent<VfdKeyButton>();
                boton.Tecla = tecla;
                boton.Capuchon = capuchon.transform;
                _teclas.Add(boton);
            }
        }

        /// <summary>El salto de linea, como constante: en los rotulos de dos lineas.</summary>
        private const char SaltoDeLinea = (char)10;

        private static TextMeshPro CrearTexto(string nombre, Transform padre, string texto,
                                              Vector3 posicion, Vector2 caja, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicion;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = texto;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            // El autoajuste de TMP con NoWrap solo mira la ALTURA de la caja: la anchura la
            // desborda sin avisar. Con teclas de 11 mm, «RESET» salia cuatro veces mas ancho
            // que su tecla y el teclado era ilegible. Se acota el cuerpo maximo a mano, a
            // partir de la letra mas larga de la linea mas larga.
            int masLarga = 0;
            foreach (string linea in texto.Split(SaltoDeLinea))
                if (linea.Length > masLarga) masLarga = linea.Length;
            int lineas = texto.Split(SaltoDeLinea).Length;

            // El autoajuste de TMP ajusta el cuerpo al ALTO del rectangulo, no al ancho, y
            // el cuerpo no se mide en metros: acotar fontSizeMax con una medida del tablero
            // dejaba las letras invisibles. Lo que si funciona es encoger el RECTANGULO
            // hasta que la linea mas larga quepa a lo ancho, y dejar que el autoajuste haga
            // el resto.
            float altoPorLinea = caja.y / Mathf.Max(1, lineas);
            float altoQueCabe = masLarga > 0
                ? Mathf.Min(altoPorLinea, caja.x / (masLarga * 0.62f))
                : altoPorLinea;

            // Los mismos ajustes que usa el tablero, que llevan dos fases funcionando. El
            // cuerpo de TMP no va en metros, asi que no se toca: lo que se le da es la CAJA.
            //
            // Y la caja que manda es la del ANCHO. Con NoWrap el autoajuste encaja el texto
            // a lo ancho del rectangulo y el alto se lo salta, que es justo al reves de lo
            // que parecia: por eso las teclas anchas de RUN y STOP sacaban la letra mas
            // grande que las estrechas y se salian por arriba y por abajo. Se le da un
            // rectangulo del ancho exacto que debe ocupar el texto, y el alto holgado.
            float anchoQueOcupa = masLarga > 0 ? altoQueCabe * masLarga * 0.62f : caja.x;

            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.02f;
            tmp.fontSizeMax = 1.2f;
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tmp.rectTransform.sizeDelta =
                new Vector2(anchoQueOcupa, altoQueCabe * lineas * 1.4f);

            return tmp;
        }
    }
}
