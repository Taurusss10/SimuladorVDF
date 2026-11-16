
using System.Text;
using TMPro;
using UnityEngine;

namespace VDF
{
    public class VdfPanelGuia : MonoBehaviour
    {
        public const string NombreRaiz = "VDF_PanelGuia";

        [Header("Referencias")]
        public VdfPracticaRunner Runner;

        [Header("Aspecto")]
        public Color ColorFondo = new Color(0.10f, 0.11f, 0.13f);
        public Color ColorTitulo = new Color(1f, 1f, 1f);
        public Color ColorPendiente = new Color(0.62f, 0.65f, 0.70f);
        public Color ColorActual = new Color(1f, 0.84f, 0.26f);
        public Color ColorHecho = new Color(0.36f, 0.86f, 0.46f);
        public Color ColorAviso = new Color(1f, 0.55f, 0.30f);

        [Tooltip("Cuántos pasos se muestran a la vez. El resto se resume arriba.")]
        [Range(3, 12)] public int PasosVisibles = 6;

        // Serializadas por el mismo motivo que en el resto del proyecto: el panel se
        // construye en Edit Mode y se guarda en la escena; un campo privado sin
        // [SerializeField] vuelve a null al entrar en Play Mode.
        [SerializeField] private TextMeshPro _titulo;
        [SerializeField] private TextMeshPro _cuerpo;
        [SerializeField] private TextMeshPro _pie;

        private int _ultimoPasoPintado = -2;

        private void Awake()
        {
            if (Runner == null) Runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            EngancharBotonConfirmar();
            Refrescar();
        }

        /// <summary>
        /// Vuelve a atar qué hace el botón de confirmar, en cada arranque.
        ///
        /// Obligatorio, no decorativo: lo que hace el botón es un delegado, y **los delegados
        /// no se serializan**. Se asigna al construir el panel en el editor y se pierde en
        /// cuanto la escena se guarda y se vuelve a cargar, que es lo que pasa al entrar en
        /// Play y lo que pasa siempre en el instalable.
        ///
        /// Se comprobó: recién construido el botón funcionaba, y tras recargar la escena
        /// aparecía pero no hacía nada. Es el mismo patrón que dejó a los cables sin cuerpo
        /// —estado que vive solo en memoria y que nadie reconstruye al cargar—, y es la
        /// tercera vez en este proyecto que muerde.
        /// </summary>
        private void EngancharBotonConfirmar()
        {
            if (_botonConfirmar == null)
            {
                foreach (var b in GetComponentsInChildren<VdfBotonPractica>(true))
                    if (b.Indice == IndiceBotonConfirmar) _botonConfirmar = b;
            }
            if (_botonConfirmar == null) return;

            _botonConfirmar.AlPulsar = delegate(int _)
            {
                if (Runner != null) Runner.ConfirmarPasoManual();
            };
        }

        /// <summary>Índice con el que se distingue al botón de confirmar de los del menú.</summary>
        public const int IndiceBotonConfirmar = 99;

        private void OnEnable()
        {
            if (Runner != null) Runner.ProgresoCambiado += Refrescar;
        }

        private void OnDisable()
        {
            if (Runner != null) Runner.ProgresoCambiado -= Refrescar;
        }

        private void Update()
        {
            // El evento cubre los cambios de paso, pero el detalle (cuántas uniones faltan,
            // cuántos componentes van identificados) cambia sin que cambie el paso.
            if (Runner != null) Refrescar();
        }

        /// <summary>Vuelca el estado de la práctica en el panel.</summary>
        public void Refrescar()
        {
            if (_titulo == null || _cuerpo == null || Runner == null) return;

            var practica = Runner.Practica;
            if (practica == null)
            {
                _titulo.text = "GUÍA — QUÉ TIENES QUE HACER   <size=70%>elige una práctica en el panel de la derecha</size>";
                _cuerpo.text = "";
                if (_pie != null) _pie.text = "";
                return;
            }

            int actual = Runner.PasoActual();

            _titulo.text = "GUÍA — QUÉ TIENES QUE HACER   <size=70%>práctica "
                         + practica.Numero + ": " + practica.Titulo + "</size>";
            _titulo.color = ColorTitulo;

            // Si el paso AVANZO, se canta. Ver el comentario de CantarPasoCumplido.
            if (_ultimoPasoPintado >= 0 && actual != _ultimoPasoPintado)
                _finDelAviso = Time.time + DuracionAviso;

            _cuerpo.text = CuerpoQueCabe(practica, actual);
            if (_pie != null) _pie.text = ConstruirPie(practica, actual);

            ActualizarBotonConfirmar(practica, actual);

            _ultimoPasoPintado = actual;
        }

