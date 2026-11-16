
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Pantalla de siete segmentos del operador digital del V1000.
    ///
    /// Sustituye al rótulo de TextMeshPro que hacía de pantalla hasta la fase 3. La
    /// diferencia no es decorativa: un display real **no puede mostrar cualquier texto**, y
    /// eso se nota. Los segmentos apagados siguen viéndose como marcas tenues, los dígitos
    /// tienen la forma angulosa característica y el punto decimal ocupa su sitio en la
    /// esquina en vez de robar un carácter. Un alumno que haya visto el equipo reconoce la
    /// pantalla; con texto proporcional, no.
    ///
    /// Cada dígito son siete cajas más el punto. Con 5 dígitos son 40 cajas, que comparten
    /// tres materiales (encendido, encendido en falla y apagado), así que el coste en Quest
    /// es despreciable.
    ///
    /// Convenio de segmentos, el estándar:
    /// <code>
    ///      aaaa
    ///     f    b
    ///     f    b
    ///      gggg
    ///     e    c
    ///     e    c
    ///      dddd   dp
    /// </code>
    /// Bit 0 = a, bit 1 = b, ... bit 6 = g.
    /// </summary>
    public class VfdSieteSegmentos : MonoBehaviour
    {
        /// <summary>Segmentos por dígito: los siete más el punto decimal.</summary>
        public const int PartesPorDigito = 8;

        private const int IndicePunto = 7;

        // Las referencias van serializadas por el mismo motivo que en VfdKeypadView: la
        // pantalla se construye en Edit Mode y se guarda en la escena, y un campo privado
        // sin [SerializeField] vuelve a null al entrar en Play Mode.
        [SerializeField] private Renderer[] _partes;
        [SerializeField] private int _numeroDigitos;

        [Header("Colores")]
        public Color ColorEncendido = new Color(0.25f, 1f, 0.45f);
        public Color ColorFalla = new Color(1f, 0.35f, 0.25f);
        [Tooltip("Los segmentos apagados no desaparecen: en un LED real se intuyen.")]
        public Color ColorApagado = new Color(0.055f, 0.10f, 0.07f);

        public int NumeroDigitos { get { return _numeroDigitos; } }

        public bool Construida { get { return _partes != null && _partes.Length > 0; } }

        // =====================================================================================
        // Tipografía de siete segmentos
        // =====================================================================================

        /// <summary>
        /// Patrón de segmentos de un carácter. Devuelve 0 (todo apagado) para el espacio y
        /// para lo que no se pueda representar.
        ///
        /// Un display de siete segmentos no tiene todas las letras, y esa limitación es la
        /// razón de que los equipos industriales usen códigos como <c>oPE</c>, <c>End</c> o
        /// <c>Uv1</c>: están escritos para poder dibujarse aquí.
        /// </summary>
        public static int Patron(char c)
        {
            switch (c)
            {
                case '0': return 0x3F;
                case '1': return 0x06;
                case '2': return 0x5B;
                case '3': return 0x4F;
                case '4': return 0x66;
                case '5': return 0x6D;
                case '6': return 0x7D;
                case '7': return 0x07;
                case '8': return 0x7F;
                case '9': return 0x6F;

                case 'A': return 0x77;
                case 'a': return 0x5F;
                case 'B':
                case 'b': return 0x7C;
                case 'C': return 0x39;
                case 'c': return 0x58;
                case 'D':
                case 'd': return 0x5E;
                case 'E': return 0x79;
                case 'e': return 0x7B;
                case 'F':
                case 'f': return 0x71;
                case 'G':
                case 'g': return 0x3D;
                case 'H': return 0x76;
                case 'h': return 0x74;
                case 'I': return 0x06;
                case 'i': return 0x04;
                case 'J':
                case 'j': return 0x1E;
                case 'L': return 0x38;
                case 'l': return 0x06;
                // La M no existe en siete segmentos. Se usa la aproximación habitual, que
                // deja las dos verticales y la barra de arriba.
                case 'M':
                case 'm': return 0x37;
                case 'N':
                case 'n': return 0x54;
                case 'O': return 0x3F;
                case 'o': return 0x5C;
                case 'P':
                case 'p': return 0x73;
                case 'q': return 0x67;
                case 'R':
                case 'r': return 0x50;
                case 'S':
                case 's': return 0x6D;
                case 'T':
                case 't': return 0x78;
                case 'U': return 0x3E;
                case 'u': return 0x1C;
                case 'V': return 0x3E;
                case 'v': return 0x1C;
                case 'Y':
                case 'y': return 0x6E;

                case '-': return 0x40;
                case '_': return 0x08;
                case '=': return 0x48;
                case '°': return 0x63;
                case ' ': return 0x00;
            }
            return 0x00;
        }

        // =====================================================================================
        // Pintado
        // =====================================================================================

        /// <summary>
        /// Vuelca un texto en la pantalla.
        ///
        /// El punto decimal NO consume un dígito: se engancha al carácter anterior, igual que
        /// en el equipo. Por eso "F00.00" cabe en los cinco dígitos del V1000.
        /// </summary>
        /// <param name="texto">Lo que devuelve <see cref="VfdKeypad.TextoPantalla"/>.</param>
        /// <param name="digitoParpadeante">
        /// Índice del dígito en edición (contando solo dígitos, sin el punto), o -1 si no hay.
        /// </param>
        /// <param name="visible">Fase del parpadeo: en false, el dígito en edición se apaga.</param>
        /// <param name="enFalla">Pinta en rojo en vez de en verde.</param>
        public void Mostrar(string texto, int digitoParpadeante, bool visible, bool enFalla)
        {
            if (!Construida) return;

            var patrones = new int[_numeroDigitos];
            var puntos = new bool[_numeroDigitos];

            int celdas = Repartir(texto, patrones, puntos);

            // Centrado: los mensajes cortos ("End", "oPE") quedan en medio y no pegados a un
            // borde, que es como los muestra el equipo.
            int desplazamiento = Mathf.Max(0, (_numeroDigitos - celdas) / 2);
            if (desplazamiento > 0)
            {
                for (int i = _numeroDigitos - 1; i >= 0; i--)
                {
                    int origen = i - desplazamiento;
                    patrones[i] = origen >= 0 ? patrones[origen] : 0;
                    puntos[i] = origen >= 0 && puntos[origen];
                }
            }

            int celdaParpadeante = digitoParpadeante >= 0 ? digitoParpadeante + desplazamiento : -1;

            Material encendido = MaterialSegmento(enFalla ? ColorFalla : ColorEncendido, true);
            Material apagado = MaterialSegmento(ColorApagado, false);

            for (int d = 0; d < _numeroDigitos; d++)
            {
                // El dígito en edición se apaga entero durante media fase del parpadeo.
                bool apagarDigito = (d == celdaParpadeante) && !visible;

                for (int s = 0; s < 7; s++)
                {
                    bool on = !apagarDigito && (patrones[d] & (1 << s)) != 0;
                    Pintar(d * PartesPorDigito + s, on ? encendido : apagado);
                }

                bool punto = !apagarDigito && puntos[d];
                Pintar(d * PartesPorDigito + IndicePunto, punto ? encendido : apagado);
            }
        }

        /// <summary>
        /// Traduce el texto a patrones por celda. Devuelve cuántas celdas ocupa.
        /// Un punto detrás de un carácter se engancha a su celda; un punto suelto (como el de
        /// "A.tUn") ocupa celda propia.
        ///
        /// Es público y estático para poder probarlo sin construir la pantalla: es la parte
        /// con reglas de verdad, y es donde se decide que "F00.00" quepa en cinco dígitos.
        /// </summary>
        public static int Repartir(string texto, int[] patrones, bool[] puntos)
        {
            for (int i = 0; i < patrones.Length; i++) { patrones[i] = 0; puntos[i] = false; }
            if (string.IsNullOrEmpty(texto)) return 0;

            int celda = 0;
            bool celdaAbierta = false;   // la celda actual ya tiene carácter y admite punto

            for (int i = 0; i < texto.Length && celda < patrones.Length; i++)
            {
                char c = texto[i];

                if (c == '.' || c == ',')
                {
                    if (celdaAbierta && !puntos[celda - 1])
                    {
                        puntos[celda - 1] = true;
                        continue;
                    }
                    // Punto sin carácter delante: se queda solo en su celda.
                    patrones[celda] = 0;
                    puntos[celda] = true;
                    celda++;
                    celdaAbierta = false;
                    continue;
                }

                patrones[celda] = Patron(c);
                celda++;
                celdaAbierta = true;
            }

            return celda;
        }

        private void Pintar(int indice, Material material)
        {
            if (indice < 0 || indice >= _partes.Length) return;
            var r = _partes[indice];
            if (r != null) r.sharedMaterial = material;
        }

        private static Material MaterialSegmento(Color color, bool emisivo)
        {
            return VdfPanelBuilder.ObtenerMaterial(color, 0.5f, emisivo);
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        /// <summary>
        /// Construye los dígitos bajo un padre. El conjunto queda centrado en el origen local
        /// del padre y mirando hacia -Z, que es por donde mira el usuario (mismo convenio que
        /// <see cref="VdfPanelBuilder"/>).
        /// </summary>
        /// <param name="padre">Nodo sin escalar donde colgar la pantalla.</param>
        /// <param name="digitos">Número de dígitos. El V1000 tiene 5.</param>
        /// <param name="anchoTotal">Ancho del conjunto, en metros.</param>
        /// <param name="altoDigito">Alto de un dígito, en metros.</param>
        /// <param name="z">Desplazamiento en Z respecto al padre (negativo = hacia el usuario).</param>
        public static VfdSieteSegmentos Construir(Transform padre, int digitos,
                                                  float anchoTotal, float altoDigito, float z)
        {
            var go = new GameObject("SieteSegmentos");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);

            var panel = go.AddComponent<VfdSieteSegmentos>();
            panel._numeroDigitos = Mathf.Max(1, digitos);
            panel._partes = new Renderer[panel._numeroDigitos * PartesPorDigito];

            // Reparto del ancho: cada dígito ocupa su celda y entre celdas queda un hueco.
            float paso = anchoTotal / panel._numeroDigitos;
            float ancho = paso * 0.74f;              // el resto es separación y sitio del punto
            float grosorSeg = altoDigito * 0.13f;
            float profundidad = 0.0012f;
            float x0 = -anchoTotal * 0.5f + paso * 0.5f;

            var apagado = MaterialSegmento(panel.ColorApagado, false);

            for (int d = 0; d < panel._numeroDigitos; d++)
            {
                var nodo = new GameObject("Digito_" + d);
                nodo.transform.SetParent(go.transform, false);
                nodo.transform.localPosition = new Vector3(x0 + d * paso, 0f, 0f);

                float w = ancho;
                float h = altoDigito;
                float t = grosorSeg;
                float largoH = w - t;             // segmentos horizontales
                float largoV = h * 0.5f - t;      // segmentos verticales

                // Orden: a, b, c, d, e, f, g, punto. Debe coincidir con los bits de Patron().
                Crear(panel, nodo.transform, d, 0, "a", new Vector3(0f, h * 0.5f, 0f), new Vector3(largoH, t, profundidad), apagado);
                Crear(panel, nodo.transform, d, 1, "b", new Vector3(w * 0.5f, h * 0.25f, 0f), new Vector3(t, largoV, profundidad), apagado);
                Crear(panel, nodo.transform, d, 2, "c", new Vector3(w * 0.5f, -h * 0.25f, 0f), new Vector3(t, largoV, profundidad), apagado);
                Crear(panel, nodo.transform, d, 3, "d", new Vector3(0f, -h * 0.5f, 0f), new Vector3(largoH, t, profundidad), apagado);
                Crear(panel, nodo.transform, d, 4, "e", new Vector3(-w * 0.5f, -h * 0.25f, 0f), new Vector3(t, largoV, profundidad), apagado);
                Crear(panel, nodo.transform, d, 5, "f", new Vector3(-w * 0.5f, h * 0.25f, 0f), new Vector3(t, largoV, profundidad), apagado);
                Crear(panel, nodo.transform, d, 6, "g", new Vector3(0f, 0f, 0f), new Vector3(largoH, t, profundidad), apagado);
                Crear(panel, nodo.transform, d, IndicePunto, "dp",
                      new Vector3(w * 0.5f + t * 0.9f, -h * 0.5f - t * 0.45f, 0f),
                      new Vector3(t * 1.6f, t * 1.6f, profundidad), apagado);
            }

            return panel;
        }

        private static void Crear(VfdSieteSegmentos panel, Transform padre, int digito, int parte,
                                  string nombre, Vector3 posicion, Vector3 tamano, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicion;
            go.transform.localScale = tamano;

            // Los segmentos no participan en interacción: quitarles el collider evita que
            // intercepten el rayo del ratón o del mando cuando se va a pulsar una tecla.
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            panel._partes[digito * PartesPorDigito + parte] = r;
        }
    }
}
