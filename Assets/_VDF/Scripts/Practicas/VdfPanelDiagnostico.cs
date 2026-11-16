
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// El tablero de diagnóstico de la práctica 5: una lista de causas posibles donde el
    /// alumno dice cuál cree que es la avería.
    ///
    /// Por qué existe: el guion de la práctica 5 tiene un paso que dice «di cuál crees que es
    /// la causa de la avería», y hasta ahora no había dónde decirlo. El catálogo de seis
    /// averías estaba escrito, probado y con sus explicaciones redactadas, pero el alumno no
    /// tenía forma de señalar ninguna: el paso quedaba sin cumplir hiciera lo que hiciera, y
    /// con él la práctica entera.
    ///
    /// Acertar no es gratis ni definitivo: al fallar lo dice y deja volver a intentarlo, y al
    /// acertar enseña la explicación. Lo que se evalúa es el diagnóstico, no la suerte, así
    /// que el intento fallido se cuenta y se muestra.
    /// </summary>
    public class VdfPanelDiagnostico : MonoBehaviour
    {
        public const string NombreRaiz = "VDF_PanelDiagnostico";

        [Header("Referencias")]
        public VdfPracticaRunner Runner;

        [Header("Colores")]
        public Color ColorFondo = new Color(0.10f, 0.11f, 0.13f);
        public Color ColorBoton = new Color(0.22f, 0.26f, 0.32f);
        public Color ColorAcierto = new Color(0.16f, 0.55f, 0.32f);
        public Color ColorFallo = new Color(0.55f, 0.22f, 0.20f);
        public Color ColorTexto = new Color(0.88f, 0.90f, 0.93f);

        [SerializeField] private List<VdfBotonPractica> _botones = new List<VdfBotonPractica>();
        [SerializeField] private TextMeshPro _resultado;
        [SerializeField] private TextMeshPro _titulo;
        private List<VdfFallaDidactica> _fallas;

        /// <summary>Cuántas veces se ha señalado una causa equivocada.</summary>
        public int Fallos { get; private set; }

        /// <summary>¿Se ha acertado ya?</summary>
        public bool Acertado { get; private set; }

        private void LateUpdate()
        {
            var falla = Runner != null ? Runner.Contexto.FallaActiva : null;
            if (falla != _fallaMostrada)
            {
                _fallaMostrada = falla;
                Reiniciar();
            }

            bool visible = Runner != null && Runner.Practica != null && Runner.Practica.NecesitaAveria();
            if (visible == _visible && _visibleIniciado) return;
            _visible = visible;
            _visibleIniciado = true;
            foreach (Transform hijo in transform) hijo.gameObject.SetActive(visible);
        }

        private bool _visible;
        private VdfFallaDidactica _fallaMostrada;
        private bool _visibleIniciado;

        private void Awake()
        {
            if (Runner == null) Runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            if (_fallas == null) _fallas = VdfCatalogoFallas.Crear();
            Reenganchar();
        }

        private void Reenganchar()
        {
            if (_botones == null) _botones = new List<VdfBotonPractica>();
            if (_botones.Count == 0)
                _botones.AddRange(GetComponentsInChildren<VdfBotonPractica>(true));

            for (int i = 0; i < _botones.Count; i++)
            {
                if (_botones[i] == null) continue;
                _botones[i].AlPulsar = delegate(int n) { Senalar(n); };
            }

            if (_resultado == null) _resultado = BuscarTexto("Resultado");
            if (_titulo == null) _titulo = BuscarTexto("Titulo");
        }

        private TextMeshPro BuscarTexto(string nombre)
        {
            var textos = GetComponentsInChildren<TextMeshPro>(true);
            for (int i = 0; i < textos.Length; i++)
                if (textos[i] != null && textos[i].gameObject.name == nombre) return textos[i];
            return null;
        }

        /// <summary>
        /// El alumno señala una causa. Devuelve true si ha acertado.
        ///
        /// Es público y con índice para que lo puedan llamar igual el ratón, el rayo de VR y
        /// un test, como el resto de interacciones del proyecto.
        /// </summary>
        public bool Senalar(int indice)
        {
            // Se pide de nuevo cada vez: el catálogo depende del modo de mando (con
            // interruptores la avería de «no arranca» es H1-04), y el Awake de este panel
            // puede correr antes de que la simulación fije el modo. Los títulos, que son lo
            // que muestran los botones, son los mismos en los dos modos.
            _fallas = VdfCatalogoFallas.Crear();
            if (indice < 0 || indice >= _fallas.Count) return false;

            var elegida = _fallas[indice];
            var activa = Runner != null ? Runner.Contexto.FallaActiva : null;

            if (activa == null)
            {
                Escribir("Ahora mismo el módulo no tiene ninguna avería provocada.", ColorTexto);
                return false;
            }

            if (elegida.Id != activa.Id)
            {
                Fallos++;
                Pintar(indice, ColorFallo);
                Escribir("No es eso. " + elegida.Titulo + " dejaría otro rastro: vuelve a operar " +
                         "el módulo y fíjate en qué hace y qué no.", ColorTexto);
                return false;
            }

            Acertado = true;
            Pintar(indice, ColorAcierto);
            if (Runner != null) Runner.Identificar(activa.Id);
            Escribir(ExplicacionParaElPanel(activa.Explicacion), ColorTexto);
            return true;
        }

        public static string ExplicacionParaElPanel(string explicacion)
        {
            if (string.IsNullOrEmpty(explicacion)) return explicacion;
            const string marca = "CÓMO REPARARLO:";
            int i = explicacion.IndexOf(marca, System.StringComparison.Ordinal);
            if (i < 0) return explicacion;
            string arreglo = explicacion.Substring(i).Trim();
            string resto = explicacion.Substring(0, i).TrimEnd();
            return "<b>" + arreglo + "</b>" + (char)10 + resto;
        }

        /// <summary>Lo que ocupa el resultado y lo que mide su caja. Para los tests.</summary>
        public Vector2 AltoResultadoYCaja(string texto)
        {
            if (_resultado == null) return Vector2.zero;
            var caja = _resultado.rectTransform.rect;
            return new Vector2(_resultado.GetPreferredValues(texto, caja.width, 0f).y, caja.height);
        }

        /// <summary>Vuelve a dejarlo como al empezar, para la siguiente avería.</summary>
        public void Reiniciar()
        {
            Fallos = 0;
            Acertado = false;
            for (int i = 0; i < _botones.Count; i++)
                if (_botones[i] != null) _botones[i].Pintar(ColorBoton);
            Escribir("Opera el módulo, observa qué hace y señala la causa.", ColorTexto);
        }

        private void Pintar(int indice, Color color)
        {
            if (indice >= 0 && indice < _botones.Count && _botones[indice] != null)
                _botones[indice].Pintar(color);
        }

        private void Escribir(string texto, Color color)
        {
            if (_resultado == null) return;
            _resultado.text = texto;
            _resultado.color = color;
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        /// <summary>
        /// Monta el panel en la escena. Idempotente: si ya existe, lo rehace.
        ///
        /// Como el menú de prácticas, el alto sale del número de opciones y las letras del
        /// ancho, para que siga cuadrando si el catálogo crece.
        /// </summary>
        public static VdfPanelDiagnostico Construir(Vector3 posicion, float rotacionY = 0f,
                                                    float ancho = 0.46f)
        {
            var previo = GameObject.Find(NombreRaiz);
            if (previo != null)
            {
                if (Application.isPlaying) Destroy(previo); else DestroyImmediate(previo);
            }

            var raiz = new GameObject(NombreRaiz);
            raiz.transform.position = posicion;
            raiz.transform.rotation = Quaternion.Euler(0f, rotacionY, 0f);

            var panel = raiz.AddComponent<VdfPanelDiagnostico>();
            panel._fallas = VdfCatalogoFallas.Crear();

            float margen = ancho * 0.042f;
            float fuenteTitulo = ancho * 0.042f;
            float fuenteBoton = ancho * 0.038f;   // 0,030 se veía a 0,65° desde el puesto
            float fuenteResultado = ancho * 0.040f;
            float altoBoton = ancho * 0.084f;
            float paso = altoBoton * 1.18f;

            float altoTitulo = margen * 2f + fuenteTitulo * 1.4f;
            // 17 líneas y no 7: con 7 todas las explicaciones se cortaban por abajo, la de
            // b1-01 casi a la mitad. El panel crece hacia ARRIBA: por abajo ya roza la mesa.
            // Lo vigila LaExplicacionDeCadaAveria_CabeEnElPanel.
            const float LineasResultado = 17f;
            float altoResultado = fuenteResultado * LineasResultado + margen;
            float alto = altoTitulo + panel._fallas.Count * paso + altoResultado;
            raiz.transform.position = posicion + Vector3.up * (fuenteResultado * (LineasResultado - 7f) * 0.5f);

            var fondo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fondo.name = "Fondo";
            fondo.transform.SetParent(raiz.transform, false);
            fondo.transform.localScale = new Vector3(ancho, alto, 0.012f);
            var col = fondo.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            fondo.GetComponent<Renderer>().sharedMaterial =
                VdfPanelBuilder.ObtenerMaterial(panel.ColorFondo, 0.15f);

            // Se reutiliza el helper de texto del panel Ladder: los cuatro atriles del
            // proyecto lo necesitan igual y no tiene sentido una cuarta copia.
            panel._titulo = VdfLadderPanel.Texto(raiz.transform, "Titulo",
                new Vector3(0f, alto * 0.5f - margen - fuenteTitulo * 0.5f, -0.008f),
                new Vector2(ancho - margen * 2f, fuenteTitulo * 1.5f), fuenteTitulo,
                TextAlignmentOptions.Center, panel.ColorTexto);
            panel._titulo.text = "¿CUÁL ES LA CAUSA?   <size=60%>apunta a tu respuesta y aprieta el gatillo</size>";

            float y = alto * 0.5f - altoTitulo - paso * 0.5f;
            for (int i = 0; i < panel._fallas.Count; i++)
            {
                var falla = panel._fallas[i];
                var boton = VdfBotonPractica.Construir(raiz.transform, i, falla.Titulo,
                    new Vector3(0f, y - i * paso, -0.010f),
                    new Vector2(ancho - margen * 2.4f, altoBoton),
                    panel.ColorBoton, panel.ColorTexto, fuenteBoton);

                // Cada botón avisa al panel, no al menú de prácticas.
                int indice = i;
                boton.AlPulsar = delegate(int n) { panel.Senalar(n); };
                panel._botones.Add(boton);
            }

            panel._resultado = VdfLadderPanel.Texto(raiz.transform, "Resultado",
                new Vector3(0f, -alto * 0.5f + altoResultado * 0.5f, -0.008f),
                new Vector2(ancho - margen * 2f, altoResultado - margen), fuenteResultado,
                TextAlignmentOptions.TopLeft, panel.ColorTexto);

            // El helper que se reutiliza viene del diagrama Ladder, donde todos los textos son
            // etiquetas de una palabra y por eso no parte líneas. Aquí se escriben las
            // explicaciones del catálogo, de varias líneas: sin esto, la explicación salía
            // del panel y seguía escrita sobre la pared del aula.
            panel._resultado.textWrappingMode = TextWrappingModes.Normal;
            panel._resultado.overflowMode = TextOverflowModes.Truncate;

            panel.Reiniciar();
            return panel;
        }
    }
}