        [SerializeField] private VdfBotonPractica _botonConfirmar;

        private void ActualizarBotonConfirmar(VdfPractica practica, int actual)
        {
            if (_botonConfirmar == null) return;

            bool haceFalta = false;
            if (actual >= 0)
            {
                var paso = practica.Paso(actual);
                haceFalta = paso != null && paso.Condicion == VdfCondicion.Manual;
            }

            if (_botonConfirmar.gameObject.activeSelf != haceFalta)
                _botonConfirmar.gameObject.SetActive(haceFalta);
        }

        /// <summary>Cuanto se queda en pantalla el aviso de paso cumplido.</summary>
        private const float DuracionAviso = 3f;
        private float _finDelAviso = -1f;

        /// <summary>¿Hay que estar cantando ahora mismo un paso cumplido?</summary>
        private bool CantarPasoCumplido { get { return Time.time < _finDelAviso; } }

        private string CuerpoQueCabe(VdfPractica practica, int actual)
        {
            string completo = ConstruirCuerpo(practica, actual, 0, true);
            if (completo == _ultimoCompleto) return _ultimoAjustado;
            _ultimoCompleto = completo;

            var caja = _cuerpo.rectTransform.rect;
            string texto = completo;
            for (int menos = 1; menos <= PasosVisibles && !Cabe(texto, caja); menos++)
                texto = ConstruirCuerpo(practica, actual, menos, true);
            // Ni con un solo paso: fuera también la ficha, que es lo que menos urge.
            if (!Cabe(texto, caja))
                texto = ConstruirCuerpo(practica, actual, PasosVisibles, false);

            _ultimoAjustado = texto;
            return texto;
        }

        private string _ultimoCompleto;
        private string _ultimoAjustado;

        private bool Cabe(string texto, Rect caja)
        {
            return _cuerpo.GetPreferredValues(texto, caja.width, 0f).y <= caja.height + 1e-4f;
        }

        /// <summary>Lo que ocupa el cuerpo y lo que mide su caja. Para los tests.</summary>
        public Vector2 AltoCuerpoYCaja()
        {
            if (_cuerpo == null) return Vector2.zero;
            var caja = _cuerpo.rectTransform.rect;
            return new Vector2(_cuerpo.GetPreferredValues(_cuerpo.text, caja.width, 0f).y, caja.height);
        }

        private string ConstruirCuerpo(VdfPractica practica, int actual, int menos, bool conFicha)
        {
            var sb = new StringBuilder();

            int total = practica.TotalPasos;
            int primero = 0;

            // La ficha de lo último que se encontró va ARRIBA y se queda hasta la siguiente.
            // Ver UltimaFicha. Cuando la hay se listan dos pasos menos, para que todo quepa
            // en el atril sin desbordar.
            var ficha = conFicha ? UltimaFicha(practica, actual) : null;
            int visibles = ficha != null ? Mathf.Max(3, PasosVisibles - 3) : PasosVisibles;
            visibles = Mathf.Max(1, visibles - menos);

            if (ficha != null)
            {
                sb.AppendLine(Tinta("<b>LO QUE ACABAS DE ENCONTRAR</b>", ColorFicha));
                sb.AppendLine("<size=88%>" + Tinta(ficha.Explicacion, ColorFicha) + "</size>");
            }

            if (total > visibles)
            {
                // Con la práctica terminada se enseña el FINAL del guion, no el principio:
                // dejar "y 2 pasos más" bajo un cartel de completada es contradictorio.
                // El anterior al actual, para ver de dónde se viene, solo si hay sitio.
                primero = actual >= 0
                    ? Mathf.Clamp(actual - (visibles > 2 ? 1 : 0), 0, total - visibles)
                    : total - visibles;
            }
            int ultimo = Mathf.Min(total, primero + visibles);

            if (primero > 0)
                sb.AppendLine(Tinta("[x] " + primero + (primero == 1 ? " paso ya completado" : " pasos ya completados"), ColorHecho));

            for (int i = primero; i < ultimo; i++)
            {
                var paso = practica.Paso(i);
                if (paso == null) continue;

                bool hecho = Runner.PasoCumplido(i);
                string marca = hecho ? "[x] " : (i == actual ? " >  " : " .  ");
                Color c = hecho ? ColorHecho : (i == actual ? ColorActual : ColorPendiente);

                sb.AppendLine(Tinta(marca + (i + 1) + ". " + paso.Instruccion, c));

                // La pista y la advertencia solo se enseñan en el paso en curso: en los
                // demás serían ruido.
                if (i == actual)
                {
                    if (paso.TieneAdvertencia)
                        sb.AppendLine(Tinta("     AVISO: " + paso.Advertencia, ColorAviso));

                    if (!string.IsNullOrEmpty(paso.Pista))
                        sb.AppendLine(Tinta("     " + paso.Pista, ColorPista));

                    string detalle = Runner.DetalleDelPasoActual();
                    if (!string.IsNullOrEmpty(detalle))
                        sb.AppendLine(Tinta("     -> <b>" + detalle + "</b>", ColorActual));
                }
            }

            if (ultimo < total)
                sb.AppendLine(Tinta("... y " + (total - ultimo) + " pasos más", ColorPendiente));

            return sb.ToString();
        }

