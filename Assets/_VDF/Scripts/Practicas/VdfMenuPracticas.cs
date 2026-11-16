
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VDF
{
    /// <summary>
    /// Menú para elegir práctica, montado como un atril más en la escena.
    ///
    /// No es un Canvas de pantalla: en VR no habría dónde dibujarlo. Es geometría con
    /// botones que se pulsan igual con el ratón, con el rayo del mando o con el dedo,
    /// porque cada botón es un collider y quien decide es
    /// <see cref="Seleccionar"/>, que no sabe de dónde viene la pulsación.
    ///
    /// El modo (VR o escritorio) **no se elige aquí**: lo decide qué escena se abre, porque
    /// las dos escenas tienen rig y cámara distintos. Lo que el menú hace es ofrecer las
    /// cinco prácticas y arrancar la elegida.
    /// </summary>
    public class VdfMenuPracticas : MonoBehaviour
    {
        public const string NombreRaiz = "VDF_MenuPracticas";

        [Header("Referencias")]
        public VdfPracticaRunner Runner;
        public VdfPanelGuia Panel;

        [Tooltip("Las prácticas que se ofrecen, en orden.")]
        public List<VdfPractica> Practicas = new List<VdfPractica>();

        [Header("Aspecto")]
        public Color ColorFondo = new Color(0.10f, 0.11f, 0.13f);
        public Color ColorBoton = new Color(0.20f, 0.33f, 0.46f);
        public Color ColorBotonElegido = new Color(0.16f, 0.55f, 0.32f);
        public Color ColorTexto = Color.white;

        [SerializeField] private TextMeshPro _titulo;
        [SerializeField] private TextMeshPro _descripcion;
        [SerializeField] private List<VdfBotonPractica> _botones = new List<VdfBotonPractica>();

        private void Awake()
        {
            if (Runner == null) Runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            if (Panel == null) Panel = Object.FindAnyObjectByType<VdfPanelGuia>();

            // Los botones no cuelgan necesariamente de este objeto tras una reconstrucción;
            // se vuelven a buscar, como el resto de componentes construidos por código.
            if (_botones == null || _botones.Count == 0)
                _botones = new List<VdfBotonPractica>(GetComponentsInChildren<VdfBotonPractica>(true));

            foreach (var b in _botones) if (b != null) b.Menu = this;

            Refrescar();
        }

        /// <summary>
        /// Arranca una práctica. Lo llaman los botones, y también se puede llamar desde
        /// fuera para saltar directamente a una práctica concreta.
        /// </summary>
        public void Seleccionar(int indice)
        {
            if (Practicas == null || indice < 0 || indice >= Practicas.Count) return;

            var practica = Practicas[indice];
            if (practica == null) return;

            if (Runner != null) Runner.Empezar(practica);
            Refrescar();

            if (Panel != null) Panel.Refrescar();
        }

        private void Update()
        {
            var raton = Mouse.current;
            if (raton == null || !raton.leftButton.wasPressedThisFrame) return;

            var cam = Camera.main;
            if (cam == null) return;

            RaycastHit hit;
            var rayo = cam.ScreenPointToRay(raton.position.ReadValue());
            if (!Physics.Raycast(rayo, out hit, 50f, ~0, QueryTriggerInteraction.Collide)) return;

            var boton = hit.collider.GetComponentInParent<VdfBotonPractica>();
            if (boton != null) boton.Pulsar();
        }

        public void Refrescar()
        {
            var actual = Runner != null ? Runner.Practica : null;

            // Dice ademas COMO se elige: apuntar y gatillo no es obvio para quien nunca
            // se ha puesto un visor, y por aqui entra todo el mundo.
            if (_titulo != null) _titulo.text = "ELIGE UNA PRÁCTICA   <size=60%>apunta con el mando y aprieta el gatillo</size>";

            if (_descripcion != null)
            {
                _descripcion.text = actual != null
                    ? "<b>" + actual.Titulo + "</b>\n" + actual.Descripcion
                    : "Ninguna práctica en curso.";
            }

            for (int i = 0; i < _botones.Count; i++)
            {
                if (_botones[i] == null) continue;

                bool elegida = actual != null && i < Practicas.Count && Practicas[i] == actual;
                _botones[i].Pintar(elegida ? ColorBotonElegido : ColorBoton);
            }
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        /// <summary>Monta el menú en la escena. Idempotente.</summary>
        public static VdfMenuPracticas Construir(Vector3 posicion, List<VdfPractica> practicas,
                                                 float rotacionY = -25f,
                                                 float ancho = 0.42f, float alto = 0f)
        {
            var previo = GameObject.Find(NombreRaiz);
            if (previo != null)
            {
                if (Application.isPlaying) Destroy(previo); else DestroyImmediate(previo);
            }

            var raiz = new GameObject(NombreRaiz);
            raiz.transform.position = posicion;
            raiz.transform.rotation = Quaternion.Euler(0f, rotacionY, 0f);

            var menu = raiz.AddComponent<VdfMenuPracticas>();
            menu.Practicas = practicas ?? new List<VdfPractica>();

            // Dos reglas que vienen de dos problemas distintos y que aquí conviven:
            //
            // 1. Todo lo horizontal y las letras salen del ANCHO. Con medidas fijas, al
            //    encoger el menú para que cupiera al lado del tablero de 50 cm los botones se
            //    salían por abajo del fondo.
            // 2. El ALTO sale del número de prácticas. Con altura fija, al añadir la quinta
            //    práctica el último botón se solapaba con la descripción.
            //
            // Por eso el alto es opcional: si no se pide uno concreto, se calcula.
            float margen = ancho * 0.048f;
            float fuenteTitulo = ancho * 0.048f;
            float fuenteBoton = ancho * 0.044f;   // 0,036 se veía a 0,6° desde el puesto: ver EscalaAtrilesVr
            float fuenteDescripcion = ancho * 0.040f;
            float altoBoton = ancho * 0.100f;
            float paso = altoBoton * 1.15f;

            float altoTitulo = margen * 2f + fuenteTitulo * 1.4f;
            float altoDescripcion = fuenteDescripcion * 5.5f + margen;
            int nPracticas = menu.Practicas != null ? menu.Practicas.Count : 0;
            if (alto <= 0f) alto = altoTitulo + nPracticas * paso + altoDescripcion;

            var fondo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fondo.name = "Fondo";
            fondo.transform.SetParent(raiz.transform, false);
            fondo.transform.localScale = new Vector3(ancho, alto, 0.012f);
            Quitar(fondo.GetComponent<Collider>());
            fondo.GetComponent<Renderer>().sharedMaterial =
                VdfPanelBuilder.ObtenerMaterial(menu.ColorFondo, 0.15f);

            menu._titulo = Texto(raiz.transform, "Titulo",
                new Vector3(0f, alto * 0.5f - margen - fuenteTitulo * 0.5f, -0.008f),
                new Vector2(ancho - margen * 2f, fuenteTitulo * 1.5f), fuenteTitulo,
                TextAlignmentOptions.Center, menu.ColorTexto);

            // Un botón por práctica, en columna.
            float yBoton = alto * 0.5f - altoTitulo - paso * 0.5f;

            menu._botones = new List<VdfBotonPractica>();
            for (int i = 0; i < menu.Practicas.Count; i++)
            {
                var p = menu.Practicas[i];
                if (p == null) continue;

                var boton = VdfBotonPractica.Construir(raiz.transform, i,
                    p.Numero + ".  " + p.Titulo,
                    new Vector3(0f, yBoton - i * paso, -0.010f),
                    new Vector2(ancho - margen * 2.4f, altoBoton),
                    menu.ColorBoton, menu.ColorTexto, fuenteBoton);

                boton.Menu = menu;
                menu._botones.Add(boton);
            }

            menu._descripcion = Texto(raiz.transform, "Descripcion",
                new Vector3(0f, -alto * 0.5f + altoDescripcion * 0.5f, -0.008f),
                new Vector2(ancho - margen * 2f, altoDescripcion - margen), fuenteDescripcion,
                TextAlignmentOptions.TopLeft, new Color(0.70f, 0.73f, 0.78f));

            menu.Refrescar();
            return menu;
        }

        private static void Quitar(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private static TextMeshPro Texto(Transform padre, string nombre, Vector3 pos,
                                         Vector2 caja, float tamano,
                                         TextAlignmentOptions alineacion, Color color)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.color = color;
            tmp.alignment = alineacion;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            tmp.enableAutoSizing = false;
            // Mismo factor que el panel de guía: 0,1 unidades de mundo por punto.
            tmp.fontSize = tamano * 10f;

            tmp.rectTransform.sizeDelta = caja;
            return tmp;
        }
    }
}