        private string ControlesDelModo()
        {
            bool enVisor = Runner != null && Runner.Simulacion != null
                        && Runner.Simulacion.CableadoXr != null;

            if (enVisor)
                return "Señalar: gatillo  ·  Cable: lateral  ·  Quitarlo: suéltalo en el aire";

            if (Runner != null && Runner.Simulacion != null
                && Runner.Simulacion.ModoMando == VdfModoMando.Interruptores)
                return "Clic: señalar y conectar  ·  Clic derecho: QUITAR cable  ·  1-6: color  ·  A S D: S4 S5 S6";

            return "Señalar y conectar: clic   ·   QUITAR un cable: clic derecho encima   ·   "
                 + "Color: teclas 1 a 6";
        }

        private string ConstruirPie(VdfPractica practica, int actual)
        {
            string controles = "   " + Tinta(ControlesDelModo(), ColorPendiente);

            if (actual < 0)
                return Tinta("PRÁCTICA COMPLETADA — " + practica.TotalPasos + " de " +
                             practica.TotalPasos, ColorHecho) + controles;

            string donde = "Paso " + (actual + 1) + " de " + practica.TotalPasos +
                           "   -   completados " + Runner.PasosCumplidos + controles;

            if (CantarPasoCumplido)
                return Tinta("PASO CUMPLIDO", ColorHecho) + "      " + Tinta(donde, ColorPendiente);

            return Tinta(donde, ColorPendiente);
        }

        /// <summary>Color de la ficha explicativa: distinto de todo lo demás del atril.</summary>
        public Color ColorFicha = new Color(0.45f, 0.85f, 1f);

        /// <summary>Color de la pista del paso actual: claro, distinto del gris de lo pendiente.</summary>
        public Color ColorPista = new Color(0.88f, 0.92f, 1f);

        public static VdfPasoPractica UltimaFicha(VdfPractica practica, int actual)
        {
            if (practica == null) return null;
            int desde = actual >= 0 ? actual - 1 : practica.TotalPasos - 1;
            for (int i = desde; i >= 0; i--)
            {
                var paso = practica.Paso(i);
                if (paso != null && paso.TieneExplicacion) return paso;
            }
            return null;
        }

        private static string Tinta(string texto, Color c)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + texto + "</color>";
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        /// <summary>
        /// Monta el atril en la escena. Idempotente: si ya existe, lo rehace.
        /// </summary>
        /// <param name="posicion">Dónde va el panel, en coordenadas de mundo.</param>
        /// <param name="rotacionY">Giro en Y, para orientarlo hacia el usuario.</param>
        public static VdfPanelGuia Construir(Vector3 posicion, float rotacionY = 18f,
                                             float ancho = 0.62f, float alto = 0.46f)
        {
            var previo = GameObject.Find(NombreRaiz);
            if (previo != null)
            {
                if (Application.isPlaying) Destroy(previo); else DestroyImmediate(previo);
            }

            var raiz = new GameObject(NombreRaiz);
            raiz.transform.position = posicion;
            raiz.transform.rotation = Quaternion.Euler(0f, rotacionY, 0f);

            var panel = raiz.AddComponent<VdfPanelGuia>();

            // Fondo. Los componentes del tablero sobresalen hacia -Z, así que el texto del
            // panel también mira hacia -Z para que se lea del derecho.
            var fondo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fondo.name = "Fondo";
            fondo.transform.SetParent(raiz.transform, false);
            fondo.transform.localScale = new Vector3(ancho, alto, 0.012f);
            var colFondo = fondo.GetComponent<Collider>();
            if (colFondo != null)
            {
                if (Application.isPlaying) Destroy(colFondo); else DestroyImmediate(colFondo);
            }
            fondo.GetComponent<Renderer>().sharedMaterial =
                VdfPanelBuilder.ObtenerMaterial(panel.ColorFondo, 0.15f);

            // Las medidas del texto salen del ancho del panel, no de números sueltos. Con
            // tamaños fijos, al encoger el atril para que dejara de ser más grande que el
            // tablero, el cuerpo de 16,5 mm daba unos quince caracteres por línea y la guía
            // se leía a trozos. Las proporciones son las que tenía el panel de 62 cm, que
            // ya estaban probadas.
            float margen = ancho * 0.036f;
            float zTexto = -0.008f;
            float fuenteTitulo = ancho * 0.039f;
            float fuenteCuerpo = ancho * 0.027f;
            float fuentePie = ancho * 0.031f;   // 0,024 se veía a 0,57° desde el puesto

            panel._titulo = CrearTexto(raiz.transform, "Titulo",
                new Vector3(0f, alto * 0.5f - margen - fuenteTitulo * 0.5f, zTexto),
                new Vector2(ancho - margen * 2f, fuenteTitulo * 1.4f), fuenteTitulo,
                TextAlignmentOptions.Left);

            panel._cuerpo = CrearTexto(raiz.transform, "Cuerpo",
                new Vector3(0f, -alto * 0.03f, zTexto),
                new Vector2(ancho - margen * 2f, alto - fuenteTitulo * 2.4f - fuentePie * 3f),
                fuenteCuerpo, TextAlignmentOptions.TopLeft);

            panel._pie = CrearTexto(raiz.transform, "Pie",
                new Vector3(0f, -alto * 0.5f + margen + fuentePie * 0.5f, zTexto),
                new Vector2(ancho - margen * 2f, fuentePie * 2.6f), fuentePie,
                TextAlignmentOptions.Left);

            // El botón de confirmar. Ver el comentario de ActualizarBotonConfirmar.
            panel._botonConfirmar = VdfBotonPractica.Construir(
                raiz.transform, IndiceBotonConfirmar, "LISTO, ENTENDIDO",
                new Vector3(ancho * 0.5f + alto * 0.25f + 0.012f, alto * 0.5f - alto * 0.5f - 0.03f - fuentePie * 1.4f, zTexto),
                new Vector2(alto * 0.5f + 0.012f, fuentePie * 2.6f),
                new Color(0.16f, 0.52f, 0.26f), Color.white, fuentePie * 1.05f);
            panel.EngancharBotonConfirmar();
            panel._botonConfirmar.gameObject.SetActive(false);

            // La foto de la pieza encontrada, al lado de su ficha. Ver VdfFotoFicha.
            VdfFotoFicha.Construir(panel, ancho, alto);

            panel.Refrescar();
            return panel;
        }

        private static TextMeshPro CrearTexto(Transform padre, string nombre, Vector3 pos,
                                              Vector2 caja, float tamano,
                                              TextAlignmentOptions alineacion)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.alignment = alineacion;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;

            // Tamaño fijo y no autoajuste: aquí el texto cambia constantemente, y con
            // autoajuste el cuerpo bailaría de tamaño en cada refresco, que marea.
            //
            // El factor es 10 y no 40: en un TextMeshPro de mundo con escala 1, fontSize 36
            // mide unas 3,6 unidades de alto, o sea 0,1 por punto. Con 40 el panel salía con
            // letras de 7 cm desbordándolo entero.
            tmp.enableAutoSizing = false;
            tmp.fontSize = tamano * 10f;

            var rt = tmp.rectTransform;
            rt.sizeDelta = caja;
            rt.pivot = new Vector2(0.5f, 0.5f);

            return tmp;
        }
    }
}
